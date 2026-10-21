using System.Collections.Generic;
using Kochi.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace SanAndreasUnity.Kochi
{
    /// Builds the Kochi world from OpenStreetMap data converted by Tools/KochiOsm/osm_to_kochi.py.
    public static class KochiOsmWorld
    {
        public const string Resource = "Kochi/OSM/";
        const float ChunkSize = 250f;
        const float RoadHeight = 0.03f;

        [System.Serializable] public class Road { public float w; public int b; public int water; public string n; public float[] p; }
        [System.Serializable] public class Building { public float h; public float[] p; public int[] t; }
        [System.Serializable] public class Poi { public string t; public float x, z; public string n; }
        [System.Serializable] public class Spawn { public float x, z, heading, roadWidth; }
        [System.Serializable]
        public class Data
        {
            public string attribution;
            public float[] bounds;
            public Spawn spawn;
            public Road[] roads;
            public Building[] buildings;
            public Poi[] pois;
        }

        static readonly Color RoadColor = new Color32(43, 42, 48, 255);
        static readonly Color BridgeColor = new Color32(120, 116, 122, 255);
        static readonly Color[] BuildingColors =
        {
            new Color32(240, 232, 214, 255), new Color32(246, 246, 240, 255), new Color32(236, 196, 200, 255),
            new Color32(190, 216, 226, 255), new Color32(222, 214, 170, 255),
        };

        public static Data Load()
        {
            var text = Resources.Load<TextAsset>(Resource + "kochi_world");
            return text == null ? null : JsonUtility.FromJson<Data>(text.text);
        }

        public static GameObject Build(Data data)
        {
            var root = new GameObject("KochiOsmWorld");
            BuildGround(data, root.transform);
            BuildRoads(data, root.transform);
            BuildBuildings(data, root.transform);
            return root;
        }

        static void BuildGround(Data data, Transform parent)
        {
            var b = data.bounds;
            var go = new GameObject("Ground");
            go.transform.SetParent(parent, false);
            var mesh = new Mesh { name = "KochiGround" };
            mesh.vertices = new[] { new Vector3(b[0], 0, b[1]), new Vector3(b[0], 0, b[3]), new Vector3(b[2], 0, b[3]), new Vector3(b[2], 0, b[1]) };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var material = new Material(KochiMaterials.Placeholder) { mainTexture = Resources.Load<Texture2D>(Resource + "ground"), color = Color.white };
            material.SetFloat("_Glossiness", 0f);
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3((b[0] + b[2]) / 2f, -0.5f, (b[1] + b[3]) / 2f);
            box.size = new Vector3(b[2] - b[0], 1f, b[3] - b[1]);
        }

        class MeshBuilder
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<int> t = new List<int>();

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int i = v.Count;
                v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3);
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(v);
                mesh.SetTriangles(t, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        static Vector2Int ChunkOf(float x, float z) => new Vector2Int(Mathf.FloorToInt(x / ChunkSize), Mathf.FloorToInt(z / ChunkSize));

        static MeshBuilder Get<TKey>(Dictionary<TKey, MeshBuilder> dict, TKey key)
        {
            if (!dict.TryGetValue(key, out var mb))
                dict[key] = mb = new MeshBuilder();
            return mb;
        }

        static void Emit(Transform parent, string name, Mesh mesh, Color color, bool collider, MaterialPropertyBlock block)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            KochiMaterials.ApplyPlaceholder(renderer, block, color);
            if (collider)
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        static void BuildRoads(Data data, Transform parent)
        {
            var chunks = new Dictionary<(Vector2Int, bool), MeshBuilder>();
            foreach (var road in data.roads)
            {
                if (road.water != 0 || road.w <= 0 || road.p == null || road.p.Length < 4)
                    continue;
                float half = road.w / 2f;
                float y = RoadHeight + Mathf.Min(road.w, 14f) * 0.001f; // wider roads draw over narrower ones
                for (int i = 0; i + 3 < road.p.Length; i += 2)
                {
                    var a = new Vector3(road.p[i], y, road.p[i + 1]);
                    var c = new Vector3(road.p[i + 2], y, road.p[i + 3]);
                    var dir = c - a;
                    if (dir.sqrMagnitude < 0.0001f)
                        continue;
                    var side = Vector3.Cross(Vector3.up, dir.normalized) * half;
                    var along = dir.normalized * half * 0.5f; // overlap segments to hide gaps at joints
                    var mb = Get(chunks, (ChunkOf(a.x, a.z), road.b != 0));
                    mb.Quad(a - side - along, c - side + along, c + side + along, a + side - along);
                }
            }
            var block = new MaterialPropertyBlock();
            var roads = new GameObject("Roads").transform;
            roads.SetParent(parent, false);
            foreach (var kv in chunks)
                Emit(roads, $"Roads_{kv.Key.Item1.x}_{kv.Key.Item1.y}", kv.Value.ToMesh("KochiRoads"), kv.Key.Item2 ? BridgeColor : RoadColor, false, block);
        }

        static void BuildBuildings(Data data, Transform parent)
        {
            var chunks = new Dictionary<(Vector2Int, int), MeshBuilder>();
            for (int bi = 0; bi < data.buildings.Length; bi++)
            {
                var bld = data.buildings[bi];
                int n = bld.p.Length / 2;
                if (n < 3)
                    continue;
                var mb = Get(chunks, (ChunkOf(bld.p[0], bld.p[1]), bi % BuildingColors.Length));
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    var a = new Vector3(bld.p[2 * i], 0f, bld.p[2 * i + 1]);
                    var c = new Vector3(bld.p[2 * j], 0f, bld.p[2 * j + 1]);
                    var up = Vector3.up * bld.h;
                    mb.Quad(c, c + up, a + up, a); // ring is counter-clockwise seen from above, so this faces outwards
                }
                int start = mb.v.Count;
                for (int i = 0; i < n; i++)
                    mb.v.Add(new Vector3(bld.p[2 * i], bld.h, bld.p[2 * i + 1]));
                for (int k = 0; k + 2 < bld.t.Length; k += 3)
                {
                    mb.t.Add(start + bld.t[k]);
                    mb.t.Add(start + bld.t[k + 2]);
                    mb.t.Add(start + bld.t[k + 1]);
                }
            }
            var block = new MaterialPropertyBlock();
            var buildings = new GameObject("Buildings").transform;
            buildings.SetParent(parent, false);
            foreach (var kv in chunks)
                Emit(buildings, $"Buildings_{kv.Key.Item1.x}_{kv.Key.Item1.y}_{kv.Key.Item2}", kv.Value.ToMesh("KochiBuildings"), BuildingColors[kv.Key.Item2], true, block);
        }

        public static Texture2D MinimapTexture() => Resources.Load<Texture2D>(Resource + "minimap");

        public static Texture2D Blip(Color color, bool arrow)
        {
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                    bool inside = arrow ? v > -0.8f && Mathf.Abs(u) < (0.9f - v) * 0.5f && !(v < -0.3f && Mathf.Abs(u) < (-0.3f - v) * 0.9f)
                                        : u * u + v * v < 0.8f;
                    bool edge = arrow ? false : u * u + v * v > 0.55f;
                    tex.SetPixel(x, y, inside ? (edge ? Color.black : color) : Color.clear);
                }
            tex.Apply();
            return tex;
        }
    }
}
