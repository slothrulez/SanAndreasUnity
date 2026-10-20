using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Kochi.World
{
    public enum KochiPieceKind
    {
        Ground,
        Seabed,
        Water,
        Road,
        LaneMarking,
        Building,
        Prop,
        Boundary,
    }

    public enum KochiShape
    {
        Box,
        Cylinder,
    }

    public struct KochiPiece
    {
        public string name;
        public KochiPieceKind kind;
        public KochiShape shape;
        public Vector3 center;
        public Vector3 size;
        public float rotationY;
        public Color color;
        public bool solid;
        public bool visible;
    }

    /// <summary>
    /// Converts <see cref="KochiDistrictData"/> into a flat list of placeholder pieces.
    /// Pure layout logic (no GameObjects) so it can be validated and tested without a scene.
    /// Roads run along grid lines at multiples of <see cref="Pitch"/>; blocks sit between them.
    /// </summary>
    public static class KochiDistrictLayout
    {
        public const float RoadThickness = 0.04f;
        public const float BoundaryHeight = 30f;

        public static float Pitch(KochiDistrictData d) => d.blockSize + d.roadWidth;

        public static float ExtentX(KochiDistrictData d) => d.blocksX * Pitch(d);

        public static float ExtentZ(KochiDistrictData d) => d.blocksZ * Pitch(d);

        public static List<KochiPiece> Build(KochiDistrictData d)
        {
            var pieces = new List<KochiPiece>();
            var random = new System.Random(d.seed);
            float pitch = Pitch(d);
            float halfRoad = d.roadWidth * 0.5f;
            float extentZ = ExtentZ(d);

            GetLandRect(d, out float landMinX, out float landMaxX, out float landMinZ, out float landMaxZ);
            Color groundColor = ParseColor(d.groundColor, new Color(0.42f, 0.56f, 0.24f));
            Color roadColor = ParseColor(d.roadColor, new Color(0.23f, 0.23f, 0.24f));

            pieces.Add(Box("Ground", KochiPieceKind.Ground,
                new Vector3((landMinX + landMaxX) * 0.5f, -0.25f, (landMinZ + landMaxZ) * 0.5f),
                new Vector3(landMaxX - landMinX, 0.5f, landMaxZ - landMinZ), groundColor));

            GetPlayRect(d, out float playMinX, out float playMaxX, out float playMinZ, out float playMaxZ);

            if (TryGetWaterRect(d, out float wMinX, out float wMaxX, out float wMinZ, out float wMaxZ))
            {
                pieces.Add(Box("Seabed", KochiPieceKind.Seabed,
                    new Vector3((wMinX + wMaxX) * 0.5f, -3.25f, (wMinZ + wMaxZ) * 0.5f),
                    new Vector3(wMaxX - wMinX, 0.5f, wMaxZ - wMinZ), new Color(0.36f, 0.31f, 0.22f)));
                var water = Box("Water", KochiPieceKind.Water,
                    new Vector3((wMinX + wMaxX) * 0.5f, -0.65f, (wMinZ + wMaxZ) * 0.5f),
                    new Vector3(wMaxX - wMinX, 0.1f, wMaxZ - wMinZ), new Color(0.16f, 0.42f, 0.52f));
                water.solid = false;
                pieces.Add(water);
            }

            // North-south roads run the full length; east-west roads are split at junctions
            // so that road surfaces never overlap (avoids z-fighting at intersections).
            for (int i = 0; i <= d.blocksX; i++)
            {
                float x = i * pitch;
                float length = extentZ + d.roadWidth;
                pieces.Add(Box($"Road_NS_{i}", KochiPieceKind.Road,
                    new Vector3(x, RoadThickness * 0.5f - 0.02f, extentZ * 0.5f),
                    new Vector3(d.roadWidth, RoadThickness, length), roadColor));
                AddLaneDashes(pieces, d, new Vector3(x, 0, -halfRoad), Vector3.forward, length, $"Road_NS_{i}");
            }

            for (int j = 0; j <= d.blocksZ; j++)
            {
                float z = j * pitch;
                for (int i = 0; i < d.blocksX; i++)
                {
                    float startX = i * pitch + halfRoad;
                    float length = d.blockSize;
                    pieces.Add(Box($"Road_EW_{j}_{i}", KochiPieceKind.Road,
                        new Vector3(startX + length * 0.5f, RoadThickness * 0.5f - 0.02f, z),
                        new Vector3(length, RoadThickness, d.roadWidth), roadColor));
                    AddLaneDashes(pieces, d, new Vector3(startX, 0, z), Vector3.right, length, $"Road_EW_{j}_{i}");
                }
            }

            var reserved = new List<Rect>();
            if (d.props != null)
            {
                foreach (var prop in d.props)
                {
                    pieces.Add(new KochiPiece
                    {
                        name = prop.name,
                        kind = KochiPieceKind.Prop,
                        shape = string.Equals(prop.shape, "cylinder", StringComparison.OrdinalIgnoreCase) ? KochiShape.Cylinder : KochiShape.Box,
                        center = prop.position,
                        size = prop.size,
                        rotationY = prop.rotationY,
                        color = ParseColor(prop.color, Color.white),
                        solid = prop.solid,
                        visible = true,
                    });
                    reserved.Add(GetFootprint(prop.position, prop.size, prop.rotationY));
                }
            }

            Color[] palette = ParsePalette(d.buildingPalette);
            for (int bx = 0; bx < d.blocksX; bx++)
            {
                for (int bz = 0; bz < d.blocksZ; bz++)
                {
                    AddBlockBuildings(pieces, d, bx, bz, random, palette, reserved);
                }
            }

            AddBoundaries(pieces, playMinX, playMaxX, playMinZ, playMaxZ);

            return pieces;
        }

        static void AddLaneDashes(List<KochiPiece> pieces, KochiDistrictData d, Vector3 start, Vector3 direction, float length, string roadName)
        {
            const float dashLength = 3f;
            const float dashSpacing = 6f;
            float pitch = Pitch(d);
            float clearance = d.roadWidth * 0.5f + 1f;
            int index = 0;
            for (float t = dashSpacing * 0.5f; t + dashLength <= length; t += dashSpacing)
            {
                Vector3 center = start + direction * (t + dashLength * 0.5f);
                float along = direction == Vector3.forward ? center.z : center.x;
                float nearestJunction = Mathf.Round(along / pitch) * pitch;
                if (Mathf.Abs(along - nearestJunction) < clearance + dashLength * 0.5f)
                    continue;

                Vector3 size = direction == Vector3.forward
                    ? new Vector3(0.15f, 0.01f, dashLength)
                    : new Vector3(dashLength, 0.01f, 0.15f);
                var dash = Box($"{roadName}_Dash_{index++}", KochiPieceKind.LaneMarking,
                    new Vector3(center.x, RoadThickness - 0.02f + 0.005f, center.z), size, new Color(0.92f, 0.92f, 0.86f));
                dash.solid = false;
                pieces.Add(dash);
            }
        }

        static void AddBlockBuildings(List<KochiPiece> pieces, KochiDistrictData d, int bx, int bz, System.Random random, Color[] palette, List<Rect> reserved)
        {
            GetBlockBuildableRect(d, bx, bz, out float minX, out float maxX, out float minZ, out float maxZ);
            float width = maxX - minX;
            float depth = maxZ - minZ;
            if (width < d.lotWidth || depth < d.lotDepth * 2f)
                return;

            int index = 0;

            // Rows facing the east-west roads (south and north edges of the block).
            int countX = Mathf.Max(1, Mathf.FloorToInt(width / d.lotWidth));
            float lotX = width / countX;
            for (int i = 0; i < countX; i++)
            {
                float cx = minX + (i + 0.5f) * lotX;
                TryAddBuilding(pieces, d, random, palette, reserved, $"Building_{bx}_{bz}_{index++}",
                    new Vector3(cx, 0, minZ + d.lotDepth * 0.5f), lotX - d.lotGap, d.lotDepth);
                TryAddBuilding(pieces, d, random, palette, reserved, $"Building_{bx}_{bz}_{index++}",
                    new Vector3(cx, 0, maxZ - d.lotDepth * 0.5f), lotX - d.lotGap, d.lotDepth);
            }

            // Columns facing the north-south roads, between the two rows.
            float spanZ = depth - d.lotDepth * 2f;
            int countZ = Mathf.FloorToInt(spanZ / d.lotWidth);
            if (countZ <= 0 || width < d.lotDepth * 2f)
                return;
            float lotZ = spanZ / countZ;
            for (int i = 0; i < countZ; i++)
            {
                float cz = minZ + d.lotDepth + (i + 0.5f) * lotZ;
                TryAddBuilding(pieces, d, random, palette, reserved, $"Building_{bx}_{bz}_{index++}",
                    new Vector3(minX + d.lotDepth * 0.5f, 0, cz), d.lotDepth, lotZ - d.lotGap);
                TryAddBuilding(pieces, d, random, palette, reserved, $"Building_{bx}_{bz}_{index++}",
                    new Vector3(maxX - d.lotDepth * 0.5f, 0, cz), d.lotDepth, lotZ - d.lotGap);
            }
        }

        static void TryAddBuilding(List<KochiPiece> pieces, KochiDistrictData d, System.Random random, Color[] palette,
            List<Rect> reserved, string name, Vector3 groundCenter, float sizeX, float sizeZ)
        {
            int floors = random.Next(d.minFloors, Mathf.Max(d.minFloors, d.maxFloors) + 1);
            Color color = palette[random.Next(palette.Length)];

            var footprint = new Rect(groundCenter.x - sizeX * 0.5f, groundCenter.z - sizeZ * 0.5f, sizeX, sizeZ);
            foreach (var r in reserved)
            {
                if (r.Overlaps(footprint))
                    return;
            }

            float height = floors * d.floorHeight;
            pieces.Add(Box(name, KochiPieceKind.Building,
                new Vector3(groundCenter.x, height * 0.5f, groundCenter.z),
                new Vector3(sizeX, height, sizeZ), color));
        }

        static void AddBoundaries(List<KochiPiece> pieces, float minX, float maxX, float minZ, float maxZ)
        {
            const float t = 1f;
            float h = BoundaryHeight;
            float cx = (minX + maxX) * 0.5f;
            float cz = (minZ + maxZ) * 0.5f;
            float sx = maxX - minX + 2 * t;
            float sz = maxZ - minZ + 2 * t;
            AddBoundary(pieces, "Boundary_West", new Vector3(minX - t * 0.5f, h * 0.5f - 4f, cz), new Vector3(t, h, sz));
            AddBoundary(pieces, "Boundary_East", new Vector3(maxX + t * 0.5f, h * 0.5f - 4f, cz), new Vector3(t, h, sz));
            AddBoundary(pieces, "Boundary_South", new Vector3(cx, h * 0.5f - 4f, minZ - t * 0.5f), new Vector3(sx, h, t));
            AddBoundary(pieces, "Boundary_North", new Vector3(cx, h * 0.5f - 4f, maxZ + t * 0.5f), new Vector3(sx, h, t));
        }

        static void AddBoundary(List<KochiPiece> pieces, string name, Vector3 center, Vector3 size)
        {
            var piece = Box(name, KochiPieceKind.Boundary, center, size, Color.clear);
            piece.visible = false;
            pieces.Add(piece);
        }

        /// <summary>Area inside the outer roads that lots can occupy for one block.</summary>
        public static void GetBlockBuildableRect(KochiDistrictData d, int bx, int bz, out float minX, out float maxX, out float minZ, out float maxZ)
        {
            float pitch = Pitch(d);
            float inset = d.roadWidth * 0.5f + d.buildingSetback;
            minX = bx * pitch + inset;
            maxX = (bx + 1) * pitch - inset;
            minZ = bz * pitch + inset;
            maxZ = (bz + 1) * pitch - inset;
        }

        /// <summary>Land area: the road grid plus half a block of margin, cut short on the water side by the quay.</summary>
        public static void GetLandRect(KochiDistrictData d, out float minX, out float maxX, out float minZ, out float maxZ)
        {
            float margin = d.blockSize * 0.5f;
            float edge = d.roadWidth * 0.5f + d.quayWidth;
            minX = -margin;
            maxX = ExtentX(d) + margin;
            minZ = -margin;
            maxZ = ExtentZ(d) + margin;
            switch (NormalizedWaterSide(d))
            {
                case "west": minX = -edge; break;
                case "east": maxX = ExtentX(d) + edge; break;
                case "south": minZ = -edge; break;
                case "north": maxZ = ExtentZ(d) + edge; break;
            }
        }

        public static bool TryGetWaterRect(KochiDistrictData d, out float minX, out float maxX, out float minZ, out float maxZ)
        {
            GetLandRect(d, out float lMinX, out float lMaxX, out float lMinZ, out float lMaxZ);
            minX = lMinX; maxX = lMaxX; minZ = lMinZ; maxZ = lMaxZ;
            switch (NormalizedWaterSide(d))
            {
                case "west": maxX = lMinX; minX = lMinX - d.waterWidth; return true;
                case "east": minX = lMaxX; maxX = lMaxX + d.waterWidth; return true;
                case "south": maxZ = lMinZ; minZ = lMinZ - d.waterWidth; return true;
                case "north": minZ = lMaxZ; maxZ = lMaxZ + d.waterWidth; return true;
                default: return false;
            }
        }

        /// <summary>Walkable/drivable area enclosed by invisible boundary walls (land plus water).</summary>
        public static void GetPlayRect(KochiDistrictData d, out float minX, out float maxX, out float minZ, out float maxZ)
        {
            GetLandRect(d, out minX, out maxX, out minZ, out maxZ);
            if (TryGetWaterRect(d, out float wMinX, out float wMaxX, out float wMinZ, out float wMaxZ))
            {
                minX = Mathf.Min(minX, wMinX);
                maxX = Mathf.Max(maxX, wMaxX);
                minZ = Mathf.Min(minZ, wMinZ);
                maxZ = Mathf.Max(maxZ, wMaxZ);
            }
        }

        public static bool IsOnRoad(KochiDistrictData d, Vector3 position)
        {
            float pitch = Pitch(d);
            float halfRoad = d.roadWidth * 0.5f;
            bool withinZ = position.z >= -halfRoad && position.z <= ExtentZ(d) + halfRoad;
            bool withinX = position.x >= -halfRoad && position.x <= ExtentX(d) + halfRoad;
            for (int i = 0; i <= d.blocksX; i++)
            {
                if (withinZ && Mathf.Abs(position.x - i * pitch) <= halfRoad)
                    return true;
            }
            for (int j = 0; j <= d.blocksZ; j++)
            {
                if (withinX && Mathf.Abs(position.z - j * pitch) <= halfRoad)
                    return true;
            }
            return false;
        }

        /// <summary>Axis-aligned XZ footprint of a (possibly rotated) box.</summary>
        public static Rect GetFootprint(Vector3 center, Vector3 size, float rotationY)
        {
            float rad = rotationY * Mathf.Deg2Rad;
            float cos = Mathf.Abs(Mathf.Cos(rad));
            float sin = Mathf.Abs(Mathf.Sin(rad));
            float w = size.x * cos + size.z * sin;
            float h = size.x * sin + size.z * cos;
            return new Rect(center.x - w * 0.5f, center.z - h * 0.5f, w, h);
        }

        public static Rect GetFootprint(KochiPiece piece) => GetFootprint(piece.center, piece.size, piece.rotationY);

        static string NormalizedWaterSide(KochiDistrictData d) => (d.waterSide ?? string.Empty).Trim().ToLowerInvariant();

        static KochiPiece Box(string name, KochiPieceKind kind, Vector3 center, Vector3 size, Color color)
        {
            return new KochiPiece
            {
                name = name,
                kind = kind,
                shape = KochiShape.Box,
                center = center,
                size = size,
                color = color,
                solid = true,
                visible = true,
            };
        }

        static Color[] ParsePalette(string[] hexColors)
        {
            var colors = new List<Color>();
            if (hexColors != null)
            {
                foreach (var hex in hexColors)
                {
                    if (TryParseColor(hex, out Color c))
                        colors.Add(c);
                }
            }
            if (colors.Count == 0)
                colors.Add(new Color(0.95f, 0.92f, 0.85f));
            return colors.ToArray();
        }

        public static Color ParseColor(string hex, Color fallback) => TryParseColor(hex, out Color c) ? c : fallback;

        /// <summary>Parses "#RRGGBB" or "#RRGGBBAA".</summary>
        public static bool TryParseColor(string hex, out Color color)
        {
            color = default;
            if (string.IsNullOrEmpty(hex))
                return false;
            string s = hex.Trim().TrimStart('#');
            if (s.Length != 6 && s.Length != 8)
                return false;
            if (!uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint value))
                return false;
            if (s.Length == 6)
                value = (value << 8) | 0xFF;
            color = new Color(
                ((value >> 24) & 0xFF) / 255f,
                ((value >> 16) & 0xFF) / 255f,
                ((value >> 8) & 0xFF) / 255f,
                (value & 0xFF) / 255f);
            return true;
        }
    }
}
