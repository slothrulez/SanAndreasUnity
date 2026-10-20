using System;
using UnityEngine;

namespace Kochi.World
{
    /// <summary>
    /// Data-driven description of one district. Loaded from a JSON TextAsset (JsonUtility format),
    /// so a new district can be added by copying a JSON file instead of editing code or scenes.
    /// </summary>
    [Serializable]
    public class KochiDistrictData
    {
        public string id = "district";
        public string displayName = "District";
        public int seed = 1;

        public int blocksX = 3;
        public int blocksZ = 3;
        public float blockSize = 60f;
        public float roadWidth = 10f;

        public float buildingSetback = 3f;
        public float lotWidth = 12f;
        public float lotDepth = 14f;
        public float lotGap = 1.5f;
        public int minFloors = 2;
        public int maxFloors = 6;
        public float floorHeight = 3.2f;
        public string[] buildingPalette = { "#F2EBD9", "#C0583A", "#3F8F8A", "#E0B341" };

        public string groundColor = "#6B8E3D";
        public string roadColor = "#3A3A3C";

        /// <summary>Side of the grid that faces the water: "west", "east", "north", "south" or empty for none.</summary>
        public string waterSide = "west";
        public float waterWidth = 120f;
        public float quayWidth = 4f;

        public KochiPropData[] props = new KochiPropData[0];
        public KochiSpawnData playerSpawn = new KochiSpawnData();
        public KochiSpawnData[] vehicleSpawns = new KochiSpawnData[0];
    }

    [Serializable]
    public class KochiPropData
    {
        public string name = "Prop";
        /// <summary>"box" or "cylinder".</summary>
        public string shape = "box";
        /// <summary>World-space center of the shape.</summary>
        public Vector3 position;
        public Vector3 size = Vector3.one;
        public float rotationY;
        public string color = "#FFFFFF";
        public bool solid = true;
        /// <summary>Landmarks are shown on the HUD when the player is close to them.</summary>
        public bool isLandmark;
    }

    [Serializable]
    public class KochiSpawnData
    {
        public Vector3 position;
        public float rotationY;
        public string color = "#FFFFFF";
    }
}
