/// <summary>
/// 单次攻击播放请求。
/// </summary>
public struct CharacterAttackPlayRequest
{
    public string BehaviorKey;
    public int ClipIndex;
    public CharacterAttackPlaybackStage PlaybackStage;
    public CharacterStance AttackStance;
}
