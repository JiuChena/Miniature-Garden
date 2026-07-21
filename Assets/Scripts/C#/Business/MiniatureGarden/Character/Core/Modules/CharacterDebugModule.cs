using CoreFramework;
using UnityEngine;

/// <summary>
/// 角色调试快照。
/// </summary>
internal struct CharacterDebugSnapshot
{
    public string characterName;
    public bool isPlayerControlled;
    public CharacterStance currentStance;
    public string currentStateName;
    public string currentBehaviorKey;
    public string currentBehaviorClipName;
    public float behaviorElapsedTime;
    public float behaviorNormalizedTime;
    public float currentEnergy;
    public float talentCooldownRemaining;
    public float burstCooldownRemaining;
    public float pendingBehaviorTransitionDuration;
    public string lastAcceptedTransition;
    public string lastTransitionRejectReason;
    public bool lastTargetFacingApplied;
    public Vector3 lastTargetFacingDirection;
    public bool canUseCover;
    public bool canVault;
    public Vector2 moveInput;
    public bool canMove;
    public string canMoveReason;
    public string movementStrategyName;
}

/// <summary>
/// 角色调试模块：负责当前受控角色的调试快照采集与屏幕绘制。
/// </summary>
[DisallowMultipleComponent]
public sealed class CharacterDebugModule : CharacterModuleBase
{
    private static GUIStyle s_labelStyle;
    private static GUIStyle s_textAreaStyle;

    [Header("Runtime Debug")]
    [SerializeField, Tooltip("是否绘制当前受控角色的运行时屏幕调试信息")]
    private bool overlayEnabled = true;

    [SerializeField, Tooltip("运行时调试信息的最小刷新间隔。0 表示每帧刷新；适当提高可降低调试刷新开销。")]
    [Min(0f)]
    private float refreshInterval = 0.1f;

    private CharacterDebugSnapshot _snapshot;
    private float _nextRefreshTime;

    public bool IsOverlayEnabled => overlayEnabled;

    public override void LateTick(Blackboard board, float deltaTime)
    {
        Capture(false);
    }

    public void Capture(bool force)
    {
        if (Owner == null || !ShouldDrawDebugOverlay())
            return;

        if (!force && !ShouldCapture())
            return;

        _snapshot = BuildSnapshot();
    }

    public void DrawOverlay()
    {
        if (Owner == null || !ShouldDrawDebugOverlay())
            return;

        Capture(true);

        EnsureStyles();

        Rect area = new Rect(16f, 16f, 860f, 420f);
        GUILayout.BeginArea(area, GUI.skin.box);
        GUILayout.Label($"Character: {_snapshot.characterName}", s_labelStyle);
        GUILayout.Label($"State: {_snapshot.currentStateName}", s_labelStyle);
        GUILayout.Label($"BehaviorKey: {_snapshot.currentBehaviorKey}", s_labelStyle);
        GUILayout.Label($"Clip: {_snapshot.currentBehaviorClipName}", s_labelStyle);
        GUILayout.Label($"Controlled: {_snapshot.isPlayerControlled}  Stance: {_snapshot.currentStance}", s_labelStyle);
        GUILayout.Label($"MoveInput: {_snapshot.moveInput}", s_labelStyle);
        GUILayout.Label($"MovementStrategy: {_snapshot.movementStrategyName}", s_labelStyle);
        GUILayout.Label($"CanMove: {_snapshot.canMove}", s_labelStyle);
        GUILayout.Label($"Behavior Time: {_snapshot.behaviorElapsedTime:F2}s ({_snapshot.behaviorNormalizedTime:P0})", s_labelStyle);
        GUILayout.Label($"Energy: {_snapshot.currentEnergy:F1}", s_labelStyle);
        GUILayout.Label($"Talent CD: {_snapshot.talentCooldownRemaining:F2}s", s_labelStyle);
        GUILayout.Label($"Burst CD: {_snapshot.burstCooldownRemaining:F2}s", s_labelStyle);
        GUILayout.Label($"Pending Transition: {_snapshot.pendingBehaviorTransitionDuration:F2}", s_labelStyle);
        GUILayout.Label($"Facing Applied: {_snapshot.lastTargetFacingApplied}  Dir: {_snapshot.lastTargetFacingDirection}", s_labelStyle);
        GUILayout.Label($"Cover: {_snapshot.canUseCover}  Vault: {_snapshot.canVault}", s_labelStyle);
        if (!string.IsNullOrWhiteSpace(_snapshot.canMoveReason))
            GUILayout.TextArea($"CanMove Reject: {_snapshot.canMoveReason}", s_textAreaStyle);
        if (!string.IsNullOrWhiteSpace(_snapshot.lastAcceptedTransition))
            GUILayout.TextArea($"Accepted: {_snapshot.lastAcceptedTransition}", s_textAreaStyle);
        if (!string.IsNullOrWhiteSpace(_snapshot.lastTransitionRejectReason))
            GUILayout.TextArea($"Rejected: {_snapshot.lastTransitionRejectReason}", s_textAreaStyle);
        GUILayout.EndArea();
    }

    private static void EnsureStyles()
    {
        if (s_labelStyle == null)
        {
            s_labelStyle = new GUIStyle(GUI.skin.label)
            {
                wordWrap = true,
                richText = false,
            };
        }

        if (s_textAreaStyle == null)
        {
            s_textAreaStyle = new GUIStyle(GUI.skin.textArea)
            {
                wordWrap = true,
                richText = false,
                stretchHeight = true,
            };
        }
    }

    private bool ShouldCapture()
    {
        if (!Application.isPlaying || refreshInterval <= 0f)
            return true;

        if (Time.unscaledTime < _nextRefreshTime)
            return false;

        _nextRefreshTime = Time.unscaledTime + Mathf.Max(0f, refreshInterval);
        return true;
    }

    private bool ShouldDrawDebugOverlay()
    {
        return Application.isPlaying &&
               Owner != null &&
               overlayEnabled &&
               Owner.IsPlayerControlled;
    }

    private CharacterDebugSnapshot BuildSnapshot()
    {
        CharacterDebugSnapshot snapshot = default;
        snapshot.characterName = Owner != null ? Owner.name : string.Empty;
        snapshot.isPlayerControlled = Owner != null && Owner.IsPlayerControlled;
        snapshot.currentStance = Context != null ? Context.CurrentStance : CharacterStance.Standing;
        snapshot.currentStateName = Owner != null ? Owner.GetCurrentStateName() : string.Empty;
        snapshot.currentBehaviorKey = Context != null ? Context.CurrentBehaviorKey : string.Empty;
        snapshot.currentBehaviorClipName = Context != null && Context.Interpreter != null && Context.Interpreter.CurrentClip != null
            ? Context.Interpreter.CurrentClip.name
            : string.Empty;
        snapshot.behaviorElapsedTime = Context != null && Context.Interpreter != null ? Context.Interpreter.ElapsedTime : 0f;
        snapshot.behaviorNormalizedTime = Context != null && Context.Interpreter != null ? Context.Interpreter.NormalizedTime : 0f;
        snapshot.currentEnergy = Context != null && Context.Resources != null ? Context.Resources.Energy : 0f;
        snapshot.talentCooldownRemaining = Context != null && Context.Cooldowns != null &&
                                           Context.Cooldowns.TryGetRemaining("Talent", out float talentCooldown)
            ? talentCooldown
            : 0f;
        snapshot.burstCooldownRemaining = Context != null && Context.Cooldowns != null &&
                                          Context.Cooldowns.TryGetRemaining("Burst", out float burstCooldown)
            ? burstCooldown
            : 0f;
        snapshot.pendingBehaviorTransitionDuration = Context != null ? Context.PendingBehaviorTransitionDuration : -1f;
        snapshot.lastAcceptedTransition = Context != null ? Context.LastAcceptedTransitionDescription : string.Empty;
        snapshot.lastTransitionRejectReason = Context != null ? Context.LastTransitionRejectReason : string.Empty;
        snapshot.lastTargetFacingApplied = Context != null && Context.LastTargetFacingApplied;
        snapshot.lastTargetFacingDirection = Context != null ? Context.LastTargetFacingDirection : Vector3.zero;
        snapshot.canUseCover = Context != null &&
                               Context.InteractionSource != null &&
                               Context.InteractionSource.IsInCoverInteractionRange(Context);
        snapshot.canVault = Context != null &&
                            Context.InteractionSource != null &&
                            Context.InteractionSource.TryGetVaultRequest(Context, out _);
        snapshot.moveInput = Context != null && Context.Board != null ? Context.Board.MoveInput : Vector2.zero;
        snapshot.movementStrategyName = Context != null && Context.MovementStrategy != null
            ? Context.MovementStrategy.GetType().Name
            : "<None>";
        string canMoveReason = string.Empty;
        snapshot.canMove = Context != null && Context.Conditions != null && Context.Conditions.CanMove(out canMoveReason);
        snapshot.canMoveReason = snapshot.canMove ? string.Empty : canMoveReason;
        return snapshot;
    }
}
