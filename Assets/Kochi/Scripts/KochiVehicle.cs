using Kochi.World;
using UnityEngine;

namespace Kochi.Gameplay
{
    /// <summary>
    /// Simple WheelCollider car. Independent of the GTA vehicle pipeline (which needs GTA model files),
    /// so it works with placeholder geometry and, later, with original vehicle models.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class KochiVehicle : MonoBehaviour
    {
        public float motorTorque = 1600f;
        public float brakeTorque = 3500f;
        public float idleBrakeTorque = 60f;
        public float maxSteerAngle = 32f;
        public float maxSpeedKmh = 90f;

        public WheelCollider[] steerWheels = new WheelCollider[0];
        public WheelCollider[] driveWheels = new WheelCollider[0];
        public WheelCollider[] wheels = new WheelCollider[0];
        public Transform[] wheelVisuals = new Transform[0];

        Rigidbody _rigidbody;

        public bool HasDriver { get; private set; }
        public Rigidbody Body => _rigidbody != null ? _rigidbody : (_rigidbody = GetComponent<Rigidbody>());
        public float SpeedKmh => Body.velocity.magnitude * 3.6f;

        public void SetDriver(bool hasDriver)
        {
            HasDriver = hasDriver;
        }

        void FixedUpdate()
        {
            float throttle = 0f;
            float steer = 0f;
            bool handbrake = !HasDriver;
            if (HasDriver)
            {
                throttle = Input.GetAxis("Vertical");
                steer = Input.GetAxis("Horizontal");
                handbrake = Input.GetKey(KeyCode.Space);
            }

            float forwardSpeed = Vector3.Dot(Body.velocity, transform.forward);
            bool braking = (throttle < 0f && forwardSpeed > 1f) || (throttle > 0f && forwardSpeed < -1f);

            foreach (var wheel in steerWheels)
                wheel.steerAngle = steer * maxSteerAngle;

            foreach (var wheel in driveWheels)
                wheel.motorTorque = braking || SpeedKmh > maxSpeedKmh ? 0f : throttle * motorTorque;

            float brake = handbrake ? brakeTorque
                : braking ? brakeTorque * Mathf.Abs(throttle)
                : Mathf.Approximately(throttle, 0f) ? idleBrakeTorque
                : 0f;
            foreach (var wheel in wheels)
                wheel.brakeTorque = brake;
        }

        void Update()
        {
            int count = Mathf.Min(wheels.Length, wheelVisuals.Length);
            for (int i = 0; i < count; i++)
            {
                wheels[i].GetWorldPose(out Vector3 position, out Quaternion rotation);
                wheelVisuals[i].SetPositionAndRotation(position, rotation * Quaternion.Euler(0f, 0f, 90f));
            }
        }

        public Vector3 GetExitPosition(float side)
        {
            return transform.position + transform.right * (side * 1.9f) + Vector3.up * 0.2f;
        }

        public static KochiVehicle CreatePlaceholderCar(string name, Vector3 position, Quaternion rotation, Color bodyColor)
        {
            var root = new GameObject(name);
            root.transform.SetPositionAndRotation(position, rotation);

            var body = root.AddComponent<Rigidbody>();
            body.mass = 1200f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var block = new MaterialPropertyBlock();
            AddPart(root.transform, "Body", PrimitiveType.Cube, new Vector3(0f, 0.75f, 0f), new Vector3(1.8f, 0.7f, 4.2f), bodyColor, true, block);
            AddPart(root.transform, "Cabin", PrimitiveType.Cube, new Vector3(0f, 1.35f, -0.2f), new Vector3(1.6f, 0.55f, 2.2f), new Color(0.15f, 0.2f, 0.25f), true, block);

            var vehicle = root.AddComponent<KochiVehicle>();
            var positions = new[]
            {
                new Vector3(-0.85f, 0.45f, 1.35f),
                new Vector3(0.85f, 0.45f, 1.35f),
                new Vector3(-0.85f, 0.45f, -1.35f),
                new Vector3(0.85f, 0.45f, -1.35f),
            };
            string[] names = { "FL", "FR", "RL", "RR" };
            vehicle.wheels = new WheelCollider[4];
            vehicle.wheelVisuals = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                var wheelObject = new GameObject($"Wheel_{names[i]}");
                wheelObject.transform.SetParent(root.transform, false);
                wheelObject.transform.localPosition = positions[i];
                var wheel = wheelObject.AddComponent<WheelCollider>();
                wheel.mass = 20f;
                wheel.radius = 0.36f;
                wheel.suspensionDistance = 0.2f;
                wheel.suspensionSpring = new JointSpring { spring = 35000f, damper = 4500f, targetPosition = 0.5f };
                vehicle.wheels[i] = wheel;

                vehicle.wheelVisuals[i] = AddPart(root.transform, $"WheelVisual_{names[i]}", PrimitiveType.Cylinder,
                    positions[i], new Vector3(0.72f, 0.12f, 0.72f), new Color(0.08f, 0.08f, 0.08f), false, block).transform;
            }
            vehicle.steerWheels = new[] { vehicle.wheels[0], vehicle.wheels[1] };
            vehicle.driveWheels = new[] { vehicle.wheels[2], vehicle.wheels[3] };
            vehicle.wheels[0].ConfigureVehicleSubsteps(5f, 12, 15);

            body.centerOfMass = new Vector3(0f, 0.35f, 0f);
            return vehicle;
        }

        static GameObject AddPart(Transform parent, string name, PrimitiveType type, Vector3 localPosition, Vector3 localScale,
            Color color, bool keepCollider, MaterialPropertyBlock block)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            if (!keepCollider)
                Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            if (type == PrimitiveType.Cylinder)
                part.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            KochiMaterials.ApplyPlaceholder(part.GetComponent<Renderer>(), block, color);
            return part;
        }
    }
}
