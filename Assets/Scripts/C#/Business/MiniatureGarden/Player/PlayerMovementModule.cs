using CoreFramework;
using UnityEngine;

/// <summary>
/// 玩家移动模块。
/// 负责持有 Player 根节点上的 CharacterController，执行地面移动，并把当前受控角色位姿同步到 Player 根节点。
/// </summary>
[DefaultExecutionOrder(500)]
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public sealed class PlayerMovementModule : MonoBehaviour, IPlayerModule, IMovementStrategy
{
    [Header("Movement")]
    [SerializeField, Tooltip("最大移动速度（米/秒）")]
    private float speed = 6f;

    [SerializeField, Tooltip("加速度（米/秒²）")]
    private float acceleration = 30f;

    [SerializeField, Tooltip("减速度（米/秒²），松开按键时使用")]
    private float deceleration = 40f;

    [SerializeField, Tooltip("旋转速度（度/秒）")]
    private float rotationSpeed = 720f;

    [SerializeField, Tooltip("下坡吸附力基础值，正值向下")]
    private float slopeStickForce = 5f;

    [SerializeField, Tooltip("吸附力随移速增长的曲线程度。1=线性, >1=高速时更强, <1=低速时也显著")]
    [Range(0.1f, 3f)]
    private float slopeStickExponent = 1f;

    [Header("Sync")]
    [SerializeField, Tooltip("启用后让当前受控角色的位置始终对齐到 Player 根节点。")]
    private bool syncPosition = true;

    [SerializeField, Tooltip("启用后让当前受控角色的朝向始终对齐到 Player 根节点。")]
    private bool syncRotation = true;

    private PlayerController _owner;
    private CharacterController _controller;
    private Transform _cameraTransform;
    private Vector3 _currentVelocity;

    public CharacterController Controller => _controller;
    public IMovementStrategy MovementStrategy => this;

    public void Initialize(PlayerController owner, PlayerContext context)
    {
        _owner = owner;
        CacheBindings();
    }

    public void Enable()
    {
        CacheBindings();
    }

    public void Disable()
    {
        StopMovementImmediately();
    }

    public void Tick(Blackboard board, float deltaTime)
    {
    }

    private void LateUpdate()
    {
        if (_owner == null)
            return;

        CharacterDriver currentCharacter = _owner.CurrentCharacter;
        if (currentCharacter == null)
            return;

        SyncControlledCharacterToPlayerRoot(currentCharacter);

        if (!currentCharacter.IsPlayerControlled)
        {
            StopMovementImmediately();
            return;
        }

        if (currentCharacter.IsInMoveState)
        {
            Execute(_owner.Board, _controller);
            return;
        }

        StopMovementImmediately();
    }

    public void StopMovementImmediately()
    {
        _currentVelocity = Vector3.zero;
    }

    public void ApplyUnitConfig(IUnitDefinition config)
    {
        if (config == null)
            return;

        speed = Mathf.Max(0f, config.MoveSpeed);
    }

    public void ApplyCharacterControllerTemplate(CharacterController templateController)
    {
        if (_controller == null || templateController == null || ReferenceEquals(_controller, templateController))
            return;

        bool wasEnabled = _controller.enabled;
        if (wasEnabled)
            _controller.enabled = false;

        _controller.height = templateController.height;
        _controller.radius = templateController.radius;
        _controller.center = templateController.center;
        _controller.slopeLimit = templateController.slopeLimit;
        _controller.stepOffset = templateController.stepOffset;
        _controller.skinWidth = templateController.skinWidth;
        _controller.minMoveDistance = templateController.minMoveDistance;
        _controller.detectCollisions = templateController.detectCollisions;

        if (wasEnabled)
            _controller.enabled = true;
    }

    public void SetRootPose(Vector3 position, Quaternion rotation)
    {
        if (_controller != null && _controller.enabled)
        {
            _controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            _controller.enabled = true;
            return;
        }

        transform.SetPositionAndRotation(position, rotation);
    }

    public void SyncControlledCharacterToPlayerRoot()
    {
        if (_owner == null)
            return;

        SyncControlledCharacterToPlayerRoot(_owner.CurrentCharacter);
    }

    public void Execute(Blackboard board, CharacterController cc)
    {
        if (cc == null)
            return;

        if (_cameraTransform == null)
        {
            Camera cam = Camera.main;
            if (cam != null)
                _cameraTransform = cam.transform;
        }

        Vector2 raw = board != null ? board.MoveInput : Vector2.zero;
        Vector3 worldDir;

        if (_cameraTransform != null)
        {
            Vector3 forward = _cameraTransform.forward;
            Vector3 right = _cameraTransform.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();
            worldDir = forward * raw.y + right * raw.x;
        }
        else
        {
            worldDir = new Vector3(raw.x, 0f, raw.y);
        }

        if (worldDir.sqrMagnitude > 0.01f)
        {
            Vector3 targetVelocity = worldDir.normalized * speed;
            _currentVelocity = Vector3.MoveTowards(_currentVelocity, targetVelocity, acceleration * Time.deltaTime);

            Quaternion target = Quaternion.LookRotation(worldDir);
            cc.transform.rotation = Quaternion.RotateTowards(cc.transform.rotation, target, rotationSpeed * Time.deltaTime);
        }
        else
        {
            _currentVelocity = Vector3.MoveTowards(_currentVelocity, Vector3.zero, deceleration * Time.deltaTime);
        }

        cc.SimpleMove(_currentVelocity);
        ApplySlopeDownforce(cc);
    }

    private void CacheBindings()
    {
        if (_controller == null)
            _controller = GetComponent<CharacterController>();
    }

    private void SyncControlledCharacterToPlayerRoot(CharacterDriver currentCharacter)
    {
        if (currentCharacter == null)
            return;

        Transform ownerTransform = transform;
        Transform currentTransform = currentCharacter.transform;

        Vector3 position = syncPosition ? ownerTransform.position : currentTransform.position;
        Quaternion rotation = syncRotation ? ownerTransform.rotation : currentTransform.rotation;

        CharacterController controller = currentCharacter.LocalCharacterController;
        bool restoreController = controller != null && controller.enabled;
        if (restoreController)
            controller.enabled = false;

        currentTransform.SetPositionAndRotation(position, rotation);

        if (restoreController)
            controller.enabled = true;
    }

    private void ApplySlopeDownforce(CharacterController cc)
    {
        Vector3 origin = cc.transform.position + cc.center;
        float distance = cc.height * 0.5f + 0.3f;

        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, distance))
            return;

        float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
        if (slopeAngle < 0.1f)
            return;

        float speedRatio = speed > 0.0001f ? Mathf.Clamp01(_currentVelocity.magnitude / speed) : 0f;
        float speedFactor = Mathf.Pow(speedRatio, slopeStickExponent);
        cc.Move(Vector3.down * (slopeStickForce * speedFactor * Time.deltaTime));
    }
}
