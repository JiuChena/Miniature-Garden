using CoreFramework;
using BehaviorCore;
using UnityEngine;

/// <summary>
/// 瑙掕壊杩愯鏃朵笂涓嬫枃銆?/// </summary>
public class CharacterContext
{
    public IUnitRuntimeDefinition Config;
    public CharacterConditions Conditions;
    public CharacterCooldowns Cooldowns;
    public CharacterResources Resources;
    public BehaviorInterpreter Interpreter;
    public StatusData Data;
    public Animator Animator;
    public CharacterController Controller;
    public Transform Transform;
    public Blackboard Board;
    public IMovementStrategy MovementStrategy;
    public IUnitBehaviorRequester BehaviorRequester;
    public ICharacterTransitionPolicy TransitionPolicy;
    public ICharacterAttackResolver AttackResolver;
    public IUnitTargetingProvider UnitTargetingProvider;
    public ICharacterInteractionSource InteractionSource;
    public IUnitAbilityLevelProvider AbilityLevelProvider;
    public CharacterStance CurrentStance = CharacterStance.Standing;
    public bool HasQueuedStanceChangeAfterAttack;
    public CharacterStance QueuedStanceAfterAttack = CharacterStance.Standing;
    public CharacterVaultRequest CurrentVaultRequest;
    public bool HasPendingVaultRequest;
    public float DeltaTime;
    public float PendingBehaviorTransitionDuration = -1f;
    public string CurrentBehaviorKey = string.Empty;
    public string LastRequestedBehaviorKey = string.Empty;
    public string LastRequestedBehaviorClipName = string.Empty;
    public float LastAppliedTransitionDuration = -1f;
    public string LastAcceptedTransitionDescription = string.Empty;
    public string LastTransitionRejectReason = string.Empty;
    public bool LastTargetFacingApplied;
    public Vector3 LastTargetFacingDirection = UnityEngine.Vector3.zero;
    public bool EnableAutomaticProjectileFacing = true;
}
