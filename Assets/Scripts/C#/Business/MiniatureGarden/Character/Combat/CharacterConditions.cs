/// <summary>
/// Character behavior condition checks.
/// </summary>
public class CharacterConditions
{
    private const string DeadReason = "角色已死亡，不能执行该行为。";
    private const string MissingConfigReason = "角色缺少运行时定义配置。";

    protected readonly CharacterContext Ctx;

    public CharacterConditions(CharacterContext context)
    {
        Ctx = context;
    }

    public virtual bool CanMove()
    {
        return CanMove(out _);
    }

    public virtual bool CanAttack()
    {
        return CanAttack(out _);
    }

    public virtual bool CanMove(out string reason)
    {
        reason = string.Empty;
        if (!HasRuntimeData(out reason))
            return false;

        if (Ctx.Config.MoveSpeed <= 0f)
        {
            reason = "角色移动速度小于等于 0，不能进入移动行为。";
            return false;
        }

        return true;
    }

    public virtual bool CanAttack(out string reason)
    {
        reason = string.Empty;
        if (!HasRuntimeData(out reason))
            return false;

        if (!Ctx.Config.SupportsAttack)
        {
            reason = "角色模板未启用普通攻击能力。";
            return false;
        }

        if (Ctx.CurrentStance == CharacterStance.Crouching)
        {
            if (!Ctx.Config.HasBehavior(BehaviorKeys.CrouchAttackStart) &&
                !Ctx.Config.HasBehavior(BehaviorKeys.CrouchAttackLoop))
            {
                reason = "当前处于蹲下姿态，但角色未配置蹲下攻击行为。";
                return false;
            }

            return true;
        }

        if (!Ctx.Config.HasBehavior(BehaviorKeys.AttackStart) &&
            !Ctx.Config.HasBehavior(BehaviorKeys.AttackLoop) &&
            !Ctx.Config.HasBehavior(BehaviorKeys.Attack))
        {
            reason =
                $"角色行为表缺少可执行的普通攻击行为。请至少配置 '{BehaviorKeys.AttackStart}'、'{BehaviorKeys.AttackLoop}' 或 '{BehaviorKeys.Attack}'。";
            return false;
        }

        return true;
    }

    public virtual bool CanBurst(out string reason)
    {
        reason = string.Empty;
        if (!HasRuntimeData(out reason))
            return false;

        if (!Ctx.Config.SupportsBurst)
        {
            reason = "角色模板未启用爆发技能能力。";
            return false;
        }

        if (!Ctx.Config.HasBehavior(BehaviorKeys.Burst))
        {
            reason = $"角色行为表缺少核心行为 key：{BehaviorKeys.Burst}。";
            return false;
        }

        if (Ctx.Cooldowns != null && Ctx.Cooldowns.TryGetRemaining("Burst", out float burstCooldown))
        {
            reason = $"爆发技能仍在冷却中，剩余 {burstCooldown:F2} 秒。";
            return false;
        }

        if (Ctx.Resources == null)
        {
            reason = "角色资源模块为空，无法校验爆发能量。";
            return false;
        }

        if (Ctx.Resources.Energy < Ctx.Config.BurstCost)
        {
            reason = $"爆发能量不足，当前 {Ctx.Resources.Energy:F1} / 需要 {Ctx.Config.BurstCost:F1}。";
            return false;
        }

        return true;
    }

    public virtual bool CanTalent(out string reason)
    {
        reason = string.Empty;
        if (!HasRuntimeData(out reason))
            return false;

        if (!Ctx.Config.SupportsTalent)
        {
            reason = "角色模板未启用天赋技能能力。";
            return false;
        }

        if (!Ctx.Config.HasBehavior(BehaviorKeys.Talent))
        {
            reason = $"角色行为表缺少核心行为 key：{BehaviorKeys.Talent}。";
            return false;
        }

        if (Ctx.Cooldowns != null && Ctx.Cooldowns.TryGetRemaining("Talent", out float talentCooldown))
        {
            reason = $"天赋技能仍在冷却中，剩余 {talentCooldown:F2} 秒。";
            return false;
        }

        return true;
    }

    public virtual bool CanReload(out string reason)
    {
        reason = string.Empty;
        if (!HasRuntimeData(out reason))
            return false;

        if (!Ctx.Config.SupportsReload)
        {
            reason = "角色模板未启用装填能力。";
            return false;
        }

        if (Ctx.CurrentStance == CharacterStance.Crouching)
        {
            if (!Ctx.Config.HasBehavior(BehaviorKeys.CrouchReload) &&
                !Ctx.Config.HasBehavior(BehaviorKeys.Reload))
            {
                reason =
                    $"角色当前处于蹲下姿态，但行为表缺少可执行的装填行为。请配置 '{BehaviorKeys.CrouchReload}' 或回退使用 '{BehaviorKeys.Reload}'。";
                return false;
            }

            return true;
        }

        if (!Ctx.Config.HasBehavior(BehaviorKeys.Reload))
        {
            reason = $"角色行为表缺少核心行为 key：{BehaviorKeys.Reload}。";
            return false;
        }

        return true;
    }

    public virtual bool CanEnterCrouch(out string reason)
    {
        reason = string.Empty;
        if (!HasRuntimeData(out reason))
            return false;

        if (Ctx.CurrentStance == CharacterStance.Crouching)
        {
            reason = "角色当前已经处于蹲下姿态。";
            return false;
        }

        if (!Ctx.Config.SupportsCrouch)
        {
            reason = "角色模板未启用蹲下能力。";
            return false;
        }

        if (!Ctx.Config.HasBehavior(BehaviorKeys.CrouchIdle))
        {
            reason = $"角色行为表缺少核心行为 key：{BehaviorKeys.CrouchIdle}。";
            return false;
        }

        if (Ctx.InteractionSource == null || !Ctx.InteractionSource.IsInCoverInteractionRange(Ctx))
        {
            reason = "当前不在可蹲下的掩体交互范围内。";
            return false;
        }

        return true;
    }

    public virtual bool CanStandUp(out string reason)
    {
        reason = string.Empty;
        if (!HasRuntimeData(out reason))
            return false;

        return true;
    }

    public virtual bool CanVault(out string reason)
    {
        reason = string.Empty;
        if (!HasRuntimeData(out reason))
            return false;

        if (!Ctx.Config.SupportsJump)
        {
            reason = "角色模板未启用翻越能力。";
            return false;
        }

        if (!Ctx.Config.HasBehavior(BehaviorKeys.MoveJump))
        {
            reason = $"角色行为表缺少核心行为 key：{BehaviorKeys.MoveJump}。";
            return false;
        }

        if (Ctx.InteractionSource == null)
        {
            reason = "当前没有可用的翻越交互来源。";
            return false;
        }

        if (Ctx.HasPendingVaultRequest)
            return true;

        if (!Ctx.InteractionSource.TryGetVaultRequest(Ctx, out _))
        {
            reason = "当前不在可翻越障碍的交互范围内。";
            return false;
        }

        return true;
    }

    private bool HasRuntimeData(out string reason)
    {
        reason = string.Empty;
        if (Ctx == null || Ctx.Data == null)
        {
            reason = "角色运行时数据尚未初始化。";
            return false;
        }

        if (Ctx.Data.IsDead)
        {
            reason = DeadReason;
            return false;
        }

        if (Ctx.Config == null)
        {
            reason = MissingConfigReason;
            return false;
        }

        return true;
    }
}
