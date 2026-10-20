using System.Linq;
using Kochi.World;
using NUnit.Framework;
using UnityEngine;

namespace Kochi.Tests
{
    public class KochiDistrictLayoutTests
    {
        static KochiDistrictData LoadTestDistrict()
        {
            var asset = Resources.Load<TextAsset>(KochiDistrictPaths.TestDistrict);
            Assert.IsNotNull(asset, $"Missing district resource {KochiDistrictPaths.TestDistrict}");
            return JsonUtility.FromJson<KochiDistrictData>(asset.text);
        }

        [Test]
        public void TestDistrictHasNoValidationErrors()
        {
            var errors = KochiDistrictValidator.Validate(LoadTestDistrict());
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void LayoutIsDeterministicForSeed()
        {
            var data = LoadTestDistrict();
            var first = KochiDistrictLayout.Build(data).Select(p => (p.name, p.center, p.size)).ToList();
            var second = KochiDistrictLayout.Build(data).Select(p => (p.name, p.center, p.size)).ToList();
            CollectionAssert.AreEqual(first, second);
        }

        [Test]
        public void GridProducesExpectedRoadSegments()
        {
            var data = new KochiDistrictData { blocksX = 2, blocksZ = 3 };
            var roads = KochiDistrictLayout.Build(data).Where(p => p.kind == KochiPieceKind.Road).ToList();
            // (blocksX + 1) full north-south roads + (blocksZ + 1) * blocksX east-west segments.
            Assert.AreEqual(3 + 4 * 2, roads.Count);
        }

        [Test]
        public void PropsReserveSpaceFromBuildings()
        {
            var data = new KochiDistrictData
            {
                blocksX = 1,
                blocksZ = 1,
                waterSide = "",
                props = new[] { new KochiPropData { name = "Big", position = new Vector3(35, 5, 35), size = new Vector3(60, 10, 60) } },
            };
            var buildings = KochiDistrictLayout.Build(data).Where(p => p.kind == KochiPieceKind.Building);
            Assert.IsEmpty(buildings);
        }

        [Test]
        public void ParsesHexColors()
        {
            Assert.IsTrue(KochiDistrictLayout.TryParseColor("#FF8000", out Color c));
            Assert.AreEqual(1f, c.r, 1e-4f);
            Assert.AreEqual(128f / 255f, c.g, 1e-4f);
            Assert.AreEqual(0f, c.b, 1e-4f);
            Assert.IsFalse(KochiDistrictLayout.TryParseColor("orange", out _));
        }
    }
}
