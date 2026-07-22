using System.Collections.Generic;
using CoreFramework;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// World-space damage number pool.
/// </summary>
public class DamageNumberPool : MonoBehaviour
{
    private sealed class ActiveDamageNumber
    {
        public ActiveDamageNumber(GameObject rootObject, TMP_Text text, Vector3 initialScale, Vector3 finalScale)
        {
            RootObject = rootObject;
            Text = text;
            Elapsed = 0f;
            InitialScale = initialScale;
            FinalScale = finalScale;
        }

        public GameObject RootObject { get; }
        public TMP_Text Text { get; }
        public float Elapsed { get; set; }
        public Vector3 InitialScale { get; }
        public Vector3 FinalScale { get; }
    }

    private static DamageNumberPool instance;

    public static DamageNumberPool Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject(nameof(DamageNumberPool));
                instance = go.AddComponent<DamageNumberPool>();
                DontDestroyOnLoad(go);
            }

            return instance;
        }
    }

    [Tooltip("Final minimum scale of the damage number.")] public float minSkipCharSize = 1f;
    [Tooltip("Initial spawn scale multiplier of the damage number.")] public float maxSkipCharSize = 1f;
    [Tooltip("Scale interpolation speed of the damage number.")] public float skipCharScaleSpeed = 6f;
    [Tooltip("Vertical drift speed of the damage number.")] public float skipCharDropSpeed = -0.8f;
    [Tooltip("Base local offset above the target.")] public Vector3 baseOffset = new Vector3(0f, 0.8f, 0f);
    [Tooltip("Random local offset range for each spawned damage number.")] public Vector3 randomOffsetRange = new Vector3(0.3f, 0f, 0.2f);
    [Tooltip("Optional direct prefab override for the damage number.")] public GameObject skipCharPrefab;
    [Tooltip("Optional direct prefab override for the anchor under the target.")] public GameObject skipCharAgentPrefab;
    [Tooltip("Object pool key of the damage number prefab.")] public string skipCharPoolKey = "SkipChar";
    [Tooltip("Object pool key of the anchor prefab.")] public string skipCharAgentPoolKey = "SkipCharAgent";
    [Tooltip("Child anchor name when reusing an existing anchor.")] public string skipCharAgentName = "SkipCharAgent";
    [Tooltip("Initial world scale of the damage number text.")] public Vector3 initialWorldScale = Vector3.one * 0.25f;
    [Tooltip("Final world scale of the damage number text.")] public Vector3 finalWorldScale = Vector3.one * 0.12f;

    private readonly List<ActiveDamageNumber> activeSkipChars = new();

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
        UpdateActiveSkipChars();
    }

    public void EnqueueDamage(GameObject target, float damage)
    {
        if (target == null)
            return;

        SpawnDamageNumber(target, damage);
    }

    private void SpawnDamageNumber(GameObject target, float damage)
    {
        Transform parent = FindOrCreateSkipCharAgent(target);
        if (parent == null)
            parent = target.transform;

        Vector3 localOffset = baseOffset + new Vector3(
            Random.Range(-randomOffsetRange.x, randomOffsetRange.x),
            Random.Range(-randomOffsetRange.y, randomOffsetRange.y),
            Random.Range(-randomOffsetRange.z, randomOffsetRange.z));
        SpawnSingleDamageNumber(parent, damage, localOffset);
    }

    private Transform FindOrCreateSkipCharAgent(GameObject target)
    {
        if (target == null)
            return null;

        Transform agent = target.transform.Find(skipCharAgentName);
        if (agent != null)
            return agent;

        if (skipCharAgentPrefab != null)
            return ObjectsPool.Instance.Get(skipCharAgentPrefab, target.transform)?.transform;

        GameObject go = new GameObject(skipCharAgentName);
        go.transform.SetParent(target.transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return go.transform;
    }

    private void SpawnSingleDamageNumber(Transform parent, float damage, Vector3 localOffset)
    {
        if (parent == null)
            return;

        if (skipCharPrefab != null)
        {
            GameObject instance = ObjectsPool.Instance.Get(skipCharPrefab, parent);
            ConfigureSkipChar(instance, damage, localOffset);
            return;
        }

        if (!string.IsNullOrWhiteSpace(skipCharPoolKey))
        {
            ObjectsPool.Instance.Get(skipCharPoolKey, parent,
                instance => ConfigureSkipChar(instance, damage, localOffset));
        }
    }

    private void ConfigureSkipChar(GameObject skipChar, float damage, Vector3 localOffset)
    {
        if (skipChar == null)
            return;

        TMP_Text text = ResolveTmpText(skipChar);
        if (text == null)
        {
            Debug.LogWarning("Damage number prefab is missing TMP_Text.", skipChar);
            return;
        }

        Transform textTransform = text.transform;
        text.text = Mathf.RoundToInt(damage).ToString();
        textTransform.localPosition = localOffset;
        textTransform.localRotation = Quaternion.identity;
        Vector3 initialScale = GetConfiguredInitialScale();
        Vector3 finalScale = GetConfiguredFinalScale();
        textTransform.localScale = initialScale;

        activeSkipChars.Add(new ActiveDamageNumber(skipChar, text, initialScale, finalScale));
    }

    private void UpdateActiveSkipChars()
    {
        Camera mainCamera = Camera.main;

        for (int i = 0; i < activeSkipChars.Count; i++)
        {
            ActiveDamageNumber entry = activeSkipChars[i];
            TMP_Text text = entry.Text;
            if (text == null)
            {
                activeSkipChars.RemoveAt(i);
                i--;
                continue;
            }

            Transform textTransform = text.transform;
            if (mainCamera != null)
                textTransform.rotation = mainCamera.transform.rotation;

            entry.Elapsed += Time.deltaTime;
            float scaleT = 1f - Mathf.Exp(-skipCharScaleSpeed * entry.Elapsed);
            Vector3 initialScale = entry.InitialScale;
            Vector3 finalScale = entry.FinalScale;
            textTransform.localScale = Vector3.LerpUnclamped(initialScale, finalScale, scaleT);
            textTransform.localPosition -= new Vector3(0f, skipCharDropSpeed * Time.deltaTime, 0f);

            if ((textTransform.localScale - finalScale).sqrMagnitude <= 0.0001f)
            {
                ObjectsPool.Instance.Put(entry.RootObject);
                activeSkipChars.RemoveAt(i);
                i--;
            }
        }
    }

    private Vector3 GetConfiguredInitialScale()
    {
        return Vector3.Scale(initialWorldScale, Vector3.one * Mathf.Max(0.01f, maxSkipCharSize));
    }

    private Vector3 GetConfiguredFinalScale()
    {
        return Vector3.Scale(finalWorldScale, Vector3.one * Mathf.Max(0.01f, minSkipCharSize));
    }

    private TMP_Text ResolveTmpText(GameObject skipChar)
    {
        if (skipChar == null)
            return null;

        TMP_Text tmpText = skipChar.GetComponentInChildren<TMP_Text>(true);
        if (tmpText != null)
            return tmpText;

        TextMesh legacyTextMesh = skipChar.GetComponentInChildren<TextMesh>(true);
        if (legacyTextMesh == null)
            return null;

        GameObject textObject = legacyTextMesh.gameObject;
        TextMeshPro upgraded = textObject.GetComponent<TextMeshPro>();
        if (upgraded == null)
            upgraded = textObject.AddComponent<TextMeshPro>();

        upgraded.text = legacyTextMesh.text;
        upgraded.color = legacyTextMesh.color;
        upgraded.alignment = TextAlignmentOptions.Center;
        upgraded.enableWordWrapping = false;
        upgraded.fontSize = Mathf.Max(2f, maxSkipCharSize * 2f);
        upgraded.raycastTarget = false;
        upgraded.isTextObjectScaleStatic = false;

        Destroy(legacyTextMesh);
        return upgraded;
    }
}
