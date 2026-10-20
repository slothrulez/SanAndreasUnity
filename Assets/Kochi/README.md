# Kochi prototype

An original, GTA-independent playable slice. It uses only primitive placeholder
geometry and needs no GTA San Andreas files. It is separate from the original
`SanAndreasUnity` loader: the `Kochi.Runtime` assembly references nothing else in the project.

## Run
Open `Assets/Kochi/Scenes/KochiPrototype.unity` in Unity 2020.3.26f1 and press Play,
or choose **Kochi Prototype** in the main menu of `Startup.unity`.

| Action | Keys |
| --- | --- |
| Walk / run / jump | WASD / Shift / Space |
| Camera | Mouse, scroll to zoom |
| Enter / exit nearest car | F or Enter |
| Drive / handbrake | WASD / Space |
| Reload district JSON | F5 |
| Release / lock cursor | Esc / left click |

## Add or extend a district
1. Copy `Resources/Kochi/Districts/fort_kochi_test.json` to `<id>.json` in the same folder.
2. Edit the grid (`blocksX`, `blocksZ`, `blockSize`, `roadWidth`), building ranges and palette,
   `waterSide`, `props` (landmarks), `playerSpawn` and `vehicleSpawns`.
3. Set `KochiGame.districtResourcePath` to `Kochi/Districts/<id>`, or pass `-kochiDistrict <id>`
   on the player command line.
4. Validate: `UNITY_EDITOR_DIR=/path/to/Editor Tools/KochiVerify/verify.sh Assets/Kochi/Resources/Kochi/Districts/<id>.json`
   (Mono only, no Unity license needed), and run the `Kochi.Tests.Editor` edit-mode tests in Unity.

Layout generation (`KochiDistrictLayout`) is pure data, so it can be tested without a scene.
`KochiDistrictBuilder` converts it into GameObjects.
