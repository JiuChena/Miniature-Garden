/// <summary>
/// 角色资源容器，当前用于管理能量。
/// </summary>
public class CharacterResources : IUnitResourceSet
{
    public float Energy { get; private set; }
    public float MaxEnergy { get; private set; }
    public float CurrentEnergy => Energy;

    public CharacterResources(float maxEnergy)
    {
        MaxEnergy = maxEnergy;
        Energy = maxEnergy;
    }

    public bool TryConsume(float amount)
    {
        if (amount <= 0f) return true;
        if (Energy < amount) return false;

        Energy -= amount;
        return true;
    }

    public void Gain(float amount)
    {
        if (amount <= 0f) return;
        Energy = UnityEngine.Mathf.Min(MaxEnergy, Energy + amount);
    }

    public void Reset(float maxEnergy)
    {
        MaxEnergy = maxEnergy;
        Energy = maxEnergy;
    }

}
