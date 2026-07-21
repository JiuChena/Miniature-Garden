using UnityEngine;

namespace TeoGames.Mesh_Combiner.Demo._16___Complex_Visibility_Sample.Scripts {
    public class AnimalController : MonoBehaviour {
        public float moveSpeed = 2f;
        public float rotateSpeed = 100f;
        public float changeDirectionInterval = 3f;

        private float _Timer;
        private Vector3 _TargetDirection;

        private void Start() {
            _TargetDirection = transform.forward;
            _Timer = changeDirectionInterval;
            SnapToGround();
        }

        private void SnapToGround() {
            if (Physics.Raycast(transform.position + Vector3.up * 50f, Vector3.down, out var hit, 100f)) {
                transform.position = new Vector3(transform.position.x, hit.point.y, transform.position.z);
            }
        }

        private void Update() {
            _Timer -= Time.deltaTime;
            if (_Timer <= 0) {
                _Timer = changeDirectionInterval + Random.Range(-1f, 1f);
                PickNewDirection();
            }

            // Rotate towards target direction
            var targetRotation = Quaternion.LookRotation(_TargetDirection);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRotation, rotateSpeed * Time.deltaTime);

            // Move forward
            transform.Translate(Vector3.forward * (moveSpeed * Time.deltaTime));

            // Stick to ground
            if (Physics.Raycast(transform.position + Vector3.up * 10f, Vector3.down, out var hit, 20f)) {
                transform.position = new Vector3(transform.position.x, hit.point.y, transform.position.z);
            }
        }

        private void PickNewDirection() {
            var angle = Random.Range(0f, 360f);
            _TargetDirection = new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad), 0, Mathf.Cos(angle * Mathf.Deg2Rad));
        }
    }
}