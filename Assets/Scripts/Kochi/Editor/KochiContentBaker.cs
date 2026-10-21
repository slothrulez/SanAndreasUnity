using System.Collections.Generic;
using System.IO;
using System.Linq;
using SanAndreasUnity.Behaviours;
using UnityEditor;
using UnityEngine;

namespace SanAndreasUnity.Kochi.Editor
{
    /// Converts the free (CC0) Quaternius models into prefabs and legacy clips laid out the way
    /// the engine's PedModel / Vehicle code expects (frame names, root-motion velocity curves, wheel dummies).
    public static class KochiContentBaker
    {
        const string CharactersSrc = "Assets/Kochi/ThirdParty/Quaternius/Characters";
        const string CarsSrc = "Assets/Kochi/ThirdParty/Quaternius/Cars";
        const string OutRoot = "Assets/Kochi/Resources/Kochi/Engine";
        public const string ModelObjectName = "KochiModel";

        static readonly Dictionary<string, string> s_boneMap = new Dictionary<string, string>
        {
            { "Bone", "Root" }, { "Hips", "Pelvis" }, { "Abdomen", "Spine" }, { "Torso", "Spine1" },
            { "Shoulder.L", "Bip01LClavicle" }, { "Shoulder.R", "Bip01RClavicle" },
            { "UpperArm.L", "LUpperArm" }, { "UpperArm.R", "RUpperArm" },
            { "LowerArm.L", "LForeArm" }, { "LowerArm.R", "RForeArm" },
            { "Palm.L", "LHand" }, { "Palm.R", "RHand" },
            { "Fingers.L", "LFinger" }, { "Fingers.R", "RFinger" },
            { "UpperLeg.L", "LThigh" }, { "UpperLeg.R", "RThigh" },
            { "LowerLeg.L", "LCalf" }, { "LowerLeg.R", "RCalf" },
            { "LowerLeg.L_end", "LFoot" }, { "LowerLeg.R_end", "RFoot" },
        };

        static readonly Dictionary<string, float> s_clipSpeeds = new Dictionary<string, float>
        {
            { "Man_Walk", 1.5f }, { "Man_Run", 4.8f },
        };

        static string MapBone(string name) => s_boneMap.TryGetValue(name, out var mapped) ? mapped : name;

        static string MapPath(string path) =>
            string.IsNullOrEmpty(path) ? ModelObjectName : ModelObjectName + "/" + string.Join("/", path.Split('/').Select(MapBone));

        static string CleanClipName(string name) => name.Contains("|") ? name.Substring(name.LastIndexOf('|') + 1) : name;

        [MenuItem("Kochi/Bake Engine Content")]
        public static void BakeAll()
        {
            EnsureFolder(OutRoot + "/Peds");
            EnsureFolder(OutRoot + "/Anims");
            EnsureFolder(OutRoot + "/Vehicles");
            EnsureFolder(OutRoot + "/Meshes");

            var fbxs = Directory.GetFiles(CharactersSrc, "*.fbx").Select(p => p.Replace('\\', '/')).OrderBy(p => p).ToArray();
            foreach (var p in fbxs)
                SetLegacy(p);
            foreach (var p in fbxs)
                BakeCharacter(p, bakeClips: p == fbxs[0]);

            foreach (var p in Directory.GetFiles(CarsSrc, "*.fbx").Select(p => p.Replace('\\', '/')).OrderBy(p => p))
                BakeCar(p);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[KochiBake] done");
        }

        public static void BakeAllBatch()
        {
            try { BakeAll(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        static void SetLegacy(string path)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (importer.animationType == ModelImporterAnimationType.Legacy && importer.importAnimation)
                return;
            importer.animationType = ModelImporterAnimationType.Legacy;
            importer.importAnimation = true;
            importer.SaveAndReimport();
        }

        static void BakeCharacter(string fbxPath, bool bakeClips)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            var go = (GameObject)Object.Instantiate(model);
            go.name = ModelObjectName;
            foreach (var anim in go.GetComponentsInChildren<Animation>(true))
                Object.DestroyImmediate(anim);
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                if (t != go.transform)
                    t.name = MapBone(t.name);

            AddMissingBones(go.transform);
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                Debug.Log($"[KochiBake]   ped node '{t.name}' parent='{(t.parent ? t.parent.name : "")}' lp={t.localPosition}");

            var bounds = Bounds(go);
            Debug.Log($"[KochiBake] {Path.GetFileName(fbxPath)} bounds center={bounds.center} size={bounds.size}");

            string name = Path.GetFileNameWithoutExtension(fbxPath);
            PrefabUtility.SaveAsPrefabAsset(go, $"{OutRoot}/Peds/{name}.prefab");
            Object.DestroyImmediate(go);

            if (!bakeClips)
                return;

            foreach (var src in AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")))
            {
                string clipName = CleanClipName(src.name);
                var clip = new AnimationClip { legacy = true, name = clipName, wrapMode = WrapMode.Loop };
                foreach (var b in AnimationUtility.GetCurveBindings(src))
                {
                    string leaf = b.path.Split('/').Last();
                    // Root motion is supplied via Frame.LocalVelocity; keep the root bone in place.
                    if (leaf == "Bone" && b.propertyName.StartsWith("m_LocalPosition"))
                        continue;
                    var nb = EditorCurveBinding.FloatCurve(MapPath(b.path), b.type, b.propertyName);
                    AnimationUtility.SetEditorCurve(clip, nb, AnimationUtility.GetEditorCurve(src, b));
                }
                s_clipSpeeds.TryGetValue(clipName, out float speed);
                string rootPath = MapPath(AnimationUtility.GetCurveBindings(src).Select(b => b.path).FirstOrDefault(p => p.Split('/').Last() == "Bone") ?? "HumanArmature/Bone");
                foreach (var axis in new[] { "x", "y", "z" })
                {
                    float v = axis == "z" ? speed : 0f;
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(rootPath, typeof(Frame), "LocalVelocity." + axis),
                        AnimationCurve.Constant(0f, Mathf.Max(src.length, 1f / 30f), v));
                }
                clip.EnsureQuaternionContinuity();
                AssetDatabase.CreateAsset(clip, $"{OutRoot}/Anims/{clipName}.anim");
                Debug.Log($"[KochiBake] clip {clipName} len={src.length:F2} curves={AnimationUtility.GetCurveBindings(clip).Length} root={rootPath}");
            }
        }

        // PedModel's ragdoll needs a few bones the free rig doesn't have; add them as empty child bones.
        static void AddMissingBones(Transform root)
        {
            Transform Get(string n) => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == n);
            void Ensure(string boneName, string parentName, System.Func<Transform, Vector3> localPos)
            {
                if (Get(boneName) != null)
                    return;
                var parent = Get(parentName);
                if (parent == null)
                {
                    Debug.LogWarning($"[KochiBake] cannot add {boneName}: no {parentName}");
                    return;
                }
                var t = new GameObject(boneName).transform;
                t.SetParent(parent, false);
                t.localPosition = localPos(parent);
            }
            // Extend a limb by the length of its parent segment.
            Ensure("LFoot", "LCalf", p => p.localPosition);
            Ensure("RFoot", "RCalf", p => p.localPosition);
            Ensure("Jaw", "Head", p => Vector3.zero);
        }

        static void BakeCar(string fbxPath)
        {
            string name = Path.GetFileNameWithoutExtension(fbxPath);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            var go = (GameObject)Object.Instantiate(model);
            go.name = ModelObjectName;

            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                var mf = t.GetComponent<MeshFilter>();
                Debug.Log($"[KochiBake]   {name} node '{t.name}' parent='{(t.parent ? t.parent.name : "")}' lp={t.localPosition} lr={t.localEulerAngles} ls={t.localScale} mesh={(mf && mf.sharedMesh ? mf.sharedMesh.bounds.ToString() : "-")}");
            }
            Transform Find(string suffix) => go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.EndsWith(suffix));
            var fl = Find("_FrontLeftWheel");
            var fr = Find("_FrontRightWheel");
            var back = Find("_BackWheels");
            var bodyBounds = Bounds(go, t => t != fl && t != fr && t != back && !t.IsChildOf(fl) && !t.IsChildOf(fr) && !t.IsChildOf(back));
            Debug.Log($"[KochiBake] {name} body center={bodyBounds.center} size={bodyBounds.size} fl={Bounds(fl.gameObject, null, go.transform).center} fr={Bounds(fr.gameObject, null, go.transform).center} back={Bounds(back.gameObject, null, go.transform).center}/{Bounds(back.gameObject, null, go.transform).size}");

            // Wheel template centred on its own pivot (the engine instantiates it under each wheel dummy).
            var flBounds = Bounds(fl.gameObject, null, go.transform);
            var wheel = new GameObject("wheel");
            var srcMf = fl.GetComponentInChildren<MeshFilter>();
            var mesh = Object.Instantiate(srcMf.sharedMesh);
            var m = go.transform.worldToLocalMatrix * srcMf.transform.localToWorldMatrix;
            var verts = mesh.vertices;
            for (int i = 0; i < verts.Length; i++)
                verts[i] = m.MultiplyPoint3x4(verts[i]) - flBounds.center;
            mesh.vertices = verts;
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            AssetDatabase.CreateAsset(mesh, $"{OutRoot}/Meshes/{name}_wheel.asset");
            wheel.AddComponent<MeshFilter>().sharedMesh = mesh;
            wheel.AddComponent<MeshRenderer>().sharedMaterials = srcMf.GetComponent<MeshRenderer>().sharedMaterials;

            var backBounds = Bounds(back.gameObject, null, go.transform);
            bool leftIsNegX = Bounds(fl.gameObject, null, go.transform).center.x < Bounds(fr.gameObject, null, go.transform).center.x;
            Vector3 flC = Bounds(fl.gameObject, null, go.transform).center, frC = Bounds(fr.gameObject, null, go.transform).center;
            Vector3 lbC = new Vector3(flC.x, backBounds.center.y, backBounds.center.z);
            Vector3 rbC = new Vector3(frC.x, backBounds.center.y, backBounds.center.z);
            AddDummy(go, "wheel_lf_dummy", flC);
            AddDummy(go, "wheel_rf_dummy", frC);
            AddDummy(go, "wheel_lb_dummy", lbC);
            AddDummy(go, "wheel_rb_dummy", rbC);
            // The right-front dummy uses the template directly; the engine instantiates copies for the others.
            wheel.transform.SetParent(go.transform.Find("wheel_rf_dummy"), false);
            Object.DestroyImmediate(fl.gameObject);
            Object.DestroyImmediate(fr.gameObject);
            Object.DestroyImmediate(back.gameObject);

            // Driver seat: front half of the cabin, on the right (+x) side; the engine mirrors it for the left seat.
            float seatX = Mathf.Abs(flC.x) * 0.45f * (leftIsNegX ? 1f : -1f);
            AddDummy(go, "ped_frontseat", new Vector3(seatX, bodyBounds.min.y + bodyBounds.size.y * 0.25f, bodyBounds.center.z + bodyBounds.size.z * 0.05f));
            AddDummy(go, "ped_backseat", new Vector3(seatX, bodyBounds.min.y + bodyBounds.size.y * 0.25f, bodyBounds.center.z - bodyBounds.size.z * 0.2f));

            var box = go.AddComponent<BoxCollider>();
            box.center = bodyBounds.center;
            box.size = bodyBounds.size;

            PrefabUtility.SaveAsPrefabAsset(go, $"{OutRoot}/Vehicles/{name}.prefab");
            Object.DestroyImmediate(go);
        }

        static void AddDummy(GameObject parent, string dummyName, Vector3 localPos)
        {
            var t = new GameObject(dummyName).transform;
            t.SetParent(parent.transform, false);
            t.localPosition = localPos;
        }

        static Bounds Bounds(GameObject go, System.Func<Transform, bool> filter = null, Transform relativeTo = null)
        {
            var root = relativeTo != null ? relativeTo : go.transform;
            bool has = false;
            var b = new Bounds();
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (filter != null && !filter(r.transform))
                    continue;
                var rb = r is SkinnedMeshRenderer smr && smr.sharedMesh != null ? TransformBounds(smr.sharedMesh.bounds, root.worldToLocalMatrix * smr.transform.localToWorldMatrix)
                    : r.GetComponent<MeshFilter>() is MeshFilter mf && mf.sharedMesh != null ? TransformBounds(mf.sharedMesh.bounds, root.worldToLocalMatrix * mf.transform.localToWorldMatrix)
                    : new Bounds(root.InverseTransformPoint(r.bounds.center), Vector3.zero);
                if (!has) { b = rb; has = true; } else b.Encapsulate(rb);
            }
            return b;
        }

        static Bounds TransformBounds(Bounds local, Matrix4x4 m)
        {
            var result = new Bounds(m.MultiplyPoint3x4(local.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var c = local.center + Vector3.Scale(local.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                result.Encapsulate(m.MultiplyPoint3x4(c));
            }
            return result;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
