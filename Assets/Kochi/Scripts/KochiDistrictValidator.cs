using System.Collections.Generic;
using UnityEngine;

namespace Kochi.World
{
    /// <summary>Sanity checks for district data, run before building and by edit-mode tests.</summary>
    public static class KochiDistrictValidator
    {
        public static List<string> Validate(KochiDistrictData d)
        {
            var errors = new List<string>();
            if (d == null)
            {
                errors.Add("District data is null.");
                return errors;
            }

            if (string.IsNullOrWhiteSpace(d.id))
                errors.Add("District id is empty.");
            if (d.blocksX < 1 || d.blocksZ < 1)
                errors.Add($"District needs at least 1x1 blocks (got {d.blocksX}x{d.blocksZ}).");
            if (d.roadWidth <= 0f || d.blockSize <= 0f)
                errors.Add("roadWidth and blockSize must be positive.");
            if (d.lotWidth <= 0f || d.lotDepth <= 0f || d.floorHeight <= 0f)
                errors.Add("lotWidth, lotDepth and floorHeight must be positive.");
            if (d.minFloors < 1 || d.maxFloors < d.minFloors)
                errors.Add($"Invalid floor range {d.minFloors}..{d.maxFloors}.");
            if (errors.Count > 0)
                return errors;

            var pieces = KochiDistrictLayout.Build(d);
            var roads = pieces.FindAll(p => p.kind == KochiPieceKind.Road);
            var solids = pieces.FindAll(p => p.solid && (p.kind == KochiPieceKind.Building || p.kind == KochiPieceKind.Prop));

            foreach (var building in pieces.FindAll(p => p.kind == KochiPieceKind.Building))
            {
                Rect footprint = KochiDistrictLayout.GetFootprint(building);
                foreach (var road in roads)
                {
                    if (KochiDistrictLayout.GetFootprint(road).Overlaps(footprint))
                    {
                        errors.Add($"{building.name} overlaps {road.name}.");
                        break;
                    }
                }
            }

            KochiDistrictLayout.GetPlayRect(d, out float minX, out float maxX, out float minZ, out float maxZ);
            var playRect = Rect.MinMaxRect(minX, minZ, maxX, maxZ);

            if (d.playerSpawn == null)
            {
                errors.Add("playerSpawn is missing.");
            }
            else
            {
                CheckSpawn(errors, "playerSpawn", d.playerSpawn.position, 0.5f, playRect, solids);
            }

            if (d.vehicleSpawns != null)
            {
                for (int i = 0; i < d.vehicleSpawns.Length; i++)
                {
                    Vector3 pos = d.vehicleSpawns[i].position;
                    CheckSpawn(errors, $"vehicleSpawns[{i}]", pos, 2.5f, playRect, solids);
                    if (!KochiDistrictLayout.IsOnRoad(d, pos))
                        errors.Add($"vehicleSpawns[{i}] at {pos} is not on a road.");
                }
            }

            return errors;
        }

        static void CheckSpawn(List<string> errors, string label, Vector3 position, float radius, Rect playRect, List<KochiPiece> solids)
        {
            var spawnRect = new Rect(position.x - radius, position.z - radius, radius * 2f, radius * 2f);
            if (!playRect.Contains(new Vector2(position.x, position.z)))
                errors.Add($"{label} at {position} is outside the district.");
            foreach (var solid in solids)
            {
                if (KochiDistrictLayout.GetFootprint(solid).Overlaps(spawnRect))
                {
                    errors.Add($"{label} at {position} overlaps {solid.name}.");
                    break;
                }
            }
        }
    }
}
