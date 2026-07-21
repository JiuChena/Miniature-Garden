using CoreFramework;

/// <summary>
/// 一次角色状态切换请求，包含目标状态与过渡参数。
/// </summary>
public struct CharacterTransitionRequest
{
    public CharacterStateId NextState;
    public InterruptPriority Priority;
    public float CrossFadeDuration;
    public string TargetBehaviorKey;
    public bool BypassBehaviorInterruptGuard;
}
