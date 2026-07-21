using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 角色冷却容器。
/// </summary>
public class CharacterCooldowns
{
    private readonly Dictionary<string, float> _cooldowns = new Dictionary<string, float>();
    private readonly List<string> _expiredKeys = new List<string>();
    private readonly List<string> _keysBuffer = new List<string>();

    public void StartCD(string id, float duration)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        _cooldowns[id] = Mathf.Max(0f, duration);
    }

    public bool IsOnCD(string id)
    {
        return !string.IsNullOrWhiteSpace(id) &&
               _cooldowns.TryGetValue(id, out float timeLeft) &&
               timeLeft > 0f;
    }

    public bool TryGetRemaining(string id, out float timeLeft)
    {
        timeLeft = 0f;
        if (string.IsNullOrWhiteSpace(id))
            return false;

        if (!_cooldowns.TryGetValue(id, out float rawTimeLeft) || rawTimeLeft <= 0f)
            return false;

        timeLeft = rawTimeLeft;
        return true;
    }

    public void Tick(float deltaTime)
    {
        if (_cooldowns.Count == 0)
            return;

        _expiredKeys.Clear();
        _keysBuffer.Clear();

        foreach (KeyValuePair<string, float> pair in _cooldowns)
            _keysBuffer.Add(pair.Key);

        for (int i = 0; i < _keysBuffer.Count; i++)
        {
            string key = _keysBuffer[i];
            float nextValue = _cooldowns[key] - deltaTime;
            if (nextValue <= 0f)
                _expiredKeys.Add(key);
            else
                _cooldowns[key] = nextValue;
        }

        for (int i = 0; i < _expiredKeys.Count; i++)
            _cooldowns.Remove(_expiredKeys[i]);
    }
}
