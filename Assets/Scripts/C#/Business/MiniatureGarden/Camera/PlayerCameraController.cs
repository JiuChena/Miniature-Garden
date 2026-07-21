using UnityEngine;
using UnityEngine.InputSystem;
using Cinemachine;
using UnityEngine.Serialization;

/// <summary>
/// 玩家摄像机控制：负责镜头缩放、鼠标锁定与玩法输入启停时的镜头输入开关。
/// Alt / UI 等“是否允许玩法输入”的判定应由上层输入模块负责，这里只消费总开关结果。
/// </summary>
[RequireComponent(typeof(CinemachineVirtualCamera))]
public class PlayerCameraController : MonoBehaviour
{
    public static PlayerCameraController Instance { get; private set; }

    [Header("距离")]
    [SerializeField, Tooltip("最近距离（米）")]
    private float minDistance = 2f;

    [SerializeField, Tooltip("最远距离（米）")]
    private float maxDistance = 10f;

    [SerializeField, Tooltip("滚轮缩放整体倍率。值越大，每格滚轮带来的目标距离变化越明显")]
    private float zoomSpeed = 2f;

    [SerializeField, Tooltip("缩放平滑速度，越大越快")]
    [Range(1f, 20f)]
    private float zoomSmoothSpeed = 8f;

    [Space(8)]
    [Header("缩放手感")]
    [SerializeField, Tooltip("近距离时每格滚轮推进或拉远的基础距离，单位为米")]
    [Min(0.01f)]
    private float nearZoomStep = 0.35f;

    [SerializeField, Tooltip("远距离时每格滚轮推进或拉远的基础距离，单位为米。适当更大可减少远景滚动次数")]
    [Min(0.01f)]
    private float farZoomStep = 0.9f;

    [SerializeField, Tooltip("镜头向角色推进时的平滑速度，越大越快")]
    [Range(1f, 30f)]
    private float zoomInSmoothSpeed = 14f;

    [SerializeField, Tooltip("镜头向外拉远时的平滑速度，越大越快")]
    [Range(1f, 30f)]
    private float zoomOutSmoothSpeed = 9f;

    [Space(8)]
    [Header("构图联动")]
    [SerializeField, Tooltip("最近距离时 Framing Transposer 使用的目标跟踪点本地 Y 偏移")]
    private float nearTrackedOffsetY = 0.5f;

    [SerializeField, Tooltip("最远距离时 Framing Transposer 使用的目标跟踪点本地 Y 偏移。适当抬高可获得更稳定的远景可读性")]
    private float farTrackedOffsetY = 0.85f;

    [SerializeField, Tooltip("最近距离时目标在屏幕中的纵向位置，0.5 表示垂直居中")]
    [Range(0f, 1f)]
    private float nearScreenY = 0.5f;

    [SerializeField, Tooltip("最远距离时目标在屏幕中的纵向位置。略高于近景可保留更多地面与环境信息")]
    [Range(0f, 1f)]
    private float farScreenY = 0.57f;

    [SerializeField, Tooltip("缩放时构图联动的平滑速度，越大越快")]
    [Range(1f, 20f)]
    private float compositionSmoothSpeed = 8f;

    [Header("输入绑定")]
    [SerializeField, Tooltip("负责实际镜头旋转的 POV 组件。留空时会自动在当前物体及子物体中查找。")]
    private CinemachinePOV pov;

    [FormerlySerializedAs("_pov")]
    [SerializeField, Tooltip("Cinemachine 输入提供器。留空时会自动在当前物体中查找。")]
    private CinemachineInputProvider inputProvider;

    [Space(8)]
    [Header("震屏")]
    [SerializeField, Tooltip("镜头震动时允许的最大旋转角度，单位为度")]
    [Range(0f, 10f)]
    private float maxShakeRotation = 4f;

    private CinemachineVirtualCamera _vcam;
    private Cinemachine3rdPersonFollow _follow3rd;
    private CinemachineFramingTransposer _framing;
    private CinemachineTransposer _transposer;
    private float _targetDistance;
    private float _smoothDistance;
    private bool _cursorLocked;
    private Quaternion _baseLocalRotation;
    private Vector3 _baseTrackedObjectOffset;
    private float _smoothTrackedOffsetY;
    private float _smoothScreenY;
    private float _shakeTimer;
    private float _shakeAmplitude;
    private float _shakeFrequency;
    private bool _gameplayInputEnabled = true;
    private string _cachedVerticalAxisName;
    private string _cachedHorizontalAxisName;
    private bool _rotationInputSuspended;

    private void Reset()
    {
        AutoBindCameraInputReferences();
    }

    private void OnValidate()
    {
        AutoBindCameraInputReferences();
    }

    private void Awake()
    {
        Instance = this;
        _vcam = GetComponent<CinemachineVirtualCamera>();
        AutoBindCameraInputReferences();
    }

    private void Start()
    {
        var body = _vcam.GetCinemachineComponent(CinemachineCore.Stage.Body);
        _follow3rd = body as Cinemachine3rdPersonFollow;
        _framing   = body as CinemachineFramingTransposer;
        _transposer = body as CinemachineTransposer;
        AutoBindCameraInputReferences();

        _targetDistance = GetCurrentDistance();
        _smoothDistance = _targetDistance;
        _baseLocalRotation = transform.localRotation;
        CacheAxisNames();
        if (_framing != null)
        {
            _baseTrackedObjectOffset = _framing.m_TrackedObjectOffset;
            _smoothTrackedOffsetY = _baseTrackedObjectOffset.y;
            _smoothScreenY = _framing.m_ScreenY;
        }

        ApplyGameplayInputState();
    }

    private void Update()
    {
        if (_gameplayInputEnabled)
            HandleZoomInput();

        UpdateZoomDistance();
        UpdateZoomComposition();
        SynchronizeCursorState();
    }

    private void LateUpdate()
    {
        UpdateShake();
    }

    public void PlayShake(float amplitude, float frequency, float duration)
    {
        if (duration <= 0f || amplitude <= 0f)
            return;

        _shakeTimer = Mathf.Max(_shakeTimer, duration);
        _shakeAmplitude = Mathf.Max(_shakeAmplitude, amplitude);
        _shakeFrequency = Mathf.Max(_shakeFrequency, Mathf.Max(0.01f, frequency));
    }

    public void SetFollowTarget(Transform target)
    {
        if (_vcam == null)
            _vcam = GetComponent<CinemachineVirtualCamera>();

        if (_vcam == null)
            return;

        _vcam.Follow = target;
        if (_vcam.LookAt == null || _vcam.LookAt == _vcam.Follow)
            _vcam.LookAt = target;
    }

    public void SetGameplayInputEnabled(bool enabled)
    {
        if (_gameplayInputEnabled == enabled)
            return;

        _gameplayInputEnabled = enabled;
        ApplyGameplayInputState();
    }

    private void HandleZoomInput()
    {
        float scroll = Mouse.current?.scroll.ReadValue().y ?? 0f;
        scroll /= 120f;
        if (Mathf.Approximately(scroll, 0f))
            return;

        float normalizedDistance = GetDistanceNormalized(_targetDistance);
        float zoomScale = Mathf.Max(0.01f, zoomSpeed) * 0.2f;
        float dynamicStep = Mathf.Lerp(nearZoomStep, farZoomStep, normalizedDistance) * zoomScale;
        _targetDistance = Mathf.Clamp(_targetDistance - scroll * dynamicStep, minDistance, maxDistance);
    }

    private void UpdateZoomDistance()
    {
        float smoothSpeed = ResolveZoomSmoothSpeed();
        float lerpFactor = 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);
        _smoothDistance = Mathf.Lerp(_smoothDistance, _targetDistance, lerpFactor);

        if (Mathf.Abs(_smoothDistance - _targetDistance) < 0.001f)
            _smoothDistance = _targetDistance;

        SetDistance(Mathf.Clamp(_smoothDistance, minDistance, maxDistance));
    }

    private void UpdateZoomComposition()
    {
        if (_framing == null)
            return;

        float normalizedDistance = GetDistanceNormalized(_smoothDistance);
        float compositionT = normalizedDistance * normalizedDistance * (3f - 2f * normalizedDistance);
        float targetTrackedOffsetY = Mathf.Lerp(nearTrackedOffsetY, farTrackedOffsetY, compositionT);
        float targetScreenY = Mathf.Lerp(nearScreenY, farScreenY, compositionT);
        float lerpFactor = 1f - Mathf.Exp(-compositionSmoothSpeed * Time.deltaTime);

        _smoothTrackedOffsetY = Mathf.Lerp(_smoothTrackedOffsetY, targetTrackedOffsetY, lerpFactor);
        _smoothScreenY = Mathf.Lerp(_smoothScreenY, targetScreenY, lerpFactor);

        Vector3 offset = _baseTrackedObjectOffset;
        offset.y = _smoothTrackedOffsetY;
        _framing.m_TrackedObjectOffset = offset;
        _framing.m_ScreenY = _smoothScreenY;
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        _cursorLocked = true;
    }

    private void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        _cursorLocked = false;
    }

    private void UpdateShake()
    {
        if (_shakeTimer <= 0f)
        {
            transform.localRotation = _baseLocalRotation;
            _shakeAmplitude = 0f;
            _shakeFrequency = 0f;
            return;
        }

        _shakeTimer -= Time.deltaTime;

        float amplitude = Mathf.Min(maxShakeRotation, _shakeAmplitude);
        float noiseTime = Time.time * _shakeFrequency;
        float pitch = (Mathf.PerlinNoise(noiseTime, 0f) - 0.5f) * 2f * amplitude;
        float yaw = (Mathf.PerlinNoise(0f, noiseTime) - 0.5f) * 2f * amplitude;

        transform.localRotation = _baseLocalRotation * Quaternion.Euler(pitch, yaw, 0f);
    }

    private float GetCurrentDistance()
    {
        if (_follow3rd != null)
            return _follow3rd.CameraDistance;
        if (_framing != null)
            return _framing.m_CameraDistance;
        if (_transposer != null)
            return _transposer.m_FollowOffset.magnitude;
        return 5f;
    }

    private void SetDistance(float d)
    {
        if (_follow3rd != null)
            _follow3rd.CameraDistance = d;
        else if (_framing != null)
            _framing.m_CameraDistance = d;
        else if (_transposer != null)
        {
            var offset = _transposer.m_FollowOffset;
            if (offset.magnitude > 0.01f)
                _transposer.m_FollowOffset = offset.normalized * d;
        }
    }

    private float GetDistanceNormalized(float distance)
    {
        if (maxDistance <= minDistance)
            return 0f;

        return Mathf.InverseLerp(minDistance, maxDistance, distance);
    }

    private float ResolveZoomSmoothSpeed()
    {
        float baseline = Mathf.Max(1f, zoomSmoothSpeed);
        if (_targetDistance < _smoothDistance)
            return Mathf.Max(baseline, zoomInSmoothSpeed);

        if (_targetDistance > _smoothDistance)
            return Mathf.Max(baseline, zoomOutSmoothSpeed);

        return baseline;
    }

    private void CacheAxisNames()
    {
        AutoBindCameraInputReferences();
        if (pov == null)
            return;

        _cachedVerticalAxisName = pov.m_VerticalAxis.m_InputAxisName;
        _cachedHorizontalAxisName = pov.m_HorizontalAxis.m_InputAxisName;
    }

    private void ApplyGameplayInputState()
    {
        SynchronizeCursorState();
        ApplyRotationInputState(_gameplayInputEnabled);
    }

    private void ApplyRotationInputState(bool allowRotationInput)
    {
        AutoBindCameraInputReferences();

        if (inputProvider != null)
            inputProvider.enabled = allowRotationInput;

        if (pov == null)
            return;

        if (!allowRotationInput)
        {
            if (!_rotationInputSuspended)
            {
                _cachedVerticalAxisName = pov.m_VerticalAxis.m_InputAxisName;
                _cachedHorizontalAxisName = pov.m_HorizontalAxis.m_InputAxisName;
            }

            pov.m_VerticalAxis.m_InputAxisName = string.Empty;
            pov.m_HorizontalAxis.m_InputAxisName = string.Empty;
            pov.m_VerticalAxis.m_InputAxisValue = 0f;
            pov.m_HorizontalAxis.m_InputAxisValue = 0f;
            _rotationInputSuspended = true;
            return;
        }

        if (!_rotationInputSuspended)
            return;

        pov.m_VerticalAxis.m_InputAxisName = _cachedVerticalAxisName;
        pov.m_HorizontalAxis.m_InputAxisName = _cachedHorizontalAxisName;
        _rotationInputSuspended = false;
    }

    private void AutoBindCameraInputReferences()
    {
        if (pov == null)
        {
            pov = GetComponent<CinemachinePOV>();
            if (pov == null)
                pov = GetComponentInChildren<CinemachinePOV>(true);
        }

        if (inputProvider == null)
            inputProvider = GetComponent<CinemachineInputProvider>();
    }

    private void SynchronizeCursorState()
    {
        if (_gameplayInputEnabled)
        {
            if (!_cursorLocked || Cursor.lockState != CursorLockMode.Locked || Cursor.visible)
                LockCursor();
            return;
        }

        if (_cursorLocked || Cursor.lockState != CursorLockMode.None || !Cursor.visible)
            UnlockCursor();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
