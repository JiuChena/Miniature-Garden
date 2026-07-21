#if MINIATURE_GARDEN_INCLUDE_PLAYMODE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class EnemyTargetingPlayModeTests
{
    private readonly List<GameObject> _runtimeObjects = new List<GameObject>(8);

    [TearDown]
    public void TearDown()
    {
        for (int i = _runtimeObjects.Count - 1; i >= 0; i--)
        {
            if (_runtimeObjects[i] != null)
                Object.DestroyImmediate(_runtimeObjects[i]);
        }

        _runtimeObjects.Clear();
    }

    [Test]
    public void TryResolveCombatTarget_PrefersFrontFacingTarget()
    {
        StatusData owner = CreateStatusData("Owner", new Vector3(0f, 0f, 0f), UnitAlignment.Enemy);
        owner.transform.forward = Vector3.forward;
        owner.gameObject.AddComponent<SphereCollider>().isTrigger = true;

        NonPlayerTargetingModule targetingModule = owner.gameObject.AddComponent<NonPlayerTargetingModule>();

        StatusData frontTarget = CreateStatusData("FrontTarget", new Vector3(0f, 0f, 4f), UnitAlignment.Friendly);
        frontTarget.gameObject.AddComponent<SphereCollider>().isTrigger = true;

        StatusData backTarget = CreateStatusData("BackTarget", new Vector3(0f, 0f, -4f), UnitAlignment.Friendly);
        backTarget.gameObject.AddComponent<SphereCollider>().isTrigger = true;

        Physics.SyncTransforms();

        bool resolved = targetingModule.TryResolveCombatTarget(
            owner, owner.transform.position, owner.transform.forward, out NonPlayerTargetingResult result);

        Assert.That(resolved, Is.True);
        Assert.That(result.targetData, Is.EqualTo(frontTarget));
        Assert.That(result.targetTransform, Is.EqualTo(frontTarget.transform));
    }

    [Test]
    public void TryResolveCombatTarget_RejectsSameAlignmentTarget()
    {
        StatusData owner = CreateStatusData("Owner", new Vector3(0f, 0f, 0f), UnitAlignment.Enemy);
        owner.transform.forward = Vector3.forward;
        owner.gameObject.AddComponent<SphereCollider>().isTrigger = true;

        NonPlayerTargetingModule targetingModule = owner.gameObject.AddComponent<NonPlayerTargetingModule>();

        StatusData sameAlignmentTarget = CreateStatusData("SameAlignmentTarget", new Vector3(0f, 0f, 4f), UnitAlignment.Enemy);
        sameAlignmentTarget.gameObject.AddComponent<SphereCollider>().isTrigger = true;

        Physics.SyncTransforms();

        bool resolved = targetingModule.TryResolveCombatTarget(
            owner, owner.transform.position, owner.transform.forward, out _);

        Assert.That(resolved, Is.False);
    }

    private StatusData CreateStatusData(string name, Vector3 position, UnitAlignment alignment)
    {
        GameObject gameObject = new GameObject(name);
        gameObject.transform.position = position;
        gameObject.layer = 0;
        _runtimeObjects.Add(gameObject);

        StatusData statusData = gameObject.AddComponent<StatusData>();
        SetPrivateField(statusData, "fallbackUnitAlignment", alignment);
        statusData.RefreshFromDriver(false, true);
        return statusData;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        System.Reflection.FieldInfo field = target.GetType().GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing field: {fieldName}");
        field.SetValue(target, value);
    }
}
#endif
