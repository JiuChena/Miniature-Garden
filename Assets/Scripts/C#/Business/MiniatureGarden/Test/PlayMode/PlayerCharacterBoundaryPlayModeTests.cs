#if MINIATURE_GARDEN_INCLUDE_PLAYMODE_TESTS
using NUnit.Framework;
using UnityEngine;
using CoreFramework;

public sealed class PlayerCharacterBoundaryPlayModeTests
{
    [Test]
    public void TryFaceProjectileTarget_RotatesWhenAutomaticFacingEnabled()
    {
        GameObject ownerObject = new GameObject("PlayerOwner");
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
                EnableAutomaticProjectileFacing = true,
            };

            TestFacingState state = new TestFacingState(context);
            state.InvokeTryFaceProjectileTarget();

            Vector3 flattenedForward = ownerObject.transform.forward;
            flattenedForward.y = 0f;
            Assert.That(Vector3.Angle(flattenedForward.normalized, Vector3.right), Is.LessThan(0.1f));
            Assert.That(context.LastTargetFacingApplied, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(ownerObject);
            Object.DestroyImmediate(targetObject);
        }
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
