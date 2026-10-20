using UnityEngine;

namespace Kochi.Gameplay
{
    /// <summary>Third-person on-foot movement using a CharacterController. Movement is relative to the camera.</summary>
    [RequireComponent(typeof(CharacterController))]
    public class KochiPlayerController : MonoBehaviour
    {
        public float walkSpeed = 2.5f;
        public float runSpeed = 6.5f;
        public float jumpHeight = 1.1f;
        public float gravity = -20f;
        public float turnSpeed = 720f;
        public Transform cameraTransform;

        CharacterController _controller;
        float _verticalVelocity;

        public CharacterController Controller => _controller != null ? _controller : (_controller = GetComponent<CharacterController>());

        public static KochiPlayerController CreatePlaceholder(Vector3 position, Quaternion rotation)
        {
            var go = new GameObject("KochiPlayer");
            go.transform.SetPositionAndRotation(position, rotation);
            var controller = go.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.35f;

            var block = new MaterialPropertyBlock();
            AddVisual(go.transform, PrimitiveType.Capsule, new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 0.9f, 0.7f), new Color(0.95f, 0.55f, 0.15f), block);
            // Small box on the face so the facing direction is readable.
            AddVisual(go.transform, PrimitiveType.Cube, new Vector3(0f, 1.5f, 0.32f), new Vector3(0.3f, 0.12f, 0.12f), new Color(0.1f, 0.1f, 0.1f), block);

            return go.AddComponent<KochiPlayerController>();
        }

        static void AddVisual(Transform parent, PrimitiveType type, Vector3 localPosition, Vector3 localScale, Color color, MaterialPropertyBlock block)
        {
            var visual = GameObject.CreatePrimitive(type);
            visual.name = $"Visual_{type}";
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = localPosition;
            visual.transform.localScale = localScale;
            block.Clear();
            block.SetColor("_Color", color);
            visual.GetComponent<Renderer>().SetPropertyBlock(block);
        }

        void Update()
        {
            var controller = Controller;
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            Vector3 forward = cameraTransform != null
                ? Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized
                : transform.forward;
            if (forward.sqrMagnitude < 0.001f)
                forward = transform.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 move = Vector3.ClampMagnitude(forward * v + right * h, 1f);

            float speed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;

            if (move.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(move, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
            }

            if (controller.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f;
            if (controller.isGrounded && Input.GetKeyDown(KeyCode.Space))
                _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            _verticalVelocity += gravity * Time.deltaTime;

            controller.Move((move * speed + Vector3.up * _verticalVelocity) * Time.deltaTime);
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            var controller = Controller;
            controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            controller.enabled = true;
            _verticalVelocity = 0f;
        }
    }
}
