using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Kochi.World;
using SanAndreasUnity.Behaviours;
using SanAndreasUnity.Behaviours.Vehicles;
using SanAndreasUnity.Importing.Animation;
using SanAndreasUnity.Importing.Items;
using SanAndreasUnity.Importing.Items.Definitions;
using SanAndreasUnity.Importing.Vehicles;
using SanAndreasUnity.Net;
using UGameCore.Utilities;
using UnityEngine;

namespace SanAndreasUnity.Kochi
{
    /// Supplies the engine with Kochi content (free models, baked clips, code-defined handling)
    /// instead of reading a GTA San Andreas installation.
    public static class KochiContent
    {
        public const string AnimFileName = "kochi_anims";
        public const string AnimGroupName = "kochi_man";
        public const string DistrictResource = "Kochi/Districts/fort_kochi_test";
        const string EngineResources = "Kochi/Engine/";
        const float PedModelScale = 0.375f;
        const float PedModelYOffset = -1.0f;

        static readonly string[] s_pedPrefabs = { "Male_Casual", "Male_Shirt", "Male_LongSleeve" };

        static readonly (int id, string prefab, string gameName)[] s_vehicles =
        {
            (400, "Taxi", "KTAXI"), (401, "NormalCar1", "KSEDAN"), (402, "NormalCar2", "KHATCH"), (403, "SUV", "KSUV"),
        };

        // Columns follow the engine's handling.cfg parser (Handling.Car); values are our own.
        const string CarHandling = "KOCHI_CAR 1400.0 3000.0 2.2 0.0 0.0 -0.15 75 0.80 0.85 0.5 5 160.0 20.0 10.0 R P 8.0 0.55 0 35.0 1.2 0.12 0.0 0.28 -0.17 0.5 0.0 0.2 0.6 9000 0 0 0 1 0";

        static readonly Dictionary<string, string> s_modelToPrefab = new Dictionary<string, string>();
        static readonly Dictionary<string, Importing.Conversion.Animation> s_anims = new Dictionary<string, Importing.Conversion.Animation>();
        static bool s_registered;

        public static bool IsInstalled { get; private set; }
        public static KochiDistrictData District { get; private set; }

        /// Starts the Kochi game straight away when launched with "-kochi" (desktop) or "?kochi" in the page URL (WebGL).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart()
        {
            bool requested = System.Environment.GetCommandLineArgs().Contains("-kochi")
                || (Application.absoluteURL ?? "").Contains("kochi");
            if (requested)
                new GameObject("KochiAutoStart").AddComponent<KochiAutoStart>();
        }

        class KochiCredits : MonoBehaviour
        {
            const string Text = "Map data \u00a9 OpenStreetMap contributors (ODbL) \u00b7 Models: Quaternius (CC0)";

            void OnGUI()
            {
                var style = new GUIStyle(GUI.skin.label) { fontSize = 11 };
                style.normal.textColor = new Color(1f, 1f, 1f, 0.8f);
                GUI.Label(new Rect(8, Screen.height - 22, 600, 20), Text, style);
            }
        }

        class KochiAutoStart : MonoBehaviour
        {
            System.Collections.IEnumerator Start()
            {
                yield return null;
                yield return null;
                StartGame();
                Destroy(gameObject);
            }
        }

        public static void StartGame()
        {
            Install();
            NetManager.StartServer(7777, "Main", 1, false, true);
        }

        public static void Install()
        {
            if (IsInstalled)
                return;
            IsInstalled = true;
            Importing.Zone.CustomZoneName = _ => "Fort Kochi";
            var credits = new GameObject("KochiCredits");
            Object.DontDestroyOnLoad(credits);
            credits.AddComponent<KochiCredits>();
            PedModel.CustomModelLoader = LoadPedModel;
            Vehicle.CustomModelLoader = LoadVehicleModel;
            Importing.Conversion.Animation.CustomLoader = LoadAnimation;
            Loader.CustomLoadingSteps = () => new[]
            {
                new Loader.LoadingStep(RegisterDefinitions, "Registering Kochi content"),
                new Loader.LoadingStep(BuildWorld, "Building Kochi world"),
                new Loader.LoadingStep(SpawnVehicles, "Spawning vehicles"),
            };
        }

        static string ModelName(string prefab) => "kochi_" + prefab.ToLowerInvariant();

        public static void RegisterDefinitions()
        {
            if (s_registered)
                return;
            s_registered = true;

            for (int i = 0; i < s_pedPrefabs.Length; i++)
            {
                string model = ModelName(s_pedPrefabs[i]);
                s_modelToPrefab[model] = s_pedPrefabs[i];
                Item.AddDefinition(new PedestrianDef($"{i + 1}, {model}, kochi, CIVMALE, STAT_STD_MISSION, {AnimGroupName}, 1FF, 0, null, 0, 0"));
            }

            AnimationGroup.LoadFromStreamReader(new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(BuildAnimGroups()))));

            Handling.AddEntries(new Handling.IEntry[] { new Handling.Car(CarHandling) });

            foreach (var v in s_vehicles)
            {
                string model = ModelName(v.prefab);
                s_modelToPrefab[model] = v.prefab;
                Item.AddDefinition(new VehicleDef($"{v.id}, {model}, {model}, car, KOCHI_CAR, {v.gameName}, null, normal, 10, 0, 0, -1, 0.7, 0.7, -1"));
            }
        }

        static string BuildAnimGroups()
        {
            var overrides = new Dictionary<AnimGroup, string[]>
            {
                // Walk, Run, Panicked, Idle, RoadCross, WalkStart
                { AnimGroup.WalkCycle, new[] { "Man_Walk", "Man_Run", "Man_Run", "Man_Idle", "Man_Walk", "Man_Walk" } },
                // IdleArmed, FuckU, GUN_STAND, sprint_civi
                { AnimGroup.MyWalkCycle, new[] { "Man_Idle", "Man_Clapping", "Man_Idle", "Man_Run" } },
                // Sit, SitPassenger, DriveLeft, DriveRight, GetInLeft, GetInRight, GetOutLeft, GetOutRight
                { AnimGroup.Car, Enumerable.Repeat("Man_Sitting", 8).ToArray() },
            };
            var sb = new StringBuilder();
            foreach (AnimGroup group in System.Enum.GetValues(typeof(AnimGroup)))
            {
                if (group == AnimGroup.None)
                    continue;
                var names = Enumerable.Repeat("Man_Idle", 64).ToArray();
                if (overrides.TryGetValue(group, out var o))
                    o.CopyTo(names, 0);
                sb.AppendLine($"{AnimGroupName}, {AnimFileName}, {group}, {names.Length}");
                foreach (var n in names)
                    sb.AppendLine(n);
                sb.AppendLine("end");
            }
            return sb.ToString();
        }

        static Importing.Conversion.Animation LoadAnimation(string fileName, string clipName, FrameContainer frames)
        {
            if (fileName != AnimFileName)
                return null;
            if (s_anims.TryGetValue(clipName, out var cached))
                return cached;
            var clip = Resources.Load<AnimationClip>(EngineResources + "Anims/" + clipName)
                ?? Resources.Load<AnimationClip>(EngineResources + "Anims/Man_Idle");
            var anim = new Importing.Conversion.Animation(clip, Vector3.zero, Vector3.zero);
            s_anims[clipName] = anim;
            return anim;
        }

        static FrameContainer InstantiateModel(string folder, string modelName, Transform parent, out GameObject instance)
        {
            instance = null;
            if (!s_modelToPrefab.TryGetValue(modelName, out string prefabName))
                return null;
            var prefab = Resources.Load<GameObject>(EngineResources + folder + "/" + prefabName);
            if (prefab == null)
            {
                Debug.LogError($"Kochi prefab not found: {folder}/{prefabName}");
                return null;
            }
            instance = Object.Instantiate(prefab, parent, false);
            instance.name = "KochiModel";
            var container = parent.gameObject.AddComponent<FrameContainer>();
            container.InitializeFromHierarchy(instance.transform, t => t.name);
            return container;
        }

        static FrameContainer LoadPedModel(string modelName, Transform parent)
        {
            var frames = InstantiateModel("Peds", modelName, parent, out var instance);
            if (instance != null)
            {
                instance.transform.localScale = Vector3.one * PedModelScale;
                instance.transform.localPosition = new Vector3(0f, PedModelYOffset, 0f);
            }
            return frames;
        }

        static FrameContainer LoadVehicleModel(VehicleDef def, Transform parent) =>
            InstantiateModel("Vehicles", def.ModelName, parent, out _);

        static void BuildWorld()
        {
            District = KochiDistrictBuilder.LoadFromResources(DistrictResource);
            KochiDistrictBuilder.Build(District);
            var cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = District.playerSpawn.position + Vector3.up;
                cam.transform.rotation = Quaternion.Euler(0f, District.playerSpawn.rotationY, 0f);
            }
        }

        static void SpawnVehicles()
        {
            if (District?.vehicleSpawns == null || !NetStatus.IsServer)
                return;
            for (int i = 0; i < District.vehicleSpawns.Length; i++)
            {
                var spawn = District.vehicleSpawns[i];
                F.RunExceptionSafe(() => Vehicle.Create(s_vehicles[i % s_vehicles.Length].id,
                    spawn.position + Vector3.up * 0.5f, Quaternion.Euler(0f, spawn.rotationY, 0f)));
            }
        }
    }
}
