using UnityEngine;

/// <summary>
/// CharacterDriver 内部数据运行时。
/// 负责角色等级数据读取、数值解析与状态快照组装。
/// </summary>
internal sealed class CharacterDataRuntime : ICharacterModule
{
    private readonly CharacterDriver _owner;
    private CharacterContext _context;

    public CharacterDataRuntime(CharacterDriver owner)
    {
        _owner = owner;
    }

    public void Initialize(CharacterDriver owner, CharacterContext context)
    {
        _context = context;
    }

    public void OnOwnerEnabled()
    {
        CharacterDataManager.Instance.CharacterDataChanged += HandleCharacterDataChanged;
    }

    public void OnOwnerDisabled()
    {
        CharacterDataManager.Instance.CharacterDataChanged -= HandleCharacterDataChanged;
    }

    public void Tick(CoreFramework.Blackboard board, float deltaTime)
    {
    }

    public void LateTick(CoreFramework.Blackboard board, float deltaTime)
    {
    }

    public void Dispose()
    {
        _context = null;
    }

    public CharacterData GetRuntimeCharacterData()
    {
        if (_context == null || _context.Config == null)
            return new CharacterData();

        return CharacterDataManager.Instance.GetCharacterDataOrDefault(_context.Config.UnitId);
    }

    public void RefreshRuntimeCharacterData(bool preserveHealthRatio)
    {
        if (_owner == null || _context == null || _context.Config == null || _owner.DataPanel == null)
            return;

        _owner.RefreshStatusData(preserveHealthRatio, false);
        _owner.RequestDebugRefresh(true);
    }

    public int GetAbilityLevel(UnitAbilityLevelGroup levelGroup)
    {
        if (_context == null || _context.Config == null)
            return 1;

        CharacterData runtimeData = GetRuntimeCharacterData();
        switch (levelGroup)
        {
            case UnitAbilityLevelGroup.NormalAttack:
                return Mathf.Max(1, runtimeData.attackLevel);
            case UnitAbilityLevelGroup.Talent:
                return Mathf.Max(1, runtimeData.talentLevel);
            case UnitAbilityLevelGroup.Burst:
                return Mathf.Max(1, runtimeData.burstLevel);
            default:
                return 1;
        }
    }

    public bool TryResolveNumericValue(string numericKey, out float value)
    {
        value = 0f;
        if (_context == null || _context.Config == null || string.IsNullOrWhiteSpace(numericKey))
            return false;

        return _context.Config.TryResolveNumericValue(numericKey, _owner, out value);
    }

    public bool TryBuildStatusSnapshot(out StatusDataSnapshot snapshot)
    {
        snapshot = default;
        if (_context == null || _context.Config == null)
            return false;

        CharacterData runtimeCharacterData = GetRuntimeCharacterData();
        int characterLevel = Mathf.Max(1, runtimeCharacterData.characterLevel);
        snapshot.hasFullStatus = true;
        snapshot.unitId = _context.Config.UnitId;
        snapshot.unitAlignment = _context.Config.UnitAlignment;
        snapshot.unitLevel = characterLevel;
        snapshot.baseHealth = _context.Config.ResolveBaseHealth(characterLevel);
        snapshot.baseAttackPower = _context.Config.ResolveBaseAttack(characterLevel);
        snapshot.baseDefense = _context.Config.ResolveBaseDefense(characterLevel);
        snapshot.baseCritRate = _context.Config.BaseCritRate;
        snapshot.baseCritDamage = _context.Config.BaseCritDamage;
        snapshot.baseDamageBonus = _context.Config.BaseDamageBonus;
        snapshot.basePenetration = _context.Config.BasePenetration;
        return true;
    }

    private void HandleCharacterDataChanged(int characterId, CharacterData data)
    {
        if (_context == null || _context.Config == null || _context.Config.UnitId != characterId)
            return;

        RefreshRuntimeCharacterData(true);
    }
}
