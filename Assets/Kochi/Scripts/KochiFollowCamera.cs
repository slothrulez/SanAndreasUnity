using UnityEngine;

namespace Kochi.Gameplay
{
    /// <summary>Orbit camera that follows a target and pulls in when geometry blocks the view.</summary>
    public class KochiFollowCamera : MonoBehaviour
    {
        public Transform target;
        public float distance = 5f;
        public float minDistance = 2f;
        public float maxDistance = 14f;
        public float pivotHeight = 1.6f;
        public float mouseSensitivity = 3f;
        public float collisionRadius = 0.25f;
        public float minPitch = -20f;
        public float maxPitch = 70f;

        float _yaw;
        float _pitch = 15f;
        readonly RaycastHit[] _hits = new RaycastHit[16];

        public void SetTarget(Transform newTarget, float newDistance, float newPivotHeight)
        {
            target = newTarget;
            distance = Mathf.Clamp(newDistance, minDistance, maxDistance);
            pivotHeight = newPivotHeight;
            if (target != null)
                _yaw = target.eulerAngles.y;
        }

        void LateUpdate()
        {
            if (target == null)
                return;

            if (Cursor.lockState == CursorLockMode.Locked || Input.GetMouseButton(1))
            {
                _yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
                _pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
            }
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
            distance = Mathf.Clamp(distance - Input.GetAxis("Mouse ScrollWheel") * 5f, minDistance, maxDistance);

            Vector3 pivot = target.position + Vector3.up * pivotHeight;
            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 direction = rotation * Vector3.back;

            float allowed = distance;
            int count = Physics.SphereCastNonAlloc(pivot, collisionRadius, direction, _hits, distance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (_hits[i].transform.IsChildOf(target))
                    continue;
                if (_hits[i].distance > 0f && _hits[i].distance < allowed)
                    allowed = _hits[i].distance;
            }

            transform.SetPositionAndRotation(pivot + direction * allowed, rotation);
        }
    }
}
