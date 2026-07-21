#if MINIATURE_GARDEN_INCLUDE_PLAYMODE_TESTS
using NUnit.Framework;
using UnityEngine;
using CoreFramework;
using System.Collections.Generic;

public sealed class EnemyAiRuntimePlayModeTests
{
    private readonly List<Object> _runtimeObjects = new List<Object>(16);

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
    public void TryFaceProjectileTarget_DoesNothingWhenAutomaticFacingDisabled()
    {
        GameObject ownerObject = new GameObject("EnemyOwner");
        GameObject targetObject = new GameObject("AimTarget");
        try
        {
            ownerObject.transform.forward = Vector3.forward;
            targetObject.transform.position = new Vector3(5f, 0f, 0f);

            StatusData ownerStatus = ownerObject.AddComponent<StatusData>();
            ownerStatus.RefreshFromDriver(false, true);
            ownerStatus.SetUnitTargetingProvider(new StaticTargetingProvider(targetObject.transform.position));

            CharacterContext context = new CharacterContext
            {
                Data = ownerStatus,
                Transform = ownerObject.transform,
                EnableAutomaticProjectileFacing = false,
            };

            TestFacingState state = new TestFacingState(context);
            Quaternion beforeRotation = ownerObject.transform.rotation;

            state.InvokeTryFaceProjectileTarget();

            Assert.That(ownerObject.transform.rotation, Is.EqualTo(beforeRotation));
            Assert.That(context.LastTargetFacingApplied, Is.False);
            Assert.That(context.LastTargetFacingDirection, Is.EqualTo(Vector3.forward));
        }
        finally
        {
            Object.DestroyImmediate(ownerObject);
            Object.DestroyImmediate(targetObject);
        }
    }

    [Test]
    public void Tick_ClearsOutOfRangeTargetAfterDelay_AndReturnsHome()
    {
        EnemyBrainModule brain = CreateEnemyBrain(out EnemyDriver owner, out _);
        GameObject targetObject = CreateGameObject("FarTarget", new Vector3(30f, 0f, 0f));
        StatusData targetStatus = CreateStatusData(targetObject, UnitAlignment.Friendly);

        owner.transform.position = new Vector3(3f, 0f, 0f);
        SetCurrentTarget(brain, new NonPlayerTargetingResult
        {
            targetData = targetStatus,
            targetTransform = targetStatus.transform,
            targetCollider = null,
            aimPoint = targetStatus.transform.position,
            launchDirection = Vector3.right,
            sqrDistance = 900f,
        });

        Blackboard board = new Blackboard();
        brain.Tick(board, 0.6f);
        Assert.That(brain.HasTarget, Is.True, "第一次 Tick 只应累计丢失计时，不应立刻丢目标。");

        board.ClearAllData();
        brain.Tick(board, 0.6f);

        Assert.That(brain.HasTarget, Is.False);
        Assert.That(brain.WantsMove, Is.True);
        Assert.That(brain.DesiredMoveDestination, Is.EqualTo(Vector3.zero));
    }

    [Test]
    public void Tick_StartsAttackWithSingleFacingStep_BeforeFiring()
    {
        EnemyBrainModule brain = CreateEnemyBrain(out EnemyDriver owner, out _);
        GameObject targetObject = CreateGameObject("CloseTarget", new Vector3(4f, 0f, 0f));
        StatusData targetStatus = CreateStatusData(targetObject, UnitAlignment.Friendly);

        SetCurrentTarget(brain, new NonPlayerTargetingResult
        {
            targetData = targetStatus,
            targetTransform = targetStatus.transform,
            targetCollider = null,
            aimPoint = targetStatus.transform.position,
            launchDirection = Vector3.right,
            sqrDistance = 16f,
        });

        owner.transform.position = Vector3.zero;
        owner.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);

        Blackboard board = new Blackboard();
        brain.Tick(board, 0.1f);

        Assert.That(board.AttackPressed, Is.False, "第一次进入攻击距离时应先进入预转向，而不是直接开火。");
        Assert.That(Vector3.Angle(owner.transform.forward, Vector3.forward), Is.LessThan(0.1f));

        board.ClearAllData();
        brain.Tick(board, 0.2f);

        Assert.That(board.AttackPressed, Is.True);
        Assert.That(board.AttackHeld, Is.True);
        Assert.That(Vector3.Angle(owner.transform.forward, Vector3.right), Is.LessThan(0.1f));
    }

    private EnemyBrainModule CreateEnemyBrain(out EnemyDriver owner, out NonPlayerTargetingModule targetingModule)
    {
        GameObject ownerObject = CreateGameObject("EnemyOwner", Vector3.zero);
        owner = ownerObject.AddComponent<EnemyDriver>();
        targetingModule = ownerObject.GetComponent<NonPlayerTargetingModule>();
        if (targetingModule == null)
            targetingModule = ownerObject.AddComponent<NonPlayerTargetingModule>();

        EnemyBrainModule brain = ownerObject.GetComponent<EnemyBrainModule>();
        if (brain == null)
            brain = ownerObject.AddComponent<EnemyBrainModule>();

        StatusData ownerStatus = CreateStatusData(ownerObject, UnitAlignment.Enemy);
        SetPrivateField(owner, "_nonPlayerTargetingProvider", targetingModule);
        SetProtectedField(owner, "statusData", ownerStatus);
        brain.Initialize(owner, null);
        return brain;
    }

    private StatusData CreateStatusData(GameObject gameObject, UnitAlignment alignment)
    {
        StatusData statusData = gameObject.GetComponent<StatusData>();
        if (statusData == null)
            statusData = gameObject.AddComponent<StatusData>();

        SetPrivateField(statusData, "fallbackUnitAlignment", alignment);
        statusData.RefreshFromDriver(false, true);
        return statusData;
    }

    private GameObject CreateGameObject(string name, Vector3 position)
    {
        GameObject gameObject = new GameObject(name);
        gameObject.transform.position = position;
        _runtimeObjects.Add(gameObject);
        return gameObject;
    }

    private static void SetCurrentTarget(EnemyBrainModule brain, NonPlayerTargetingResult result)
    {
        System.Reflection.MethodInfo method = typeof(EnemyBrainModule).GetMethod(
            "SetCurrentTarget",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(brain, new object[] { result });
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        System.Reflection.FieldInfo field = target.GetType().GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing field: {fieldName}");
        field.SetValue(target, value);
    }

    private static void SetProtectedField(object target, string fieldName, object value)
    {
        System.Reflection.FieldInfo field = target.GetType().BaseType?.GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing protected field: {fieldName}");
        field.SetValue(target, value);
    }

    private sealed class TestFacingState : CharacterStateBase
    {
        protected override CharacterStateId StateId => CharacterStateId.Idle;

        public TestFacingState(CharacterContext context) : base(new HSM(), context)
        {
        }

        public override void OnEnter()
        {
        }

        public override void OnUpdate()
        {
        }

        public override void OnExit()
        {
        }

        public void InvokeTryFaceProjectileTarget()
        {
            TryFaceProjectileTarget();
        }
    }

    private sealed class StaticTargetingProvider : IUnitTargetingProvider
    {
        private readonly Vector3 _aimPoint;

        public StaticTargetingProvider(Vector3 aimPoint)
        {
            _aimPoint = aimPoint;
        }

        public bool TryResolveProjectileTargeting(StatusData ownerData, Vector3 spawnPosition, Vector3 fallbackDirection,
            out ProjectileTargetingResult result, int targetingScopeId = 0)
        {
            result = new ProjectileTargetingResult
            {
                targetData = null,
                targetTransform = null,
                targetCollider = null,
                aimPoint = _aimPoint,
                launchDirection = (_aimPoint - spawnPosition).normalized,
                usesLockedSnapshot = false,
            };
            return true;
        }
    }
}
#endif
