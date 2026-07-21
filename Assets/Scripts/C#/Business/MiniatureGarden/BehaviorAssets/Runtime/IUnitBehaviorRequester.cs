using BehaviorCore;
using UnityEngine.Scripting.APIUpdating;

/// <summary>
/// Runtime entry point for requesting unit behaviors.
/// </summary>
[MovedFrom(false, null, null, "ICharacterBehaviorRequester")]
public interface IUnitBehaviorRequester
{
    bool RequestBehavior(BehaviorClip clip);
    bool RequestBehavior(string key, int clipIndex = 0);
    BehaviorClip GetBehavior(string key, int clipIndex = 0);
    BehaviorClip[] GetBehaviorGroup(string key);
}
