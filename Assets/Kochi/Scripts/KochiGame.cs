using System;
using System.Collections.Generic;
using Kochi.World;
using UnityEngine;

namespace Kochi.Gameplay
{
    /// <summary>
    /// Entry point of the Kochi prototype scene: builds the district from data, spawns the player,
    /// vehicles and camera, and handles entering/exiting vehicles and the HUD.
    /// Does not depend on any GTA San Andreas data or San Andreas Unity systems.
    /// </summary>
    public class KochiGame : MonoBehaviour
    {
        public const string DistrictCommandLineArg = "-kochiDistrict";

        [Tooltip("Resource path (under a Resources folder, without extension) of the district JSON to load.")]
        public string districtResourcePath = KochiDistrictPaths.TestDistrict;
        public float enterVehicleDistance = 3.5f;
        public float maxExitSpeedKmh = 15f;
        public float landmarkHintDistance = 30f;

        public KochiDistrictData District { get; private set; }
        public KochiPlayerController Player { get; private set; }
        public KochiVehicle CurrentVehicle { get; private set; }
        public IReadOnlyList<KochiVehicle> Vehicles => _vehicles;

        readonly List<KochiVehicle> _vehicles = new List<KochiVehicle>();
        GameObject _districtRoot;
        KochiFollowCamera _camera;
        string _lastError;

        void Start()
        {
            SetupLighting();
            SetupCamera();
            try
            {
                LoadDistrict(ResolveDistrictPath());
                SpawnPlayer();
                SpawnVehicles();
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                Debug.LogException(ex);
            }
        }

        string ResolveDistrictPath()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], DistrictCommandLineArg, StringComparison.OrdinalIgnoreCase))
                    return KochiDistrictPaths.ResourceFolder + args[i + 1];
            }
            return districtResourcePath;
        }

        void LoadDistrict(string resourcePath)
        {
            if (_districtRoot != null)
                Destroy(_districtRoot);
            District = KochiDistrictBuilder.LoadFromResources(resourcePath);
            _districtRoot = KochiDistrictBuilder.Build(District);
            _lastError = null;
        }

        void SpawnPlayer()
        {
            var spawn = District.playerSpawn ?? new KochiSpawnData();
            Player = KochiPlayerController.CreatePlaceholder(spawn.position, Quaternion.Euler(0f, spawn.rotationY, 0f));
            Player.cameraTransform = _camera.transform;
            _camera.SetTarget(Player.transform, 5f, 1.6f);
        }

        void SpawnVehicles()
        {
            if (District.vehicleSpawns == null)
                return;
            for (int i = 0; i < District.vehicleSpawns.Length; i++)
            {
                var spawn = District.vehicleSpawns[i];
                Color color = KochiDistrictLayout.ParseColor(spawn.color, Color.white);
                _vehicles.Add(KochiVehicle.CreatePlaceholderCar($"KochiCar_{i}", spawn.position + Vector3.up * 0.3f,
                    Quaternion.Euler(0f, spawn.rotationY, 0f), color));
            }
        }

        void SetupCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1500f;
            _camera = cam.GetComponent<KochiFollowCamera>();
            if (_camera == null)
                _camera = cam.gameObject.AddComponent<KochiFollowCamera>();
        }

        static void SetupLighting()
        {
            foreach (var light in FindObjectsOfType<Light>())
            {
                if (light.type == LightType.Directional)
                    return;
            }
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        void Update()
        {
            if (Input.GetMouseButtonDown(0))
                Cursor.lockState = CursorLockMode.Locked;
            if (Input.GetKeyDown(KeyCode.Escape))
                Cursor.lockState = CursorLockMode.None;
            Cursor.visible = Cursor.lockState != CursorLockMode.Locked;

            if (Input.GetKeyDown(KeyCode.F5))
                ReloadDistrict();

            if (Player == null)
                return;

            if (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Return))
            {
                if (CurrentVehicle == null)
                    TryEnterNearestVehicle();
                else
                    TryExitVehicle();
            }
        }

        void ReloadDistrict()
        {
            try
            {
                LoadDistrict(ResolveDistrictPath());
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                Debug.LogException(ex);
            }
        }

        public KochiVehicle FindNearestVehicle(out float distance)
        {
            KochiVehicle nearest = null;
            distance = float.PositiveInfinity;
            foreach (var vehicle in _vehicles)
            {
                if (vehicle == null)
                    continue;
                float d = Vector3.Distance(Player.transform.position, vehicle.transform.position);
                if (d < distance)
                {
                    distance = d;
                    nearest = vehicle;
                }
            }
            return nearest;
        }

        public bool TryEnterNearestVehicle()
        {
            var vehicle = FindNearestVehicle(out float distance);
            if (vehicle == null || distance > enterVehicleDistance)
                return false;

            CurrentVehicle = vehicle;
            vehicle.SetDriver(true);
            Player.gameObject.SetActive(false);
            _camera.SetTarget(vehicle.transform, 7f, 1.4f);
            return true;
        }

        public bool TryExitVehicle()
        {
            if (CurrentVehicle == null || CurrentVehicle.SpeedKmh > maxExitSpeedKmh)
                return false;

            Vector3? exitPosition = null;
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 candidate = CurrentVehicle.GetExitPosition(side);
                bool blocked = Physics.CheckCapsule(candidate + Vector3.up * 0.4f, candidate + Vector3.up * 1.5f, 0.35f,
                    ~0, QueryTriggerInteraction.Ignore);
                if (!blocked)
                {
                    exitPosition = candidate;
                    break;
                }
            }
            if (exitPosition == null)
                return false;

            CurrentVehicle.SetDriver(false);
            Player.gameObject.SetActive(true);
            Player.Teleport(exitPosition.Value, Quaternion.Euler(0f, CurrentVehicle.transform.eulerAngles.y, 0f));
            CurrentVehicle = null;
            _camera.SetTarget(Player.transform, 5f, 1.6f);
            return true;
        }

        public string FindNearbyLandmark()
        {
            if (District?.props == null)
                return null;
            Vector3 position = CurrentVehicle != null ? CurrentVehicle.transform.position : Player.transform.position;
            string best = null;
            float bestDistance = landmarkHintDistance;
            foreach (var prop in District.props)
            {
                if (!prop.isLandmark)
                    continue;
                float d = Vector2.Distance(new Vector2(position.x, position.z), new Vector2(prop.position.x, prop.position.z));
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = prop.name;
                }
            }
            return best;
        }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 360, 220), GUI.skin.box);
            if (_lastError != null)
            {
                GUILayout.Label($"Error: {_lastError}");
            }
            else if (District != null && Player != null)
            {
                GUILayout.Label($"Kochi prototype — {District.displayName}");
                if (CurrentVehicle != null)
                {
                    GUILayout.Label($"Driving: {CurrentVehicle.SpeedKmh:0} km/h");
                    GUILayout.Label("W/S throttle/brake, A/D steer, Space handbrake, F exit");
                }
                else
                {
                    GUILayout.Label("WASD move, Shift run, Space jump, mouse look");
                    if (FindNearestVehicle(out float d) != null && d <= enterVehicleDistance)
                        GUILayout.Label("Press F to drive");
                }
                string landmark = FindNearbyLandmark();
                if (landmark != null)
                    GUILayout.Label($"Nearby: {landmark}");
                GUILayout.Label("Click to capture mouse, Esc to release, F5 reload district");
            }
            GUILayout.EndArea();
        }
    }
}
