using System;
using System.IO;
using System.Linq;
using Kochi.World;
using Newtonsoft.Json;

static class Program
{
    static int Main(string[] args)
    {
        var data = JsonConvert.DeserializeObject<KochiDistrictData>(File.ReadAllText(args[0]),
            new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error });
        var pieces = KochiDistrictLayout.Build(data);
        foreach (var g in pieces.GroupBy(p => p.kind))
            Console.WriteLine($"{g.Key}: {g.Count()}");
        var errors = KochiDistrictValidator.Validate(data);
        foreach (var e in errors) Console.WriteLine("ERROR: " + e);
        // Same checks as the edit-mode tests.
        var a = KochiDistrictLayout.Build(data).Select(p => (p.name, p.center, p.size)).ToList();
        var b = KochiDistrictLayout.Build(data).Select(p => (p.name, p.center, p.size)).ToList();
        Console.WriteLine("deterministic: " + a.SequenceEqual(b));
        var grid = new KochiDistrictData { blocksX = 2, blocksZ = 3 };
        Console.WriteLine("road segments 2x3: " + KochiDistrictLayout.Build(grid).Count(p => p.kind == KochiPieceKind.Road) + " (expected 11)");
        var reserved = new KochiDistrictData { blocksX = 1, blocksZ = 1, waterSide = "", props = new[] { new KochiPropData { name = "Big", position = new UnityEngine.Vector3(35, 5, 35), size = new UnityEngine.Vector3(60, 10, 60) } } };
        Console.WriteLine("buildings under big prop: " + KochiDistrictLayout.Build(reserved).Count(p => p.kind == KochiPieceKind.Building) + " (expected 0)");
        KochiDistrictLayout.TryParseColor("#FF8000", out var c);
        Console.WriteLine($"color: {c.r} {c.g:0.0000} {c.b} invalid-rejected: {!KochiDistrictLayout.TryParseColor("orange", out _)}");
        Console.WriteLine("spawn on road: " + KochiDistrictLayout.IsOnRoad(data, data.vehicleSpawns[0].position));
        return errors.Count == 0 ? 0 : 1;
    }
}
