using CoreFramework;
using UnityEngine;

/// <summary>
/// 玩家运行时共享上下文。
/// </summary>
public sealed class PlayerContext
{
    public Transform Transform;
    public Blackboard Board;
    public CharacterController Controller;
    public IMovementStrategy MovementStrategy;
    public ICharacterInteractionSource InteractionSource;
}
