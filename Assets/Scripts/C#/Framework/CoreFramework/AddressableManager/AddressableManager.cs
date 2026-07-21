using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace CoreFramework
{
    /// <summary>
    /// Addressables 资源管理器。
    /// 以“共享条目 + Lease + Scope”管理资源所有权，避免业务层手动 key 配对释放。
    /// </summary>
    public sealed class AddressableManager
    {
        private static readonly AddressableManager instance = new AddressableManager();
        public static AddressableManager Instance => instance;

        private readonly Dictionary<string, ResourceEntry> resources = new Dictionary<string, ResourceEntry>(32);
        private readonly Dictionary<int, ResourceLease> activeLeases = new Dictionary<int, ResourceLease>(64);
        private int nextLeaseId = 1;

        private AddressableManager() { }

        /// <summary>
        /// 异步获取一个资源 Lease。相同 key+type 只会共享一个底层加载条目。
        /// 调用方应在不再使用资源时释放 Lease，或将其绑定到 Scope 上。
        /// </summary>
        public async Task<ResourceLease<T>> AcquireAssetAsync<T>(string key, ResourceScope scope = null) where T : Object
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Addressable key 不能为空。", nameof(key));

            string resourceKey = BuildResourceKey<T>(key);
            ResourceEntry entry = await GetOrLoadEntryAsync<T>(resourceKey, key);
            if (!entry.IsLoaded || entry.Asset == null)
                throw new InvalidOperationException($"Addressable 资源加载失败：{key}({typeof(T).Name})");

            int leaseId = nextLeaseId++;
            if (leaseId == int.MaxValue)
            {
                nextLeaseId = 1;
                leaseId = nextLeaseId++;
            }

            entry.ReferenceCount += 1;
            ResourceLease<T> lease = new ResourceLease<T>(this, leaseId, resourceKey, entry.Asset as T, scope);
            activeLeases.Add(leaseId, lease);
            scope?.Register(lease);
            return lease;
        }

        /// <summary>
        /// 异步获取一个常驻资源 Lease。适用于 UI 根节点等长期驻留资源。
        /// </summary>
        public Task<ResourceLease<T>> AcquirePersistentAssetAsync<T>(string key) where T : Object
        {
            return AcquireAssetAsync<T>(key, ResourceScope.Persistent);
        }

        /// <summary>
        /// 仅在资源已加载完成时同步获取，不会触发新的同步加载阻塞。
        /// </summary>
        public bool TryGetLoadedAsset<T>(string key, out T asset) where T : Object
        {
            string resourceKey = BuildResourceKey<T>(key);
            if (resources.TryGetValue(resourceKey, out ResourceEntry entry) &&
                entry.IsLoaded &&
                entry.Asset is T typedAsset)
            {
                asset = typedAsset;
                return true;
            }

            asset = null;
            return false;
        }

        /// <summary>
        /// 查询资源是否存在于当前缓存中且已加载完成。
        /// </summary>
        public bool IsAssetLoaded<T>(string key) where T : Object
        {
            return TryGetEntry<T>(key, out ResourceEntry entry) && entry.IsLoaded && entry.Asset != null;
        }

        /// <summary>
        /// 查询资源的加载状态。未缓存时返回 None。
        /// </summary>
        public AsyncOperationStatus GetResourceStatus<T>(string key) where T : Object
        {
            return TryGetEntry<T>(key, out ResourceEntry entry)
                ? entry.Handle.Status
                : AsyncOperationStatus.None;
        }

        /// <summary>
        /// 获取资源调试信息。仅用于调试和诊断。
        /// </summary>
        public bool TryGetResourceDebugInfo<T>(string key, out ResourceDebugInfo debugInfo) where T : Object
        {
            if (!TryGetEntry<T>(key, out ResourceEntry entry))
            {
                debugInfo = default;
                return false;
            }

            debugInfo = new ResourceDebugInfo(
                entry.Key,
                entry.Asset,
                entry.ReferenceCount,
                entry.Handle.Status,
                entry.LoadFailed);
            return true;
        }

        /// <summary>
        /// 释放所有引用计数已归零但仍残留在缓存中的资源。
        /// 正常流程下不应依赖该接口，仅作为兜底清理。
        /// </summary>
        public void ReleaseUnusedResources()
        {
            if (resources.Count == 0)
                return;

            List<string> pendingRemoval = null;
            foreach (KeyValuePair<string, ResourceEntry> pair in resources)
            {
                ResourceEntry entry = pair.Value;
                if (entry.ReferenceCount > 0)
                    continue;

                entry.ReleaseHandle();
                pendingRemoval ??= new List<string>(4);
                pendingRemoval.Add(pair.Key);
            }

            if (pendingRemoval == null)
                return;

            for (int i = 0; i < pendingRemoval.Count; i++)
                resources.Remove(pendingRemoval[i]);
        }

        /// <summary>
        /// 强制释放所有缓存资源和所有活动 Lease。仅限关机、域清理或测试复位使用。
        /// </summary>
        public void ForceReleaseAllResourcesForShutdown()
        {
            if (activeLeases.Count > 0)
            {
                List<ResourceLease> leases = new List<ResourceLease>(activeLeases.Values);
                for (int i = 0; i < leases.Count; i++)
                    leases[i].ForceDisposeSilently();

                activeLeases.Clear();
            }

            foreach (KeyValuePair<string, ResourceEntry> pair in resources)
                pair.Value.ReleaseHandle();

            resources.Clear();
        }

        internal void ReleaseLease(int leaseId, string resourceKey)
        {
            if (!activeLeases.Remove(leaseId, out ResourceLease lease))
                return;

            if (!resources.TryGetValue(resourceKey, out ResourceEntry entry))
                return;

            entry.ReferenceCount -= 1;
            if (entry.ReferenceCount < 0)
            {
                entry.ReferenceCount = 0;
                Debug.LogError($"AddressableManager 检测到重复释放：{resourceKey}");
            }

            if (entry.ReferenceCount == 0)
            {
                entry.ReleaseHandle();
                resources.Remove(resourceKey);
            }
        }

        private bool TryGetEntry<T>(string key, out ResourceEntry entry) where T : Object
        {
            return resources.TryGetValue(BuildResourceKey<T>(key), out entry);
        }

        private async Task<ResourceEntry> GetOrLoadEntryAsync<T>(string resourceKey, string addressableKey) where T : Object
        {
            if (resources.TryGetValue(resourceKey, out ResourceEntry existingEntry))
            {
                await existingEntry.Task;
                return existingEntry;
            }

            AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(addressableKey);
            ResourceEntry entry = new ResourceEntry(resourceKey, handle);
            resources.Add(resourceKey, entry);

            await entry.Task;
            if (!entry.IsLoaded)
            {
                entry.ReleaseHandle();
                resources.Remove(resourceKey);
            }

            return entry;
        }

        private static string BuildResourceKey<T>(string key) where T : Object
        {
            return typeof(T).FullName + "::" + key;
        }
    }

    /// <summary>
    /// 资源调试快照。
    /// </summary>
    public readonly struct ResourceDebugInfo
    {
        public readonly string Key;
        public readonly Object Asset;
        public readonly int ReferenceCount;
        public readonly AsyncOperationStatus Status;
        public readonly bool LoadFailed;

        public ResourceDebugInfo(string key, Object asset, int referenceCount, AsyncOperationStatus status, bool loadFailed)
        {
            Key = key;
            Asset = asset;
            ReferenceCount = referenceCount;
            Status = status;
            LoadFailed = loadFailed;
        }
    }

    /// <summary>
    /// 资源作用域。作用域释放时会自动释放其名下所有 Lease。
    /// </summary>
    public class ResourceScope : IDisposable
    {
        public static ResourceScope Persistent { get; } = new ResourceScope("Persistent", false);

        private readonly List<ResourceLease> leases = new List<ResourceLease>(4);
        private readonly bool autoDisposeEnabled;

        public string Name { get; }
        public bool IsDisposed { get; private set; }

        public ResourceScope(string name = null, bool autoDisposeEnabled = true)
        {
            Name = string.IsNullOrWhiteSpace(name) ? "UnnamedScope" : name;
            this.autoDisposeEnabled = autoDisposeEnabled;
        }

        internal void Register(ResourceLease lease)
        {
            if (lease == null || IsDisposed)
                return;

            leases.Add(lease);
        }

        internal void Unregister(ResourceLease lease)
        {
            if (lease == null || leases.Count == 0)
                return;

            leases.Remove(lease);
        }

        public void Dispose()
        {
            if (IsDisposed || !autoDisposeEnabled)
                return;

            IsDisposed = true;
            for (int i = leases.Count - 1; i >= 0; i--)
                leases[i]?.Dispose();

            leases.Clear();
        }
    }

    /// <summary>
    /// 资源引用票据。持有资源实例与一份独立引用。
    /// </summary>
    public abstract class ResourceLease : IDisposable
    {
        private readonly AddressableManager owner;
        private readonly int leaseId;
        private readonly string resourceKey;

        internal ResourceLease(AddressableManager owner, int leaseId, string resourceKey, ResourceScope scope)
        {
            this.owner = owner;
            this.leaseId = leaseId;
            this.resourceKey = resourceKey;
            Scope = scope;
        }

        public ResourceScope Scope { get; private set; }
        public bool IsReleased { get; private set; }

        public void Dispose()
        {
            if (IsReleased)
                return;

            IsReleased = true;
            Scope?.Unregister(this);
            Scope = null;
            owner.ReleaseLease(leaseId, resourceKey);
        }

        internal void ForceDisposeSilently()
        {
            if (IsReleased)
                return;

            IsReleased = true;
            Scope?.Unregister(this);
            Scope = null;
        }
    }

    /// <summary>
    /// 强类型资源 Lease。
    /// </summary>
    public sealed class ResourceLease<T> : ResourceLease where T : Object
    {
        public T Asset { get; }

        internal ResourceLease(AddressableManager owner, int leaseId, string resourceKey, T asset, ResourceScope scope = null) : base(owner, leaseId, resourceKey, scope)
        {
            Asset = asset;
        }
    }

    internal sealed class ResourceEntry
    {
        public string Key { get; }
        public AsyncOperationHandle Handle { get; }
        public Task Task { get; }
        public Object Asset => Handle.IsValid() ? Handle.Result as Object : null;
        public bool IsLoaded => Handle.IsValid() && Handle.Status == AsyncOperationStatus.Succeeded;
        public bool LoadFailed => Handle.IsValid() && Handle.Status == AsyncOperationStatus.Failed;
        public int ReferenceCount;

        public ResourceEntry(string key, AsyncOperationHandle handle)
        {
            Key = key;
            Handle = handle;
            Task = handle.Task;
            ReferenceCount = 0;
        }

        public void ReleaseHandle()
        {
            if (Handle.IsValid())
                Addressables.Release(Handle);
        }
    }
}
