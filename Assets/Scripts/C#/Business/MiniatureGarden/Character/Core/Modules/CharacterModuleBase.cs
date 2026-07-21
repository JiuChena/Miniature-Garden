using CoreFramework;
using UnityEngine;

/// <summary>
/// CharacterDriver 模块空实现基类。
/// </summary>
public abstract class CharacterModuleBase : MonoBehaviour, ICharacterModule
{
    protected CharacterDriver Owner { get; private set; }
    protected CharacterContext Context { get; private set; }

    public virtual void Initialize(CharacterDriver owner, CharacterContext context)
    {
        Owner = owner;
        Context = context;
    }

    public virtual void OnOwnerEnabled()
    {
    }

    public virtual void OnOwnerDisabled()
    {
    }

    public virtual void Tick(Blackboard board, float deltaTime)
    {
    }

    public virtual void LateTick(Blackboard board, float deltaTime)
    {
    }

    public virtual void Dispose()
    {
    }
}
