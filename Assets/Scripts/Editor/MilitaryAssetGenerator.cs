using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SurvivalRts.Editor
{
    // Sinh bộ quân sự: Lính kiếm, Lính cung (model graybox + animation GPU + prefab + SoldierData) và Trại lính,
    // đồng thời gán model/animation riêng vào BattleData.asset cho trận đại chiến.
    public static partial class CivilianAssetGenerator
    {
        const string SoldierDataDir = "Assets/ScriptableObjects/Units";
        const string BattleDataPath = "Assets/ScriptableObjects/Battle/BattleData.asset";

        static readonly Color SoldierTunic = new Color(0.78f, 0.78f, 0.78f);
        static readonly Color SoldierSteel = new Color(0.64f, 0.66f, 0.68f);
        static readonly Color SoldierLeather = new Color(0.42f, 0.42f, 0.44f);
        static readonly Color SoldierPants = new Color(0.28f, 0.28f, 0.30f);
        static readonly Color ShieldWood = new Color(0.48f, 0.48f, 0.50f);
        static readonly Color ShieldFace = new Color(0.76f, 0.76f, 0.76f);
        static readonly Color GoldTrim = new Color(0.90f, 0.90f, 0.92f);

        static readonly PartDef[] SwordsmanParts =
        {
            new PartDef { Bone = Hips, Center = new Vector3(0f, 0.02f, 0f), Size = new Vector3(0.34f, 0.16f, 0.20f), Color = SoldierLeather },
            new PartDef { Bone = Spine, Center = new Vector3(0f, 0.25f, 0f), Size = new Vector3(0.40f, 0.45f, 0.23f), Color = SoldierTunic },
            new PartDef { Bone = Spine, Center = new Vector3(0f, 0.30f, 0.118f), Size = new Vector3(0.30f, 0.26f, 0.02f), Color = SoldierSteel },
            new PartDef { Bone = Spine, Center = new Vector3(0f, 0.43f, 0f), Size = new Vector3(0.46f, 0.08f, 0.25f), Color = SoldierSteel },
            new PartDef { Bone = Head, Center = new Vector3(0f, 0.13f, 0f), Size = new Vector3(0.23f, 0.24f, 0.23f), Color = HumanSkin },
            new PartDef { Bone = Head, Center = new Vector3(0f, 0.24f, 0f), Size = new Vector3(0.27f, 0.12f, 0.27f), Color = SoldierSteel },
            new PartDef { Bone = Head, Center = new Vector3(0f, 0.31f, 0f), Size = new Vector3(0.04f, 0.06f, 0.20f), Color = GoldTrim },
            new PartDef { Bone = Head, Center = new Vector3(0f, 0.12f, 0.125f), Size = new Vector3(0.035f, 0.10f, 0.02f), Color = SoldierSteel },
            new PartDef { Bone = Head, Center = new Vector3(-0.05f, 0.14f, 0.116f), Size = new Vector3(0.04f, 0.035f, 0.01f), Color = EyeDark },
            new PartDef { Bone = Head, Center = new Vector3(0.05f, 0.14f, 0.116f), Size = new Vector3(0.04f, 0.035f, 0.01f), Color = EyeDark },
            new PartDef { Bone = ArmL, Center = new Vector3(0f, -0.13f, 0f), Size = new Vector3(0.115f, 0.28f, 0.115f), Color = SoldierTunic },
            new PartDef { Bone = ForearmL, Center = new Vector3(0f, -0.13f, 0f), Size = new Vector3(0.10f, 0.28f, 0.10f), Color = SoldierLeather },
            new PartDef { Bone = ArmR, Center = new Vector3(0f, -0.13f, 0f), Size = new Vector3(0.115f, 0.28f, 0.115f), Color = SoldierTunic },
            new PartDef { Bone = ForearmR, Center = new Vector3(0f, -0.13f, 0f), Size = new Vector3(0.10f, 0.28f, 0.10f), Color = SoldierLeather },
            // Kiếm ở tay phải: chuôi, đốc chắn và lưỡi chĩa về phía trước.
            new PartDef { Bone = ForearmR, Center = new Vector3(0f, -0.27f, 0.06f), Size = new Vector3(0.04f, 0.04f, 0.14f), Color = SoldierLeather },
            new PartDef { Bone = ForearmR, Center = new Vector3(0f, -0.27f, 0.14f), Size = new Vector3(0.20f, 0.035f, 0.04f), Color = GoldTrim },
            new PartDef { Bone = ForearmR, Center = new Vector3(0f, -0.27f, 0.47f), Size = new Vector3(0.06f, 0.02f, 0.62f), Color = SoldierSteel },
            // Khiên ở tay trái.
            new PartDef { Bone = ForearmL, Center = new Vector3(-0.08f, -0.15f, 0.02f), Size = new Vector3(0.04f, 0.44f, 0.36f), Color = ShieldWood },
            new PartDef { Bone = ForearmL, Center = new Vector3(-0.105f, -0.15f, 0.02f), Size = new Vector3(0.012f, 0.36f, 0.28f), Color = ShieldFace },
            new PartDef { Bone = ForearmL, Center = new Vector3(-0.115f, -0.15f, 0.02f), Size = new Vector3(0.02f, 0.08f, 0.08f), Color = GoldTrim },
            new PartDef { Bone = ThighL, Center = new Vector3(0f, -0.21f, 0f), Size = new Vector3(0.135f, 0.44f, 0.135f), Color = SoldierPants },
            new PartDef { Bone = ShinL, Center = new Vector3(0f, -0.20f, 0f), Size = new Vector3(0.125f, 0.41f, 0.125f), Color = SoldierPants },
            new PartDef { Bone = ShinL, Center = new Vector3(0f, -0.41f, 0.035f), Size = new Vector3(0.135f, 0.075f, 0.21f), Color = BootsDark },
            new PartDef { Bone = ThighR, Center = new Vector3(0f, -0.21f, 0f), Size = new Vector3(0.135f, 0.44f, 0.135f), Color = SoldierPants },
            new PartDef { Bone = ShinR, Center = new Vector3(0f, -0.20f, 0f), Size = new Vector3(0.125f, 0.41f, 0.125f), Color = SoldierPants },
            new PartDef { Bone = ShinR, Center = new Vector3(0f, -0.41f, 0.035f), Size = new Vector3(0.135f, 0.075f, 0.21f), Color = BootsDark },
        };

        static readonly PartDef[] ArcherParts =
        {
            new PartDef { Bone = Hips, Center = new Vector3(0f, 0.02f, 0f), Size = new Vector3(0.32f, 0.15f, 0.19f), Color = SoldierLeather },
            new PartDef { Bone = Spine, Center = new Vector3(0f, 0.25f, 0f), Size = new Vector3(0.37f, 0.44f, 0.21f), Color = SoldierTunic },
            new PartDef { Bone = Spine, Center = new Vector3(0f, 0.25f, 0.11f), Size = new Vector3(0.30f, 0.06f, 0.02f), Color = SoldierLeather },
            // Ống tên đeo sau lưng kèm đuôi lông tên nhô lên trên vai phải.
            new PartDef { Bone = Spine, Center = new Vector3(0.08f, 0.26f, -0.155f), Size = new Vector3(0.13f, 0.38f, 0.11f), Color = SoldierLeather },
            new PartDef { Bone = Spine, Center = new Vector3(0.08f, 0.49f, -0.155f), Size = new Vector3(0.10f, 0.12f, 0.08f), Color = GoldTrim },
            new PartDef { Bone = Head, Center = new Vector3(0f, 0.13f, 0f), Size = new Vector3(0.22f, 0.24f, 0.22f), Color = HumanSkin },
            // Mũ trùm cung thủ có vành che trước trán.
            new PartDef { Bone = Head, Center = new Vector3(0f, 0.23f, -0.01f), Size = new Vector3(0.26f, 0.11f, 0.27f), Color = SoldierTunic },
            new PartDef { Bone = Head, Center = new Vector3(0f, 0.20f, 0.14f), Size = new Vector3(0.22f, 0.04f, 0.06f), Color = SoldierLeather },
            new PartDef { Bone = Head, Center = new Vector3(-0.05f, 0.14f, 0.116f), Size = new Vector3(0.04f, 0.035f, 0.01f), Color = EyeDark },
            new PartDef { Bone = Head, Center = new Vector3(0.05f, 0.14f, 0.116f), Size = new Vector3(0.04f, 0.035f, 0.01f), Color = EyeDark },
            new PartDef { Bone = ArmL, Center = new Vector3(0f, -0.13f, 0f), Size = new Vector3(0.105f, 0.28f, 0.105f), Color = SoldierTunic },
            new PartDef { Bone = ForearmL, Center = new Vector3(0f, -0.13f, 0f), Size = new Vector3(0.095f, 0.28f, 0.095f), Color = SoldierLeather },
            // Cung cầm ở tay trái: thân cung uốn cong và dây cung căng dọc hai đầu.
            new PartDef { Bone = ForearmL, Center = new Vector3(0f, -0.27f, 0f), Size = new Vector3(0.045f, 0.06f, 0.22f), Color = SoldierLeather },
            new PartDef { Bone = ForearmL, Center = new Vector3(0f, -0.22f, 0.24f), Size = new Vector3(0.04f, 0.055f, 0.30f), Color = ShieldWood },
            new PartDef { Bone = ForearmL, Center = new Vector3(0f, -0.22f, -0.24f), Size = new Vector3(0.04f, 0.055f, 0.30f), Color = ShieldWood },
            new PartDef { Bone = ForearmL, Center = new Vector3(0f, -0.13f, 0.44f), Size = new Vector3(0.035f, 0.05f, 0.16f), Color = ShieldWood },
            new PartDef { Bone = ForearmL, Center = new Vector3(0f, -0.13f, -0.44f), Size = new Vector3(0.035f, 0.05f, 0.16f), Color = ShieldWood },
            new PartDef { Bone = ForearmL, Center = new Vector3(0f, -0.06f, 0f), Size = new Vector3(0.016f, 0.016f, 0.96f), Color = GoldTrim },
            new PartDef { Bone = ArmR, Center = new Vector3(0f, -0.13f, 0f), Size = new Vector3(0.105f, 0.28f, 0.105f), Color = SoldierTunic },
            new PartDef { Bone = ForearmR, Center = new Vector3(0f, -0.13f, 0f), Size = new Vector3(0.095f, 0.28f, 0.095f), Color = SoldierLeather },
            // Mũi tên cầm ở tay phải: thân tên, đầu nhọn và lông vũ.
            new PartDef { Bone = ForearmR, Center = new Vector3(0f, -0.27f, 0.22f), Size = new Vector3(0.025f, 0.025f, 0.52f), Color = ShieldWood },
            new PartDef { Bone = ForearmR, Center = new Vector3(0f, -0.27f, 0.50f), Size = new Vector3(0.045f, 0.025f, 0.08f), Color = SoldierSteel },
            new PartDef { Bone = ForearmR, Center = new Vector3(0f, -0.27f, -0.02f), Size = new Vector3(0.05f, 0.05f, 0.08f), Color = GoldTrim },
            new PartDef { Bone = ThighL, Center = new Vector3(0f, -0.21f, 0f), Size = new Vector3(0.13f, 0.44f, 0.13f), Color = SoldierPants },
            new PartDef { Bone = ShinL, Center = new Vector3(0f, -0.20f, 0f), Size = new Vector3(0.12f, 0.41f, 0.12f), Color = SoldierPants },
            new PartDef { Bone = ShinL, Center = new Vector3(0f, -0.41f, 0.035f), Size = new Vector3(0.13f, 0.075f, 0.20f), Color = BootsDark },
            new PartDef { Bone = ThighR, Center = new Vector3(0f, -0.21f, 0f), Size = new Vector3(0.13f, 0.44f, 0.13f), Color = SoldierPants },
            new PartDef { Bone = ShinR, Center = new Vector3(0f, -0.20f, 0f), Size = new Vector3(0.12f, 0.41f, 0.12f), Color = SoldierPants },
            new PartDef { Bone = ShinR, Center = new Vector3(0f, -0.41f, 0.035f), Size = new Vector3(0.13f, 0.075f, 0.20f), Color = BootsDark },
        };

        [MenuItem("SurvivalRts/Tạo quân sự (Trại lính, Lính kiếm, Lính cung)")]
        public static void GenerateMilitary()
        {
            EnsureFolder(ModelDir);
            EnsureFolder(AnimDir);
            EnsureFolder(TextureDir);
            EnsureFolder(MaterialDir);
            EnsureFolder(AnimDataDir);
            EnsureFolder(UnitPrefabDir);
            EnsureFolder(BuildingPrefabDir);
            EnsureFolder(BuildingDataDir);
            EnsureFolder(SoldierDataDir);

            var soldier = GenerateSwordsman(out var swordMesh, out var swordMat, out var swordAnim);
            var archer = GenerateArcher(out var archerMesh, out var archerMat, out var archerAnim);
            AssignBattleUnitVisuals(swordMesh, swordMat, swordAnim, archerMesh, archerMat, archerAnim);
            var barracks = GenerateBarracksBuilding(soldier);
            var archeryRange = GenerateArcheryRangeBuilding(archer);
            AddToBuildMenu(barracks, archeryRange);

            AssetDatabase.SaveAssets();
            Debug.Log("Đã tạo Lính kiếm, Lính cung, Trại lính và Trại cung, đồng thời cập nhật BattleData.asset.");
        }

        static SoldierData GenerateSwordsman(out Mesh mesh, out Material material, out GpuAnimationData animData)
        {
            const string unit = "Swordsman";
            EnsureFolder(ModelDir + "/" + unit);
            EnsureFolder(AnimDir + "/" + unit);
            var root = new GameObject(unit + "_Rig");
            try
            {
                var bones = BuildRig(root);
                var bindposes = BuildBindposes(root, bones);
                mesh = BuildCharacterMesh(unit + "_Mesh", bones, bindposes, SwordsmanParts);
                mesh = SaveAsset(mesh, ModelDir + "/" + unit + "/" + unit + "_Mesh.asset");

                var sources = new List<GpuAnimationBaker.ClipSource>
                {
                    MakeClip(bones, root, unit, "Idle", 2f, true, SoldierIdlePose),
                    MakeClip(bones, root, unit, "Walk", 0.7f, true, SoldierWalkPose),
                    MakeClip(bones, root, unit, "Attack", 0.9f, true, SwordAttackPose),
                    MakeClip(bones, root, unit, "Death", 1.2f, false, DeathPose),
                };
                animData = GpuAnimationBaker.Bake(root, bones, bindposes, sources, BakeFps,
                    TextureDir + "/" + unit + "_BoneTex.asset", AnimDataDir + "/" + unit + "_Anim.asset");

                ResetPose(bones);
                SaveRigPrefab(root, bones, mesh, unit, SoldierTunic);
                material = CreateGpuSkinnedMaterial("M_" + unit, animData.boneTexture);
                var prefab = CreateSoldierPrefab(unit, mesh, material, animData);

                string dataPath = SoldierDataDir + "/Soldier_" + unit + ".asset";
                var data = AssetDatabase.LoadAssetAtPath<SoldierData>(dataPath);
                if (data == null)
                {
                    data = ScriptableObject.CreateInstance<SoldierData>();
                    AssetDatabase.CreateAsset(data, dataPath);
                }
                data.displayName = "Lính kiếm";
                data.prefab = prefab;
                EditorUtility.SetDirty(data);
                return data;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static SoldierData GenerateArcher(out Mesh mesh, out Material material, out GpuAnimationData animData)
        {
            const string unit = "Archer";
            EnsureFolder(ModelDir + "/" + unit);
            EnsureFolder(AnimDir + "/" + unit);
            var root = new GameObject(unit + "_Rig");
            try
            {
                var bones = BuildRig(root);
                var bindposes = BuildBindposes(root, bones);
                mesh = BuildCharacterMesh(unit + "_Mesh", bones, bindposes, ArcherParts);
                mesh = SaveAsset(mesh, ModelDir + "/" + unit + "/" + unit + "_Mesh.asset");

                var sources = new List<GpuAnimationBaker.ClipSource>
                {
                    MakeClip(bones, root, unit, "Idle", 2f, true, ArcherIdlePose),
                    MakeClip(bones, root, unit, "Walk", 0.7f, true, ArcherWalkPose),
                    MakeClip(bones, root, unit, "Attack", 1.4f, true, ArcherAttackPose),
                    MakeClip(bones, root, unit, "Death", 1.2f, false, DeathPose),
                };
                animData = GpuAnimationBaker.Bake(root, bones, bindposes, sources, BakeFps,
                    TextureDir + "/" + unit + "_BoneTex.asset", AnimDataDir + "/" + unit + "_Anim.asset");

                ResetPose(bones);
                SaveRigPrefab(root, bones, mesh, unit, SoldierTunic);
                material = CreateGpuSkinnedMaterial("M_" + unit, animData.boneTexture);
                var prefab = CreateSoldierPrefab(unit, mesh, material, animData);

                string dataPath = SoldierDataDir + "/Soldier_" + unit + ".asset";
                var data = AssetDatabase.LoadAssetAtPath<SoldierData>(dataPath);
                if (data == null)
                {
                    data = ScriptableObject.CreateInstance<SoldierData>();
                    AssetDatabase.CreateAsset(data, dataPath);
                }
                data.displayName = "Lính cung";
                data.prefab = prefab;
                data.ranged = true;
                data.maxHealth = 70f;
                data.damage = 12f;
                data.attackInterval = 1.4f;
                data.strikeDelay = 0.7f;
                data.attackRange = 14f;
                data.detectRange = 16f;
                data.leashRange = 20f;
                data.moveSpeed = 3f;
                EditorUtility.SetDirty(data);
                return data;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static void AssignBattleUnitVisuals(Mesh swordMesh, Material swordMat, GpuAnimationData swordAnim,
            Mesh archerMesh, Material archerMat, GpuAnimationData archerAnim)
        {
            var battleData = AssetDatabase.LoadAssetAtPath<BattleData>(BattleDataPath);
            if (battleData == null || battleData.unitTypes == null) return;

            for (int i = 0; i < battleData.unitTypes.Length; i++)
            {
                var unitType = battleData.unitTypes[i];
                if (unitType.ranged)
                {
                    unitType.mesh = archerMesh;
                    unitType.material = archerMat;
                    unitType.animation = archerAnim;
                    unitType.tint = Color.white;
                    unitType.scale = 0.85f;
                }
                else
                {
                    unitType.mesh = swordMesh;
                    unitType.material = swordMat;
                    unitType.animation = swordAnim;
                    unitType.tint = Color.white;
                    unitType.scale = 0.85f;
                }
            }
            EditorUtility.SetDirty(battleData);
        }

        static GameObject CreateSoldierPrefab(string unitName, Mesh mesh, Material material, GpuAnimationData animData)
        {
            var go = new GameObject(unitName);
            try
            {
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = material;
                go.AddComponent<GpuMeshAnimator>().Setup(animData, "Idle");
                go.AddComponent<Health>();
                // Không có GridOccupant nên PlayerTarget tự xếp loại Unit: kẻ địch ưu tiên đánh lính trước công trình.
                go.AddComponent<PlayerTarget>();
                go.AddComponent<SoldierUnit>();

                var collider = go.AddComponent<CapsuleCollider>();
                collider.center = new Vector3(0f, 0.9f, 0f);
                collider.radius = 0.35f;
                collider.height = 1.8f;

                return PrefabUtility.SaveAsPrefabAsset(go, UnitPrefabDir + "/" + unitName + ".prefab");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        static BuildingData GenerateBarracksBuilding(SoldierData soldier)
        {
            var stoneMat = LoadOrCreateLitMaterial(TestMatDir + "/M_Stone.mat", new Color(0.52f, 0.52f, 0.50f));
            var woodMat = LoadOrCreateLitMaterial(TestMatDir + "/M_Wood.mat", new Color(0.55f, 0.36f, 0.18f));
            var frontMat = LoadOrCreateLitMaterial(TestMatDir + "/M_Front.mat", new Color(0.90f, 0.75f, 0.20f));
            var wallMat = LoadOrCreateLitMaterial(MaterialDir + "/M_Barracks_Wall.mat", new Color(0.58f, 0.44f, 0.30f));
            var roofMat = LoadOrCreateLitMaterial(MaterialDir + "/M_Barracks_Roof.mat", new Color(0.62f, 0.18f, 0.14f));
            var bannerMat = LoadOrCreateLitMaterial(MaterialDir + "/M_Barracks_Banner.mat", SoldierTunic);
            var steelMat = LoadOrCreateLitMaterial(MaterialDir + "/M_Barracks_Steel.mat", SoldierSteel);

            var barracks = new GameObject("Barracks");
            try
            {
                var t = barracks.transform;
                AddBox(t, "Foundation", new Vector3(0f, 0.1f, 0f), new Vector3(2.9f, 0.2f, 2.9f), stoneMat, true);
                // Nhà dài ở nửa sau, sân tập ở nửa trước (phía +Z là cửa, lính tập kết ở đây).
                AddBox(t, "Longhouse", new Vector3(0f, 0.95f, -0.55f), new Vector3(2.5f, 1.5f, 1.5f), wallMat, true);
                AddBox(t, "RoofLow", new Vector3(0f, 1.82f, -0.55f), new Vector3(2.7f, 0.25f, 1.75f), roofMat, true);
                AddBox(t, "RoofHigh", new Vector3(0f, 2.05f, -0.55f), new Vector3(2.3f, 0.22f, 1.1f), roofMat, false);
                AddBox(t, "Door", new Vector3(0f, 0.6f, 0.22f), new Vector3(0.6f, 0.9f, 0.04f), woodMat, false);
                // Hàng rào gỗ hai bên sân.
                AddBox(t, "FenceL", new Vector3(-1.32f, 0.45f, 0.75f), new Vector3(0.12f, 0.7f, 1.2f), woodMat, false);
                AddBox(t, "FenceR", new Vector3(1.32f, 0.45f, 0.75f), new Vector3(0.12f, 0.7f, 1.2f), woodMat, false);
                // Cột cờ và lá cờ.
                AddBox(t, "FlagPole", new Vector3(-1.1f, 1.6f, 1.1f), new Vector3(0.08f, 3f, 0.08f), woodMat, false);
                AddBox(t, "Banner", new Vector3(-0.82f, 2.65f, 1.1f), new Vector3(0.5f, 0.6f, 0.03f), bannerMat, false);
                // Giá vũ khí và hình nộm tập.
                AddBox(t, "WeaponRack", new Vector3(0.95f, 0.55f, 0.55f), new Vector3(0.12f, 0.9f, 0.7f), woodMat, false);
                AddBox(t, "RackSword1", new Vector3(0.88f, 0.75f, 0.35f), new Vector3(0.04f, 0.7f, 0.06f), steelMat, false);
                AddBox(t, "RackSword2", new Vector3(0.88f, 0.75f, 0.55f), new Vector3(0.04f, 0.7f, 0.06f), steelMat, false);
                AddBox(t, "RackSword3", new Vector3(0.88f, 0.75f, 0.75f), new Vector3(0.04f, 0.7f, 0.06f), steelMat, false);
                AddBox(t, "DummyPost", new Vector3(0.25f, 0.55f, 0.95f), new Vector3(0.1f, 0.9f, 0.1f), woodMat, false);
                AddBox(t, "DummyBody", new Vector3(0.25f, 0.95f, 0.95f), new Vector3(0.36f, 0.4f, 0.2f), wallMat, false);
                AddBox(t, "FrontMarker", new Vector3(0f, 0.22f, 1.35f), new Vector3(0.6f, 0.3f, 0.18f), frontMat, false);

                var prefab = PrefabUtility.SaveAsPrefabAsset(barracks, BuildingPrefabDir + "/Barracks.prefab");
                string path = BuildingDataDir + "/Building_Barracks.asset";
                // maxWorkers = số lính tối đa của một trại.
                SaveBuildingData(path, "Trại lính", BuildingCategory.Military,
                    prefab, new Vector2Int(3, 3), 2.3f, 600f, 10f, 0, 6, ResourceType.None, 0f, false, 0f, 0,
                    woodCost: 8, stoneCost: 4);
                var data = AssetDatabase.LoadAssetAtPath<BuildingData>(path);
                data.soldier = soldier;
                EditorUtility.SetDirty(data);
                return data;
            }
            finally
            {
                Object.DestroyImmediate(barracks);
            }
        }

        static BuildingData GenerateArcheryRangeBuilding(SoldierData archer)
        {
            var stoneMat = LoadOrCreateLitMaterial(TestMatDir + "/M_Stone.mat", new Color(0.52f, 0.52f, 0.50f));
            var woodMat = LoadOrCreateLitMaterial(TestMatDir + "/M_Wood.mat", new Color(0.55f, 0.36f, 0.18f));
            var frontMat = LoadOrCreateLitMaterial(TestMatDir + "/M_Front.mat", new Color(0.90f, 0.75f, 0.20f));
            var wallMat = LoadOrCreateLitMaterial(MaterialDir + "/M_Barracks_Wall.mat", new Color(0.58f, 0.44f, 0.30f));
            var roofMat = LoadOrCreateLitMaterial(MaterialDir + "/M_Barracks_Roof.mat", new Color(0.62f, 0.18f, 0.14f));
            var bannerMat = LoadOrCreateLitMaterial(MaterialDir + "/M_Barracks_Banner.mat", SoldierTunic);

            var range = new GameObject("ArcheryRange");
            try
            {
                var t = range.transform;
                AddBox(t, "Foundation", new Vector3(0f, 0.1f, 0f), new Vector3(2.9f, 0.2f, 2.9f), stoneMat, true);
                AddBox(t, "Lodge", new Vector3(0f, 0.85f, -0.65f), new Vector3(2.4f, 1.3f, 1.25f), wallMat, true);
                AddBox(t, "Roof", new Vector3(0f, 1.62f, -0.65f), new Vector3(2.6f, 0.24f, 1.45f), roofMat, true);
                AddBox(t, "Door", new Vector3(0f, 0.55f, -0.02f), new Vector3(0.55f, 0.85f, 0.04f), woodMat, false);
                AddBox(t, "FenceL", new Vector3(-1.32f, 0.45f, 0.55f), new Vector3(0.12f, 0.65f, 1.5f), woodMat, false);
                AddBox(t, "FenceR", new Vector3(1.32f, 0.45f, 0.55f), new Vector3(0.12f, 0.65f, 1.5f), woodMat, false);
                // Hai bia bắn cung đặt ở sân tập.
                AddBox(t, "TargetPost1", new Vector3(-0.65f, 0.55f, 0.95f), new Vector3(0.10f, 0.85f, 0.10f), woodMat, false);
                AddBox(t, "TargetBoard1", new Vector3(-0.65f, 0.85f, 0.90f), new Vector3(0.42f, 0.42f, 0.08f), bannerMat, false);
                AddBox(t, "TargetCenter1", new Vector3(-0.65f, 0.85f, 0.85f), new Vector3(0.16f, 0.16f, 0.04f), roofMat, false);
                AddBox(t, "TargetPost2", new Vector3(0.65f, 0.55f, 0.95f), new Vector3(0.10f, 0.85f, 0.10f), woodMat, false);
                AddBox(t, "TargetBoard2", new Vector3(0.65f, 0.85f, 0.90f), new Vector3(0.42f, 0.42f, 0.08f), bannerMat, false);
                AddBox(t, "TargetCenter2", new Vector3(0.65f, 0.85f, 0.85f), new Vector3(0.16f, 0.16f, 0.04f), roofMat, false);
                AddBox(t, "FrontMarker", new Vector3(0f, 0.22f, 1.35f), new Vector3(0.6f, 0.3f, 0.18f), frontMat, false);

                var prefab = PrefabUtility.SaveAsPrefabAsset(range, BuildingPrefabDir + "/ArcheryRange.prefab");
                string path = BuildingDataDir + "/Building_ArcheryRange.asset";
                SaveBuildingData(path, "Trại cung", BuildingCategory.Military,
                    prefab, new Vector2Int(3, 3), 2.1f, 520f, 10f, 0, 6, ResourceType.None, 0f, false, 0f, 0,
                    woodCost: 10, stoneCost: 3);
                var data = AssetDatabase.LoadAssetAtPath<BuildingData>(path);
                data.soldier = archer;
                EditorUtility.SetDirty(data);
                return data;
            }
            finally
            {
                Object.DestroyImmediate(range);
            }
        }

        static void AddToBuildMenu(BuildingData barracks, BuildingData archeryRange)
        {
            var placer = Object.FindAnyObjectByType<BuildingPlacer>();
            if (placer == null)
            {
                Debug.LogWarning("Không tìm thấy BuildingPlacer trong scene đang mở; hãy tự thêm Building_Barracks vào danh sách công trình.");
                return;
            }
            var so = new SerializedObject(placer);
            var buildings = so.FindProperty("buildings");
            if (buildings == null) return;
            EnsureBuildingInList(buildings, barracks, buildings.arraySize);
            EnsureBuildingInList(buildings, archeryRange, buildings.arraySize);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(placer);

            var tracerPool = Object.FindAnyObjectByType<TracerPool>();
            if (tracerPool != null)
            {
                var tracerSo = new SerializedObject(tracerPool);
                tracerSo.FindProperty("width").floatValue = 0.08f;
                tracerSo.FindProperty("duration").floatValue = 0.35f;
                tracerSo.FindProperty("projectileSpeed").floatValue = 24f;
                tracerSo.FindProperty("arcHeightFactor").floatValue = 0.25f;
                tracerSo.FindProperty("arrowLength").floatValue = 1.35f;
                tracerSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(tracerPool);
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(placer.gameObject.scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(placer.gameObject.scene);
        }

        // ---------- Tư thế lính kiếm ----------

        // Thế thủ: kiếm giơ chéo trước người, khiên che ngực, thở nhẹ (chu kỳ 2s).
        static void SoldierIdlePose(float t, Vector3[] e, ref Vector3 hips)
        {
            float p = t / 2f * Mathf.PI * 2f;
            float s = Mathf.Sin(p);
            e[Spine] = new Vector3(4f + 1.5f * s, 0f, 0f);
            e[Head] = new Vector3(-3f + s, 0f, 0f);
            e[ArmR] = new Vector3(-38f + 2f * s, 0f, 10f);
            e[ForearmR] = new Vector3(-48f, 0f, 0f);
            e[ArmL] = new Vector3(-42f + 2f * s, 18f, -8f);
            e[ForearmL] = new Vector3(-62f, 0f, 0f);
            e[ThighL] = new Vector3(-10f, 0f, -4f);
            e[ShinL] = new Vector3(10f, 0f, 0f);
            e[ThighR] = new Vector3(8f, 0f, 4f);
            e[ShinR] = new Vector3(6f, 0f, 0f);
            hips.y = 0.93f - 0.01f * (1f - Mathf.Cos(p));
        }

        // Chạy giữ thế thủ: chân như đi bộ nhanh, tay giữ kiếm và khiên trước người (chu kỳ 0.7s).
        static void SoldierWalkPose(float t, Vector3[] e, ref Vector3 hips)
        {
            float p = t / 0.7f * Mathf.PI * 2f;
            float s = Mathf.Sin(p);
            float c = Mathf.Cos(p);
            e[Hips] = new Vector3(0f, -4f * s, 0f);
            e[Spine] = new Vector3(10f, 4f * s, 0f);
            e[Head] = new Vector3(-8f, -4f * s, 0f);
            e[ThighL] = new Vector3(-32f * s, 0f, 0f);
            e[ThighR] = new Vector3(32f * s, 0f, 0f);
            e[ShinL] = new Vector3(6f + 44f * Mathf.Max(0f, c), 0f, 0f);
            e[ShinR] = new Vector3(6f + 44f * Mathf.Max(0f, -c), 0f, 0f);
            e[ArmR] = new Vector3(-34f - 6f * s, 0f, 10f);
            e[ForearmR] = new Vector3(-50f, 0f, 0f);
            e[ArmL] = new Vector3(-40f + 6f * s, 18f, -8f);
            e[ForearmL] = new Vector3(-62f, 0f, 0f);
            hips.y = 0.91f + 0.045f * Mathf.Abs(c);
        }

        // Nhát chém (chu kỳ 0.9s): vung kiếm lên ở 0.30s, chém xuống ở 0.45s (khớp SoldierData.strikeDelay), thu về.
        static void SwordAttackPose(float t, Vector3[] e, ref Vector3 hips)
        {
            e[ArmR] = new Vector3(Keys(t, 0f, -38f, 0.30f, -150f, 0.45f, -15f, 0.9f, -38f),
                Keys(t, 0f, 0f, 0.30f, -20f, 0.45f, 25f, 0.9f, 0f), 10f);
            e[ForearmR] = new Vector3(Keys(t, 0f, -48f, 0.30f, -80f, 0.45f, -8f, 0.9f, -48f), 0f, 0f);
            e[Spine] = new Vector3(Keys(t, 0f, 6f, 0.30f, -8f, 0.45f, 24f, 0.9f, 6f),
                Keys(t, 0f, 0f, 0.30f, -22f, 0.45f, 20f, 0.9f, 0f), 0f);
            e[Hips] = new Vector3(0f, Keys(t, 0f, 0f, 0.30f, -8f, 0.45f, 8f, 0.9f, 0f), 0f);
            e[Head] = new Vector3(Keys(t, 0f, -3f, 0.45f, -14f, 0.9f, -3f), 0f, 0f);
            e[ArmL] = new Vector3(-48f, 22f, -8f);
            e[ForearmL] = new Vector3(-66f, 0f, 0f);
            e[ThighL] = new Vector3(Keys(t, 0f, -12f, 0.45f, -24f, 0.9f, -12f), 0f, -4f);
            e[ShinL] = new Vector3(Keys(t, 0f, 12f, 0.45f, 22f, 0.9f, 12f), 0f, 0f);
            e[ThighR] = new Vector3(14f, 0f, 4f);
            e[ShinR] = new Vector3(10f, 0f, 0f);
            hips.y = Keys(t, 0f, 0.92f, 0.30f, 0.94f, 0.45f, 0.86f, 0.9f, 0.92f);
        }

        // ---------- Tư thế lính cung ----------

        // Đứng gác cầm cung: tay trái giữ cung trước ngực, tay phải cầm tên ngang hông, thở nhẹ (chu kỳ 2s).
        static void ArcherIdlePose(float t, Vector3[] e, ref Vector3 hips)
        {
            float p = t / 2f * Mathf.PI * 2f;
            float s = Mathf.Sin(p);
            e[Spine] = new Vector3(3f + 1.5f * s, 8f, 0f);
            e[Head] = new Vector3(-2f + s, -8f, 0f);
            e[ArmL] = new Vector3(-36f + 2f * s, 12f, -8f);
            e[ForearmL] = new Vector3(-42f, 0f, 0f);
            e[ArmR] = new Vector3(-22f - 2f * s, -10f, 8f);
            e[ForearmR] = new Vector3(-48f, 0f, 0f);
            e[ThighL] = new Vector3(-8f, 0f, -3f);
            e[ShinL] = new Vector3(8f, 0f, 0f);
            e[ThighR] = new Vector3(8f, 0f, 3f);
            e[ShinR] = new Vector3(6f, 0f, 0f);
            hips.y = 0.93f - 0.01f * (1f - Mathf.Cos(p));
        }

        // Chạy cầm cung: thân hơi đổ trước, tay trái giữ cung, tay phải giữ tên đánh nhịp nhẹ (chu kỳ 0.7s).
        static void ArcherWalkPose(float t, Vector3[] e, ref Vector3 hips)
        {
            float p = t / 0.7f * Mathf.PI * 2f;
            float s = Mathf.Sin(p);
            float c = Mathf.Cos(p);
            e[Hips] = new Vector3(0f, -4f * s, 0f);
            e[Spine] = new Vector3(9f, 4f * s, 0f);
            e[Head] = new Vector3(-7f, -4f * s, 0f);
            e[ThighL] = new Vector3(-30f * s, 0f, 0f);
            e[ThighR] = new Vector3(30f * s, 0f, 0f);
            e[ShinL] = new Vector3(6f + 42f * Mathf.Max(0f, c), 0f, 0f);
            e[ShinR] = new Vector3(6f + 42f * Mathf.Max(0f, -c), 0f, 0f);
            e[ArmL] = new Vector3(-38f + 8f * s, 10f, -8f);
            e[ForearmL] = new Vector3(-45f, 0f, 0f);
            e[ArmR] = new Vector3(-24f - 10f * s, -8f, 8f);
            e[ForearmR] = new Vector3(-50f, 0f, 0f);
            hips.y = 0.91f + 0.042f * Mathf.Abs(c);
        }

        // Giương cung bắn (chu kỳ 1.4s): nâng cung và kéo dây từ 0.20s đến 0.66s, buông dây tại 0.70s (khớp strikeDelay), rồi rút tên mới.
        static void ArcherAttackPose(float t, Vector3[] e, ref Vector3 hips)
        {
            e[Hips] = new Vector3(0f, Keys(t, 0f, 10f, 0.22f, 18f, 0.66f, 22f, 0.74f, 20f, 1.4f, 10f), 0f);
            e[Spine] = new Vector3(
                Keys(t, 0f, 4f, 0.22f, 0f, 0.66f, -3f, 0.74f, 2f, 1.4f, 4f),
                Keys(t, 0f, 8f, 0.22f, 16f, 0.66f, 24f, 0.74f, 20f, 1.4f, 8f), 0f);
            e[Head] = new Vector3(
                Keys(t, 0f, -2f, 0.22f, 0f, 0.66f, 2f, 1.4f, -2f),
                Keys(t, 0f, -16f, 0.22f, -32f, 0.66f, -42f, 0.74f, -38f, 1.4f, -16f), 0f);
            // Tay trái giơ thẳng cánh cung về phía mục tiêu.
            e[ArmL] = new Vector3(
                Keys(t, 0f, -45f, 0.20f, -84f, 0.66f, -88f, 0.74f, -92f, 1.10f, -60f, 1.4f, -45f),
                Keys(t, 0f, 10f, 0.20f, -18f, 0.74f, -18f, 1.4f, 10f), -6f);
            e[ForearmL] = new Vector3(
                Keys(t, 0f, -40f, 0.20f, -6f, 0.74f, -4f, 1.10f, -28f, 1.4f, -40f), 0f, 0f);
            // Tay phải đặt tên vào dây (0.20s), kéo căng về sát má (0.66s), bật nảy khi buông tên (0.72s) rồi vòng lên rút tên mới.
            e[ArmR] = new Vector3(
                Keys(t, 0f, -35f, 0.20f, -82f, 0.66f, -52f, 0.73f, -38f, 1.05f, -110f, 1.4f, -35f),
                Keys(t, 0f, -8f, 0.20f, -18f, 0.66f, 24f, 0.73f, 32f, 1.05f, 15f, 1.4f, -8f),
                Keys(t, 0f, 10f, 0.20f, 8f, 0.66f, 22f, 0.73f, 28f, 1.05f, 14f, 1.4f, 10f));
            e[ForearmR] = new Vector3(
                Keys(t, 0f, -48f, 0.20f, -20f, 0.66f, -92f, 0.73f, -72f, 1.05f, -85f, 1.4f, -48f), 0f, 0f);
            e[ThighL] = new Vector3(-14f, 0f, -4f);
            e[ShinL] = new Vector3(14f, 0f, 0f);
            e[ThighR] = new Vector3(12f, 0f, 4f);
            e[ShinR] = new Vector3(10f, 0f, 0f);
            hips.y = Keys(t, 0f, 0.92f, 0.66f, 0.90f, 0.73f, 0.92f, 1.4f, 0.92f);
        }
    }
}
