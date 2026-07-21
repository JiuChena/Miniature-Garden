using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

namespace CoreFramework
{
    /// <summary>
    /// 对象池模块，缓存已实例化的 GameObject 避免重复 Instantiate/Destroy。
    /// Addressable prefab 由对象池自身持有 Lease，直到池显式清理。
    /// </summary>
    public class ObjectsPool : MonoBehaviour
    {
        private static ObjectsPool instance;

        public static ObjectsPool Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject obj = new GameObject("ObjectsPool");
                    instance = obj.AddComponent<ObjectsPool>();
                    DontDestroyOnLoad(obj);
                }

                return instance;
            }
        }

        private readonly Dictionary<string, Queue<GameObject>> objectPool = new Dictionary<string, Queue<GameObject>>();
        private readonly Dictionary<string, ResourceLease<GameObject>> prefabLeases = new Dictionary<string, ResourceLease<GameObject>>();
        private readonly Queue<BufferReturnObjectInfo> bufferQueue = new Queue<BufferReturnObjectInfo>();

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            int count = bufferQueue.Count;
            for (int i = 0; i < count; i++)
            {
                BufferReturnObjectInfo temp = bufferQueue.Dequeue();
                temp.delayTime -= Time.deltaTime;
                if (temp.delayTime <= 0f)
                    ReturnObject(temp.obj);
                else
                    bufferQueue.Enqueue(temp);
            }
        }

        /// <summary>
        /// 从对象池获取对象。若池中无可用对象则通过 Addressables 异步加载预制体后实例化。
        /// 对象池会保留 prefab 的 Lease，直到对应池被清理。
        /// </summary>
        public async Task GetObject(string objectName, Transform parent = null, UnityAction<GameObject> callback = null)
        {
            if (TryDequeuePooledObject(objectName, parent, callback, out GameObject pooledObject))
            {
                pooledObject.SetActive(true);
                return;
            }

            ResourceLease<GameObject> prefabLease = await GetOrAcquirePrefabLeaseAsync(objectName);
            if (prefabLease == null || prefabLease.Asset == null)
            {
                Debug.LogError($"对象池加载失败：{objectName}");
                return;
            }

            GameObject instanceObject = Instantiate(prefabLease.Asset);
            instanceObject.name = objectName;
            instanceObject.transform.SetParent(parent, true);
            callback?.Invoke(instanceObject);
            instanceObject.SetActive(true);
        }

        /// <summary>
        /// 从对象池获取对象。若池中无可用对象则直接 Instantiate 传入的 prefab。
        /// prefab 已在内存中，不走 Addressables 加载。
        /// </summary>
        public GameObject GetObject(GameObject prefab, Transform parent = null, UnityAction<GameObject> callback = null)
        {
            if (prefab == null)
                return null;

            if (TryDequeuePooledObject(prefab.name, parent, callback, out GameObject pooledObject))
            {
                pooledObject.SetActive(true);
                return pooledObject;
            }

            GameObject createdObject = Instantiate(prefab);
            createdObject.name = prefab.name;
            createdObject.transform.SetParent(parent, true);
            callback?.Invoke(createdObject);
            createdObject.SetActive(true);
            return createdObject;
        }

        /// <summary>
        /// 将对象返还对象池。
        /// </summary>
        public void ReturnObject(GameObject obj, UnityAction<GameObject> callback = null)
        {
            if (obj == null)
                return;

            callback?.Invoke(obj);

            if (!objectPool.TryGetValue(obj.name, out Queue<GameObject> queue))
            {
                queue = new Queue<GameObject>();
                objectPool.Add(obj.name, queue);
            }

            queue.Enqueue(obj);
            obj.transform.SetParent(transform, true);
            obj.SetActive(false);
        }

        /// <summary>
        /// 延迟返还对象到对象池，delayTime 秒后自动回池。
        /// </summary>
        public void ReturnObject(GameObject obj, float delayTime)
        {
            if (obj == null)
                return;

            bufferQueue.Enqueue(new BufferReturnObjectInfo(obj, delayTime));
        }

        /// <summary>
        /// 清空指定 prefab key 的缓存池，并释放其 prefab lease。
        /// </summary>
        public void ClearPool(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
                return;

            if (objectPool.TryGetValue(objectName, out Queue<GameObject> queue))
            {
                while (queue.Count > 0)
                {
                    GameObject pooledObject = queue.Dequeue();
                    if (pooledObject != null)
                        Destroy(pooledObject);
                }

                objectPool.Remove(objectName);
            }

            if (prefabLeases.TryGetValue(objectName, out ResourceLease<GameObject> prefabLease))
            {
                prefabLease.Dispose();
                prefabLeases.Remove(objectName);
            }
        }

        /// <summary>
        /// 清空所有对象池缓存，并释放所有 addressable prefab lease。
        /// </summary>
        public void ClearAllPools()
        {
            foreach (KeyValuePair<string, Queue<GameObject>> pair in objectPool)
            {
                Queue<GameObject> queue = pair.Value;
                while (queue.Count > 0)
                {
                    GameObject pooledObject = queue.Dequeue();
                    if (pooledObject != null)
                        Destroy(pooledObject);
                }
            }

            objectPool.Clear();

            foreach (KeyValuePair<string, ResourceLease<GameObject>> pair in prefabLeases)
                pair.Value?.Dispose();

            prefabLeases.Clear();
        }

        private bool TryDequeuePooledObject(string objectName, Transform parent, UnityAction<GameObject> callback, out GameObject pooledObject)
        {
            pooledObject = null;
            if (!objectPool.TryGetValue(objectName, out Queue<GameObject> queue) || queue.Count == 0)
                return false;

            pooledObject = queue.Dequeue();
            if (pooledObject == null)
                return false;

            pooledObject.transform.SetParent(parent, true);
            callback?.Invoke(pooledObject);
            return true;
        }

        private async Task<ResourceLease<GameObject>> GetOrAcquirePrefabLeaseAsync(string objectName)
        {
            if (prefabLeases.TryGetValue(objectName, out ResourceLease<GameObject> cachedLease) &&
                cachedLease != null &&
                !cachedLease.IsReleased &&
                cachedLease.Asset != null)
            {
                return cachedLease;
            }

            ResourceLease<GameObject> prefabLease =
                await AddressableManager.Instance.AcquireAssetAsync<GameObject>(objectName);
            prefabLeases[objectName] = prefabLease;
            return prefabLease;
        }
    }

    /// <summary>
    /// 延迟回池的缓冲数据。
    /// </summary>
    public class BufferReturnObjectInfo
    {
        public GameObject obj;
        public float delayTime;

        public BufferReturnObjectInfo(GameObject obj, float delayTime)
        {
            this.obj = obj;
            this.delayTime = delayTime;
        }
    }
}
