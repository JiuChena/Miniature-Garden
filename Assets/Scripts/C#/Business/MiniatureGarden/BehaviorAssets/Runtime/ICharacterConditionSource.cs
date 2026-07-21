/// <summary>
/// 角色条件源接口。允许角色覆写默认条件实现。
/// </summary>
public interface ICharacterConditionSource
{
    CharacterConditions CreateConditions(CharacterContext context);
}
