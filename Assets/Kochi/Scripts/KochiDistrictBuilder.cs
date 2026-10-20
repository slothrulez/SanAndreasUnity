using System.Collections.Generic;
using UnityEngine;

namespace Kochi.World
{
    /// <summary>Instantiates placeholder geometry (primitives with colliders) for a district.</summary>
    public static class KochiDistrictBuilder
    {
        static readonly int ColorId = Shader.PropertyToID("_Color");

        public static KochiDistrictData LoadFromResources(string resourcePath)
        {
            var textAsset = Resources.Load<TextAsset>(resourcePath);
            if (textAsset == null)
                throw new System.IO.FileNotFoundException($"District data not found in Resources: '{resourcePath}'");
            return JsonUtility.FromJson<KochiDistrictData>(textAsset.text);
        }

        public static GameObject Build(KochiDistrictData data)
        {
            foreach (var error in KochiDistrictValidator.Validate(data))
                Debug.LogWarning($"[Kochi] District '{data?.id}': {error}");

            var root = new GameObject($"District_{data.id}");
            var groups = new Dictionary<KochiPieceKind, Transform>();
            var propertyBlock = new MaterialPropertyBlock();

            foreach (var piece in KochiDistrictLayout.Build(data))
            {
                if (!groups.TryGetValue(piece.kind, out Transform group))
                {
                    group = new GameObject(piece.kind.ToString()).transform;
                    group.SetParent(root.transform, false);
                    groups.Add(piece.kind, group);
                }

                CreatePiece(piece, group, propertyBlock);
            }

            return root;
        }

        public static GameObject CreatePiece(KochiPiece piece, Transform parent, MaterialPropertyBlock propertyBlock)
        {
            bool isCylinder = piece.shape == KochiShape.Cylinder;
            var go = GameObject.CreatePrimitive(isCylinder ? PrimitiveType.Cylinder : PrimitiveType.Cube);
            go.name = piece.name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = piece.center;
            go.transform.localRotation = Quaternion.Euler(0f, piece.rotationY, 0f);
            // Unity's cylinder primitive is 2 units tall.
            go.transform.localScale = isCylinder
                ? new Vector3(piece.size.x, piece.size.y * 0.5f, piece.size.z)
                : piece.size;

            if (!piece.solid)
                Object.DestroyImmediate(go.GetComponent<Collider>());

            var renderer = go.GetComponent<MeshRenderer>();
            if (!piece.visible)
            {
                Object.DestroyImmediate(renderer);
                Object.DestroyImmediate(go.GetComponent<MeshFilter>());
            }
            else
            {
                propertyBlock.Clear();
                propertyBlock.SetColor(ColorId, piece.color);
                renderer.SetPropertyBlock(propertyBlock);
            }

            return go;
        }
    }
}
