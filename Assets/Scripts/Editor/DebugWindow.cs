using System;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SurvivalRts.Editor
{
    // Cửa sổ và menu công cụ kiểm chứng nhanh trong Editor.
    public class DebugWindow : EditorWindow
    {
        const string TerrainAssetFolder = "Assets/Art/Terrain";
        const string TerrainDataPath = TerrainAssetFolder + "/PrototypeTerrainData.asset";
        const string GameplayPrefabFolder = "Assets/Prefabs/Gameplay";

        Vector2 scrollPos;

        [MenuItem("Debug/Mở bảng Debug %#t")]
        public static void OpenWindow()
        {
            var window = GetWindow<DebugWindow>("Debug");
            window.minSize = new Vector2(380f, 480f);
            window.Show();
        }

        [MenuItem("Debug/Kiểm chứng xoay công trình")]
        public static void VerifyBuildingRotation()
        {
            int trigMismatch = 0;
            for (int deg = 0; deg < 360; deg++)
            {
                int expectedCos = (int)Math.Round(Math.Cos(deg * Math.PI / 180.0) * (1 << BuildGrid.TrigShift));
                int expectedSin = (int)Math.Round(Math.Sin(deg * Math.PI / 180.0) * (1 << BuildGrid.TrigShift));
                if (BuildGrid.Cos(deg) != expectedCos || BuildGrid.Sin(deg) != expectedSin)
                    trigMismatch++;
            }

            Vector2Int[] footprints =
            {
                new Vector2Int(1, 1),
                new Vector2Int(2, 2),
                new Vector2Int(3, 3),
                new Vector2Int(2, 3),
                new Vector2Int(1, 4)
            };
            int[] cardinalAngles = { 0, 90, 180, 270 };

            var actual = new List<Vector2Int>();
            var expected = new List<Vector2Int>();
            int totalCases = 0;
            int passedCases = 0;

            for (int f = 0; f < footprints.Length; f++)
            {
                var baseFp = footprints[f];
                for (int a = 0; a < cardinalAngles.Length; a++)
                {
                    int angle = cardinalAngles[a];
                    int turns = ((angle + 45) / 90) & 3;
                    var rotFp = BuildGrid.RotatedFootprint(baseFp, turns);

                    // Tâm đã snap theo chẵn/lẻ của rotFp quanh ô (10, 10).
                    long cx = (10 * 2 + rotFp.x) * (BuildGrid.SubCell / 2);
                    long cz = (10 * 2 + rotFp.y) * (BuildGrid.SubCell / 2);

                    BuildGrid.CollectCellCoords(cx, cz, baseFp, angle, actual);

                    expected.Clear();
                    for (int z = 10; z < 10 + rotFp.y; z++)
                    {
                        for (int x = 10; x < 10 + rotFp.x; x++)
                            expected.Add(new Vector2Int(x, z));
                    }

                    totalCases++;
                    bool match = actual.Count == expected.Count;
                    if (match)
                    {
                        for (int i = 0; i < actual.Count; i++)
                        {
                            if (actual[i] != expected[i])
                            {
                                match = false;
                                break;
                            }
                        }
                    }

                    if (match)
                    {
                        passedCases++;
                    }
                    else
                    {
                        Debug.LogError($"[DebugWindow] Lệch CollectCells tại footprint {baseFp} góc {angle}°: thực tế {actual.Count} ô, kỳ vọng {expected.Count} ô.");
                    }
                }
            }

            BuildGrid.CollectCellCoords(10 * BuildGrid.SubCell, 10 * BuildGrid.SubCell, new Vector2Int(2, 2), 37, actual);
            int count2x2At37 = actual.Count;
            BuildGrid.CollectCellCoords(10 * BuildGrid.SubCell + 128, 10 * BuildGrid.SubCell + 128, new Vector2Int(3, 3), 135, actual);
            int count3x3At135 = actual.Count;

            Debug.Log($"[DebugWindow] Kiểm chứng xoay công trình hoàn tất: Bảng lượng giác lệch={trigMismatch}/360, Góc vuông khớp={passedCases}/{totalCases} (2x2@37°={count2x2At37} ô, 3x3@135°={count3x3At135} ô).");
        }

        [MenuItem("Debug/Thiết lập Terrain và HeightField cho scene")]
        public static void SetupSceneTerrainAndHeightField()
        {
            EnsureFolder("Assets/Art");
            EnsureFolder(TerrainAssetFolder);
            EnsureFolder("Assets/Prefabs");
            EnsureFolder(GameplayPrefabFolder);

            var terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);
            if (terrainData == null)
            {
                terrainData = new TerrainData();
                terrainData.heightmapResolution = 257;
                terrainData.size = new Vector3(256f, 60f, 256f);
                terrainData.alphamapResolution = 128;
                terrainData.baseMapResolution = 128;
                AssetDatabase.CreateAsset(terrainData, TerrainDataPath);
            }
            else
            {
                terrainData.heightmapResolution = 257;
                terrainData.size = new Vector3(256f, 60f, 256f);
            }

            SetupGrayboxTerrainLayers(terrainData);

            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer < 0) groundLayer = 0;

            var heightField = UnityEngine.Object.FindFirstObjectByType<HeightField>();
            GameObject terrainGo;
            if (heightField != null)
            {
                terrainGo = heightField.gameObject;
            }
            else
            {
                terrainGo = GameObject.Find("Terrain");
                if (terrainGo == null) terrainGo = new GameObject("Terrain");
                heightField = terrainGo.GetComponent<HeightField>();
                if (heightField == null) heightField = terrainGo.AddComponent<HeightField>();
            }

            terrainGo.name = "Terrain";
            terrainGo.layer = groundLayer;
            terrainGo.transform.position = new Vector3(-128f, -10f, -128f);
            terrainGo.transform.rotation = Quaternion.identity;
            terrainGo.transform.localScale = Vector3.one;

            var terrain = terrainGo.GetComponent<Terrain>();
            if (terrain == null) terrain = terrainGo.AddComponent<Terrain>();
            var collider = terrainGo.GetComponent<TerrainCollider>();
            if (collider == null) collider = terrainGo.AddComponent<TerrainCollider>();
            terrain.terrainData = terrainData;
            collider.terrainData = terrainData;

            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/TerrainGraybox.mat");
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Terrain/Lit") ?? Shader.Find("Universal Render Pipeline/Lit");
                if (shader != null)
                {
                    EnsureFolder("Assets/Art/Materials");
                    mat = new Material(shader);
                    AssetDatabase.CreateAsset(mat, "Assets/Art/Materials/TerrainGraybox.mat");
                }
            }
            if (mat != null) terrain.materialTemplate = mat;

            heightField.UnityTerrain = terrain;
            heightField.ResizeMapChunks(new Vector2Int(20, 20));

            // Tạo dữ liệu độ cao số nguyên cho khu vực 50x50 quanh gốc (giữ nguyên các vùng dốc 10°, dốc 35°, bục cao, hồ nước).
            var minCell = new Vector2Int(-25, -25);
            var sizeCells = new Vector2Int(50, 50);
            var baked = BuildPrototypeHeights(minCell, sizeCells);
            heightField.SetBakedData(minCell, sizeCells, baked);
            heightField.SyncToUnityTerrain();
            PaintTerrainSplatmap(terrainData, heightField);

            // Tắt các khối mesh mặt đất cũ để chỉ dùng Unity Terrain hiển thị và bắt tia.
            var testArea = GameObject.Find("TestArea");
            if (testArea != null)
            {
                string[] oldGroundNames = { "Ground_50x50", "Slope_Gentle_10deg", "Slope_Steep_35deg", "Platform_High" };
                foreach (string childName in oldGroundNames)
                {
                    var child = testArea.transform.Find(childName);
                    if (child != null) child.gameObject.SetActive(false);
                }
            }

            // Liên kết BuildGrid với HeightField.
            var buildGrid = UnityEngine.Object.FindFirstObjectByType<BuildGrid>();
            if (buildGrid != null)
            {
                buildGrid.HeightField = heightField;
                EditorUtility.SetDirty(buildGrid);
            }

            // Gỡ NetworkManager trùng lặp nếu có trong scene.
            var netManagers = UnityEngine.Object.FindObjectsByType<FishNet.Managing.NetworkManager>(FindObjectsSortMode.None);
            if (netManagers.Length > 1)
            {
                for (int i = 1; i < netManagers.Length; i++)
                    UnityEngine.Object.DestroyImmediate(netManagers[i].gameObject);
            }

            // Lưu prefab Terrain vào Assets/Prefabs/Gameplay/Terrain.prefab theo quy tắc prefab.
            SaveGameplayPrefab(terrainGo, GameplayPrefabFolder + "/Terrain.prefab");
            if (buildGrid != null)
                SaveGameplayPrefab(buildGrid.gameObject, GameplayPrefabFolder + "/BuildGrid.prefab");

            // Nướng lại NavMesh trên vùng hoạt động để dân và lính di chuyển bình thường trên Terrain.
            var navSurface = UnityEngine.Object.FindFirstObjectByType<NavMeshSurface>();
            if (navSurface != null)
            {
                navSurface.collectObjects = CollectObjects.Volume;
                navSurface.center = new Vector3(0f, 2f, 0f);
                navSurface.size = new Vector3(80f, 30f, 80f);
                navSurface.BuildNavMesh();
                EditorUtility.SetDirty(navSurface);
            }

            EditorUtility.SetDirty(terrainData);
            EditorUtility.SetDirty(heightField);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            Debug.Log($"[DebugWindow] Đã thiết lập Unity Terrain & HeightField: MapSize={heightField.MapSizeCells} m ({heightField.MapChunks} chunk), AllocatedChunks={heightField.AllocatedChunkCount}, Hash=0x{heightField.ComputeDeterministicHash():X8}.");
        }

        [MenuItem("Debug/Kiểm chứng HeightField và Chunk")]
        public static void VerifyHeightFieldAndChunks()
        {
            var hf = UnityEngine.Object.FindFirstObjectByType<HeightField>();
            if (hf == null)
            {
                Debug.LogError("[DebugWindow] Không tìm thấy HeightField trong scene.");
                return;
            }

            hf.EnsureInitialized();
            uint hash1 = hf.ComputeDeterministicHash();

            // Kiểm tra tính tất định khi nạp lại lần 2.
            hf.EnsureInitialized();
            uint hash2 = hf.ComputeDeterministicHash();

            int flatH = hf.HeightAtWorldCell(0, 0);
            int gentleH = hf.HeightAtWorldCell(12, 12);
            bool gentleSteep = hf.IsSteepAtWorldCell(12, 12, 30);
            int steepH = hf.HeightAtWorldCell(12, -12);
            bool steepIsSteep = hf.IsSteepAtWorldCell(12, -12, 30);
            int platformH = hf.HeightAtWorldCell(-12, -12);
            bool platformSteep = hf.IsSteepAtWorldCell(-12, -12, 30);
            bool pondWater = hf.IsWaterAtWorldCell(-14, 12);

            // Kiểm tra đổi độ cao một vùng ở runtime và cập nhật version chunk.
            var chunk0 = hf.GetOrCreateChunk(10, 10);
            int vBefore = chunk0.Version;
            short oldVal = (short)hf.HeightAtWorldCell(2, 2);
            hf.SetWorldRegionHeights(2, 2, 2, 2, 128, syncTerrain: true);
            int vMid = chunk0.Version;
            int modH = hf.HeightAtWorldCell(2, 2);
            hf.SetWorldRegionHeights(2, 2, 2, 2, oldVal, syncTerrain: true);
            int vAfter = chunk0.Version;
            uint hashAfterRestore = hf.ComputeDeterministicHash();

            Debug.Log(
                $"[DebugWindow] Kiểm chứng HeightField: Map={hf.MapSizeCells.x}x{hf.MapSizeCells.y}m ({hf.MapChunks.x}x{hf.MapChunks.y} chunk), " +
                $"Allocated={hf.AllocatedChunkCount}, HashMatch={hash1 == hash2 && hash1 == hashAfterRestore} (0x{hash1:X8}), " +
                $"Flat(0,0)={flatH}, Gentle(12,12)={gentleH} (steep={gentleSteep}), Steep(12,-12)={steepH} (steep={steepIsSteep}), " +
                $"Platform(-12,-12)={platformH} (steep={platformSteep}), PondWater(-14,12)={pondWater}, " +
                $"ChunkVersion={vBefore}->{vMid}->{vAfter} (modH={modH}).");
        }

        [MenuItem("Debug/Cấu hình ngưỡng địa hình cho BuildingData")]
        public static void ConfigureBuildingDataSlopeThresholds()
        {
            string[] guids = AssetDatabase.FindAssets("t:BuildingData");
            int updated = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<BuildingData>(path);
                if (data == null) continue;

                int area = data.footprint.x * data.footprint.y;
                if (area >= 9)
                {
                    // Nhà lớn (3x3 trở lên như Trại lính) đòi hỏi nền phẳng hơn.
                    data.maxHeightDelta = 0.30f;
                    data.maxSlopeAngle = 22f;
                }
                else
                {
                    // Nhà nhỏ (1x1, 2x2, 1x4) đặt được trên dốc thoải 10°.
                    data.maxHeightDelta = 0.80f;
                    data.maxSlopeAngle = 30f;
                }
                EditorUtility.SetDirty(data);
                updated++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[DebugWindow] Đã cập nhật ngưỡng maxHeightDelta và maxSlopeAngle cho {updated} BuildingData.");
        }

        [MenuItem("Debug/Thiết lập ObjectPlacementData và ProceduralVegetation")]
        public static void SetupPlacementRulesAndVegetation()
        {
            EnsureFolder("Assets/Data");
            EnsureFolder("Assets/Data/Placement");

            string blockPath = "Assets/Data/Placement/LargeObstacle_Block.asset";
            var blockData = AssetDatabase.LoadAssetAtPath<ObjectPlacementData>(blockPath);
            if (blockData == null)
            {
                blockData = ScriptableObject.CreateInstance<ObjectPlacementData>();
                AssetDatabase.CreateAsset(blockData, blockPath);
            }
            blockData.displayName = "Vật cản lớn";
            blockData.placementRule = PlacementRule.Block;
            blockData.footprint = Vector2Int.one;
            blockData.clearable = true;
            blockData.clearRewardType = ResourceType.Wood;
            blockData.clearRewardAmount = 10;
            EditorUtility.SetDirty(blockData);

            string clearPath = "Assets/Data/Placement/SmallFoliage_Clear.asset";
            var clearData = AssetDatabase.LoadAssetAtPath<ObjectPlacementData>(clearPath);
            if (clearData == null)
            {
                clearData = ScriptableObject.CreateInstance<ObjectPlacementData>();
                AssetDatabase.CreateAsset(clearData, clearPath);
            }
            clearData.displayName = "Bụi cây nhỏ";
            clearData.placementRule = PlacementRule.Clear;
            clearData.footprint = Vector2Int.one;
            clearData.clearable = true;
            clearData.clearRewardType = ResourceType.Wood;
            clearData.clearRewardAmount = 2;
            EditorUtility.SetDirty(clearData);

            // Gán quy tắc đặt cho các vật thể trong TestArea của scene prototype.
            var occupants = UnityEngine.Object.FindObjectsByType<GridOccupant>(FindObjectsSortMode.None);
            int assignedBlock = 0, assignedClear = 0;
            foreach (var occ in occupants)
            {
                if (occ == null || occ.isBuilding) continue;
                if (occ.name.Contains("BerryBush") || occ.name.Contains("Bush"))
                {
                    occ.placementData = clearData;
                    assignedClear++;
                }
                else
                {
                    occ.placementData = blockData;
                    assignedBlock++;
                }
                EditorUtility.SetDirty(occ);
            }

            // Gắn ProceduralVegetation lên Terrain và lưu Prefab.
            var hf = UnityEngine.Object.FindFirstObjectByType<HeightField>();
            if (hf != null)
            {
                var veg = hf.gameObject.GetComponent<ProceduralVegetation>();
                if (veg == null) veg = hf.gameObject.AddComponent<ProceduralVegetation>();
                veg.BlockTreeData = blockData;
                veg.ClearBushData = clearData;
                EditorUtility.SetDirty(veg);
                SaveGameplayPrefab(hf.gameObject, GameplayPrefabFolder + "/Terrain.prefab");
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log($"[DebugWindow] Đã thiết lập ObjectPlacementData (Block={assignedBlock}, Clear={assignedClear}) và gắn ProceduralVegetation.");
        }

        [MenuItem("Debug/Kiểm chứng sinh cây từ Seed")]
        public static void VerifyProceduralVegetation()
        {
            var veg = UnityEngine.Object.FindFirstObjectByType<ProceduralVegetation>();
            if (veg == null)
            {
                Debug.LogError("[DebugWindow] Không tìm thấy ProceduralVegetation trong scene.");
                return;
            }

            veg.RespawnWithSeed(12345);
            uint hash12345A = veg.ComputeDeterministicHash();
            int totalA = veg.TotalCount;
            int blockA = veg.BlockCount;
            int clearA = veg.ClearCount;

            veg.RespawnWithSeed(12345);
            uint hash12345B = veg.ComputeDeterministicHash();
            int totalB = veg.TotalCount;

            veg.RespawnWithSeed(67890);
            uint hash67890 = veg.ComputeDeterministicHash();
            int total67890 = veg.TotalCount;
            int block67890 = veg.BlockCount;
            int clear67890 = veg.ClearCount;

            // Dọn các object tạm sinh trong Edit Mode để scene giữ sạch, khi vào Play Mode sẽ tự sinh từ seed.
            veg.ClearSpawned();

            Debug.Log(
                $"[DebugWindow] Kiểm chứng ProceduralVegetation: Seed 12345 khớp={hash12345A == hash12345B && totalA == totalB} " +
                $"(Hash=0x{hash12345A:X8}, Tổng={totalA}, Block={blockA}, Clear={clearA}) | " +
                $"Seed 67890 khác={hash67890 != hash12345A} (Hash=0x{hash67890:X8}, Tổng={total67890}, Block={block67890}, Clear={clear67890}).");
        }

        static short[] BuildPrototypeHeights(Vector2Int minCell, Vector2Int sizeCells)
        {
            var data = new short[sizeCells.x * sizeCells.y];
            for (int z = 0; z < sizeCells.y; z++)
            {
                int wz = minCell.y + z;
                for (int x = 0; x < sizeCells.x; x++)
                {
                    int wx = minCell.x + x;
                    short h = 0;

                    // Dốc thoải 10° tại x in [9..14], z in [9..15]: tan(10°)*256 ≈ 45 đơn vị mỗi mét.
                    if (wx >= 9 && wx <= 14 && wz >= 9 && wz <= 15)
                    {
                        h = (short)((wz - 9 + 1) * 45);
                    }
                    // Dốc đứng 35° tại x in [9..14], z in [-15..-9]: tan(35°)*256 ≈ 180 đơn vị mỗi mét.
                    else if (wx >= 9 && wx <= 14 && wz >= -15 && wz <= -9)
                    {
                        h = (short)((wz - (-15) + 1) * 180);
                    }
                    // Bục cao phẳng 1.5 m tại x in [-15..-10], z in [-15..-10].
                    else if (wx >= -15 && wx <= -10 && wz >= -15 && wz <= -10)
                    {
                        h = 384;
                    }
                    // Hồ nước tại x in [-17..-12], z in [9..14]: đáy hồ sâu -0.5 m (-128).
                    else if (wx >= -17 && wx <= -12 && wz >= 9 && wz <= 14)
                    {
                        h = -128;
                    }

                    data[x + z * sizeCells.x] = h;
                }
            }
            return data;
        }

        static void SetupGrayboxTerrainLayers(TerrainData td)
        {
            var flatLayer = CreateOrLoadLayer("FlatGrayLayer", new Color(0.62f, 0.62f, 0.62f));
            var slopeLayer = CreateOrLoadLayer("SlopeDarkGrayLayer", new Color(0.45f, 0.45f, 0.45f));
            var highLayer = CreateOrLoadLayer("PlateauLightGrayLayer", new Color(0.74f, 0.74f, 0.74f));
            td.terrainLayers = new[] { flatLayer, slopeLayer, highLayer };
        }

        static TerrainLayer CreateOrLoadLayer(string name, Color color)
        {
            string texPath = $"{TerrainAssetFolder}/{name}.png";
            string layerPath = $"{TerrainAssetFolder}/{name}.terrainlayer";

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex == null)
            {
                tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
                var pixels = new Color[16 * 16];
                Color lineCol = color * 0.92f;
                lineCol.a = 1f;
                for (int y = 0; y < 16; y++)
                {
                    for (int x = 0; x < 16; x++)
                        pixels[x + y * 16] = (x == 0 || y == 0) ? lineCol : color;
                }
                tex.SetPixels(pixels);
                tex.Apply();
                System.IO.File.WriteAllBytes(texPath, tex.EncodeToPNG());
                AssetDatabase.ImportAsset(texPath);
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            }

            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            if (layer == null)
            {
                layer = new TerrainLayer
                {
                    diffuseTexture = tex,
                    tileSize = new Vector2(1f, 1f),
                    metallic = 0f,
                    smoothness = 0.1f
                };
                AssetDatabase.CreateAsset(layer, layerPath);
            }
            else
            {
                layer.diffuseTexture = tex;
                layer.tileSize = new Vector2(1f, 1f);
            }
            return layer;
        }

        static void PaintTerrainSplatmap(TerrainData td, HeightField hf)
        {
            if (td.terrainLayers == null || td.terrainLayers.Length < 3) return;
            int w = td.alphamapWidth;
            int h = td.alphamapHeight;
            var map = new float[h, w, 3];
            Vector3 tPos = hf.UnityTerrain.transform.position;
            Vector3 tSize = td.size;

            for (int az = 0; az < h; az++)
            {
                int wz = Mathf.FloorToInt(tPos.z + (az + 0.5f) / h * tSize.z);
                for (int ax = 0; ax < w; ax++)
                {
                    int wx = Mathf.FloorToInt(tPos.x + (ax + 0.5f) / w * tSize.x);
                    bool steep = hf.IsSteepAtWorldCell(wx, wz, 25);
                    int heightSub = hf.HeightAtWorldCell(wx, wz);

                    if (steep || heightSub < 0)
                    {
                        map[az, ax, 1] = 1f;
                    }
                    else if (heightSub > 64)
                    {
                        map[az, ax, 2] = 1f;
                    }
                    else
                    {
                        map[az, ax, 0] = 1f;
                    }
                }
            }
            td.SetAlphamaps(0, 0, map);
        }

        static void SaveGameplayPrefab(GameObject sceneGo, string prefabPath)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(sceneGo))
            {
                PrefabUtility.ApplyPrefabInstance(sceneGo, InteractionMode.AutomatedAction);
            }
            else
            {
                PrefabUtility.SaveAsPrefabAssetAndConnect(sceneGo, prefabPath, InteractionMode.AutomatedAction);
            }
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            if (slash > 0)
            {
                string parent = path.Substring(0, slash);
                string folder = path.Substring(slash + 1);
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, folder);
            }
        }

        void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            EditorGUILayout.LabelField("Công cụ kiểm chứng", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            if (GUILayout.Button("1. Kiểm chứng xoay công trình (0°/90°/180°/270°)"))
                VerifyBuildingRotation();

            if (GUILayout.Button("2. Thiết lập Terrain & HeightField cho scene"))
                SetupSceneTerrainAndHeightField();

            if (GUILayout.Button("3. Kiểm chứng HeightField & Chunk"))
                VerifyHeightFieldAndChunks();

            if (GUILayout.Button("4. Cấu hình ngưỡng địa hình cho BuildingData"))
                ConfigureBuildingDataSlopeThresholds();

            if (GUILayout.Button("5. Thiết lập ObjectPlacementData & ProceduralVegetation"))
                SetupPlacementRulesAndVegetation();

            if (GUILayout.Button("6. Kiểm chứng sinh cây từ Seed"))
                VerifyProceduralVegetation();

            EditorGUILayout.EndScrollView();
        }
    }
}
