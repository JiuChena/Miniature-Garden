using CoreFramework;
using BehaviorCore;
using UnityEngine;

/// <summary>
/// 角色翻越状态。执行期间由脚本驱动位移，不允许被其他普通行为打断。
/// </summary>
public sealed class VaultState : CharacterStateBase
{
    private const float GroundSnapProbeExtraHeight = 1.5f;
    private const float GroundSnapMaxDistance = 6f;
    private const float LandingBlendStartNormalized = 0.78f;
    private const float LandingBlendFullNormalized = 0.98f;

    protected override CharacterStateId StateId => CharacterStateId.Vault;

    private Vector3 _startPosition;
    private Vector3 _endPosition;
    private Vector3 _facingDirection;
    private float _arcHeight;
    private float _duration;
    private float _elapsed;
    private bool _controllerWasEnabled;
    private bool _pendingComplete;

    public VaultState(HSM hsm, CharacterContext context) : base(hsm, context) { }

    public override void OnEnter()
    {
        SetStance(CharacterStance.Standing);
        StopMovementImmediately();
        _pendingComplete = false;

        if (!TryInitializeVault())
        {
            Hsm.SwitchState<IdleState>(InterruptPriority.Normal);
            return;
        }

        Ctx.Interpreter.OnCompleted += OnCompleted;
        if (!RequestBehavior(BehaviorKeys.MoveJump))
            Hsm.SwitchState<IdleState>(InterruptPriority.Normal);
    }

    public override void OnUpdate()
    {
        TickRuntime();
        UpdateVaultMotion();

        if (_pendingComplete)
            Hsm.SwitchState<IdleState>(InterruptPriority.Normal);
    }

    public override void OnExit()
    {
        Ctx.Interpreter.OnCompleted -= OnCompleted;
        Vector3 resolvedEndPosition = ResolveGroundSnappedEndPosition();

        if (Ctx.Transform != null)
        {
            Ctx.Transform.position = resolvedEndPosition;
            if (_facingDirection.sqrMagnitude > 0.0001f)
            {
                Ctx.Transform.rotation =
                    Quaternion.LookRotation(_facingDirection.normalized, Vector3.up);
            }
        }

        _endPosition = resolvedEndPosition;
        RestoreController();

        Ctx.CurrentVaultRequest = default;
        Ctx.HasPendingVaultRequest = false;
        _duration = 0f;
        _elapsed = 0f;
        _pendingComplete = false;
    }

    private bool TryInitializeVault()
    {
        BehaviorClip vaultClip = GetBehavior(BehaviorKeys.MoveJump);
        if (vaultClip == null)
            return false;

        CharacterVaultRequest request;
        if (Ctx.HasPendingVaultRequest)
        {
            request = Ctx.CurrentVaultRequest;
            Ctx.HasPendingVaultRequest = false;
        }
        else if (Ctx.InteractionSource == null ||
                 !Ctx.InteractionSource.TryGetVaultRequest(Ctx, out request))
        {
            return false;
        }

        Ctx.CurrentVaultRequest = request;
        _startPosition = request.StartPosition;
        _endPosition = request.EndPosition;
        _facingDirection = request.FacingDirection.sqrMagnitude > 0.0001f
            ? request.FacingDirection.normalized
            : (Ctx.Transform != null ? Ctx.Transform.forward : Vector3.forward);
        _arcHeight = Mathf.Max(0f, request.ArcHeight);
        _duration = Mathf.Max(0.01f, vaultClip.totalDuration);
        _elapsed = 0f;

        if (Ctx.Transform != null)
            Ctx.Transform.position = _startPosition;

        if (Ctx.Controller != null)
        {
            _controllerWasEnabled = Ctx.Controller.enabled;
            if (_controllerWasEnabled)
                Ctx.Controller.enabled = false;
        }

        return true;
    }

    private void UpdateVaultMotion()
    {
        if (Ctx.Transform == null || _duration <= 0f)
            return;

        _elapsed = Mathf.Min(_elapsed + Ctx.DeltaTime, _duration);
        float normalizedTime = Mathf.Clamp01(_elapsed / _duration);
        Vector3 desiredPosition = Vector3.Lerp(_startPosition, _endPosition, normalizedTime);
        desiredPosition.y += 4f * _arcHeight * normalizedTime * (1f - normalizedTime);

        if (normalizedTime >= LandingBlendStartNormalized &&
            TryResolveGroundPosition(desiredPosition, out Vector3 groundedPosition))
        {
            float landingBlend = Mathf.InverseLerp(
                LandingBlendStartNormalized, LandingBlendFullNormalized, normalizedTime);
            landingBlend = landingBlend * landingBlend * (3f - 2f * landingBlend);
            if (groundedPosition.y < desiredPosition.y)
                desiredPosition.y = Mathf.Lerp(desiredPosition.y, groundedPosition.y, landingBlend);
        }

        Ctx.Transform.position = desiredPosition;

        if (_facingDirection.sqrMagnitude > 0.0001f)
        {
            Ctx.Transform.rotation =
                Quaternion.LookRotation(_facingDirection.normalized, Vector3.up);
        }
    }

    private void RestoreController()
    {
        if (Ctx.Controller == null || !_controllerWasEnabled)
            return;

        Ctx.Controller.enabled = true;
        Ctx.Controller.Move(Vector3.zero);
        _controllerWasEnabled = false;
    }

    private Vector3 ResolveGroundSnappedEndPosition()
    {
        Vector3 resolvedPosition = Ctx != null && Ctx.Transform != null ? Ctx.Transform.position : _endPosition;
        if (!TryResolveGroundPosition(resolvedPosition, out resolvedPosition))
            resolvedPosition = _endPosition;

        return resolvedPosition;
    }

    private bool TryResolveGroundPosition(Vector3 referencePosition, out Vector3 groundedPosition)
    {
        groundedPosition = referencePosition;
        if (Ctx == null || Ctx.Transform == null)
            return false;

        float probeHeight = GroundSnapProbeExtraHeight;
        if (Ctx.Controller != null)
            probeHeight = Mathf.Max(probeHeight, Ctx.Controller.height + Ctx.Controller.skinWidth);

        Vector3 rayOrigin = referencePosition + Vector3.up * probeHeight;
        float rayDistance = probeHeight + GroundSnapMaxDistance;
        if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit,
                rayDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        groundedPosition.y = hit.point.y;
        return true;
    }

    private void OnCompleted(BehaviorClip completedClip)
    {
        _pendingComplete = true;
    }
}
