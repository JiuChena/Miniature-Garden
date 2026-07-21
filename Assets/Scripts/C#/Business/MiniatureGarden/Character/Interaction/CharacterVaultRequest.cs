using UnityEngine;

/// <summary>
/// 一次翻越请求，包含表演行为执行期间需要的起点、终点和朝向数据。
/// </summary>
public struct CharacterVaultRequest
{
    public Vector3 StartPosition;
    public Vector3 EndPosition;
    public Vector3 FacingDirection;
    public float ArcHeight;
}
