using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SurvivalRts.Editor
{
    // Sinh tự động bộ model và animation cho Người dân (Villager), Thợ xây (Builder),
    // 3 giai đoạn khung xây dựng (Construction Stage 1-3) và 2 công trình (House, BuilderHut).
    public static class CivilianAssetGenerator
    {
        const float BakeFps = 30f;
        const string ModelDir = "Assets/Arts/Models";
        const string AnimDir = "Assets/Arts/Animations";
        const string TextureDir = "Assets/Arts/Textures";
        const string MaterialDir = "Assets/Arts/Materials";
        const string TestMatDir = "Assets/Arts/Materials/Test";
        const string AnimDataDir = "Assets/ScriptableObjects/Animations";
        const string UnitPrefabDir = "Assets/Prefabs/TestObjects/Units";
        const string BuildingPrefabDir = "Assets/Prefabs/TestObjects/Buildings";
        const string BuildingDataDir = "Assets/ScriptableObjects/Buildings";

        const int Hips = 0, Spine = 1, Head = 2, ArmL = 3, ForearmL = 4, ArmR = 5, ForearmR = 6,
            ThighL = 7, ShinL = 8, ThighR = 9, ShinR = 10;

        struct BoneDef
        {
            public string Name;
            public int Parent;
            public Vector3 Position;
        }

        struct PartDef
        {
            public int Bone;
            public Vector3 Center;
            public Vector3 Size;
            public Color Color;
        }

        delegate void PoseFunction(float t, Vector3[] euler, ref Vector3 hips);

        static readonly BoneDef[] Bones =
        {
            new BoneDef { Name = "Hips", Parent = -1, Position = new Vector3(0f, 0.95f, 0f) },
            new BoneDef { Name = "Spine", Parent = Hips, Position = new Vector3(0f, 0.1f, 0f) },
            new BoneDef { Name = "Head", Parent = Spine, Position = new Vector3(0f, 0.55f, 0f) },
            new BoneDef { Name = "UpperArm.L", Parent = Spine, Position = new Vector3(-0.27f, 0.46f, 0f) },
            new BoneDef { Name = "Forearm.L", Parent = ArmL, Position = new Vector3(0f, -0.28f, 0f) },
            new BoneDef { Name = "UpperArm.R", Parent = Spine, Position = new Vector3(0.27f, 0.46f, 0f) },
            new BoneDef { Name = "Forearm.R", Parent = ArmR, Position = new Vector3(0f, -0.28f, 0f) },
            new BoneDef { Name = "Thigh.L", Parent = Hips, Position = new Vector3(-0.11f, -0.05f, 0f) },
            new BoneDef { Name = "Shin.L", Parent = ThighL, Position = new Vector3(0f, -0.44f, 0f) },
            new BoneDef { Name = "Thigh.R", Parent = Hips, Position = new Vector3(0.11f, -0.05f, 0f) },
            new BoneDef { Name = "Shin.R", Parent = ThighR, Position = new Vector3(0f, -0.44f, 0f) },
        };

        // Bảng màu nhân vật
        static readonly Color HumanSkin = new Color(0.90f, 0.73f, 0.58f);
        static readonly Color HairBrown = new Color(0.26f, 0.17f, 0.10f);
        static readonly Color EyeDark = new Color(0.06f, 0.05f, 0.04f);
        static readonly Color BootsDark = new Color(0.16f, 0.11f, 0.08f);

        // Màu riêng của Người dân (Villager)
        static readonly Color VillagerShirt = new Color(0.30f, 0.55f, 0.72f);
        static readonly Color VillagerPants = new Color(0.34f, 0.30f, 0.25f);

        // Màu riêng của Thợ xây (Builder)
        static readonly Color BuilderHelmet = new Color(0.94f, 0.70f, 0.12f);
        static readonly Color BuilderShirt = new Color(0.78f, 0.43f, 0.18f);
        static readonly Color BuilderApron = new Color(0.38f, 0.24f, 0.13f);
        static readonly Color BuilderPants = new Color(0.22f, 0.29f, 0.38f);
        static readonly Color WoodHandle = new Color(0.48f, 0.31f, 0.16f);
        static readonly Color IronHead = new Color(0.46f, 0.49f, 0.53f);

        static readonly PartDef[] VillagerParts =
        {
            new PartDef { Bone = Hips, Center = new Vector3(0f, 0.02f, 0f), Size = new Vector3(0.32f, 0.15f, 0.19f), Color = VillagerPants },
            new PartDef { Bone = Spine, Center = new Vector3(0f, 0.25f, 0f), Size = new Vector3(0.38f, 0.44f, 0.22f), Color = VillagerShirt },
            new PartDef { Bone = Head, Center = new Vector3(0f, 0.13f, 0f), Size = new Vector3(0.23f, 0.25f, 0.23f), Color = HumanSkin },
            new PartDef { Bone = Head, Center = new Vector3(0f, 0.24f, -0.01f), Size = new Vector3(0.25f, 0.08f, 0.25f), Color = HairBrown },
            new PartDef { Bone = Head, Center = new Vector3(-0.05f, 0.14f, 0.116f), Size = new Vector3(0.04f, 0.035f, 0.01f), Color = EyeDark },
            new PartDef { Bone = Head, Center = new Vector3(0.05f, 0.14f, 0.116f), Size = new Vector3(0.04f, 0.035f, 0.01f), Color = EyeDark },
            new PartDef { Bone = ArmL, Center = new Vector3(0f, -0.13f, 0f), Size = new Vector3(0.11f, 0.28f, 0.11f), Color = VillagerShirt },
            new PartDef { Bone = ForearmL, Center = new Vector3(0f, -0.13f, 0f), Size = new Vector3(0.095f, 0.28f, 0.095f), Color = HumanSkin },
            new PartDef { Bone = ArmR, Center = new Vector3(0f, -0.13f, 0f), Size = new Vector3(0.11f, 0.28f, 0.11f), Color = VillagerShirt },
            new PartDef { Bone = ForearmR, Center = new Vector3(0f, -0.13f, 0f), Size = new Vector3(0.095f, 0.28f, 0.095f), Color = HumanSkin },
            new PartDef { Bone = ThighL, Center = new Vector3(0f, -0.21f, 0f), Size = new Vector3(0.13f, 0.44f, 0.13f), Color = VillagerPants },
            new PartDef { Bone = ShinL, Center = new Vector3(0f, -0.20f, 0f), Size = new Vector3(0.12f, 0.41f, 0.12f), Color = VillagerPants },
            new PartDef { Bone = ShinL, Center = new Vector3(0f, -0.41f, 0.035f), Size = new Vector3(0.13f, 0.07f, 0.20f), Color = BootsDark },
            new PartDef { Bone = ThighR, Center = new Vector3(0f, -0.21f, 0f), Size = new Vector3(0.13f, 0.44f, 0.13f), Color = VillagerPants },
            new PartDef { Bone = ShinR, Center = new Vector3(0f, -0.20f, 0f), Size = new Vector3(0.12f, 0.41f, 0.12f), Color = VillagerPants },
            new PartDef { Bone = ShinR, Center = new Vector3(0f, -0.41f, 0.035f), Size = new Vector3(0.13f, 0.07f, 0.20f), Color = BootsDark },
        };

        static readonly PartDef[] BuilderParts =
        {
            new PartDef { Bone = Hips, Center = new Vector3(0f, 0.02f, 0f), Size = new Vector3(0.34f, 0.16f, 0.20f), Color = BuilderApron },
            new PartDef { Bone = Spine, Center = new Vector3(0f, 0.25f, 0f), Size = new Vector3(0.40f, 0.45f, 0.23f), Color = BuilderShirt },
            new PartDef { Bone = Spine, Center = new Vector3(0f, 0.18f, 0.118f), Size = new Vector3(0.32f, 0.30f, 0.02f), Color = BuilderApron },
            new PartDef { Bone = Head, Center = new Vector3(0f, 0.13f, 0f), Size = new Vector3(0.23f, 0.24f, 0.23f), Color = HumanSkin },
            // Mũ cứng thợ xây + vành mũ trước
            new PartDef { Bone = Head, Center = new Vector3(0f, 0.25f, 0f), Size = new Vector3(0.27f, 0.09f, 0.27f), Color = BuilderHelmet },
            new PartDef { Bone = Head, Center = new Vector3(0f, 0.21f, 0.15f), Size = new Vector3(0.25f, 0.03f, 0.08f), Color = BuilderHelmet },
            new PartDef { Bone = Head, Center = new Vector3(-0.05f, 0.14f, 0.116f), Size = new Vector3(0.04f, 0.035f, 0.01f), Color = EyeDark },
            new PartDef { Bone = Head, Center = new Vector3(0.05f, 0.14f, 0.116f), Size = new Vector3(0.04f, 0.035f, 0.01f), Color = EyeDark },
            new PartDef { Bone = ArmL, Center = new Vector3(0f, -0.13f, 0f), Size = new Vector3(0.115f, 0.28f, 0.115f), Color = BuilderShirt },
            new PartDef { Bone = ForearmL, Center = new Vector3(0f, -0.13f, 0f), Size = new Vector3(0.10f, 0.28f, 0.10f), Color = HumanSkin },
            new PartDef { Bone = ArmR, Center = new Vector3(0f, -0.13f, 0f), Size = new Vector3(0.115f, 0.28f, 0.115f), Color = BuilderShirt },
            new PartDef { Bone = ForearmR, Center = new Vector3(0f, -0.13f, 0f), Size = new Vector3(0.10f, 0.28f, 0.10f), Color = HumanSkin },
            // Búa xây dựng: cán gỗ + đầu búa sắt
            new PartDef { Bone = ForearmR, Center = new Vector3(0f, -0.26f, 0.16f), Size = new Vector3(0.045f, 0.045f, 0.36f), Color = WoodHandle },
            new PartDef { Bone = ForearmR, Center = new Vector3(0f, -0.26f, 0.32f), Size = new Vector3(0.18f, 0.09f, 0.09f), Color = IronHead },
            new PartDef { Bone = ThighL, Center = new Vector3(0f, -0.21f, 0f), Size = new Vector3(0.135f, 0.44f, 0.135f), Color = BuilderPants },
            new PartDef { Bone = ShinL, Center = new Vector3(0f, -0.20f, 0f), Size = new Vector3(0.125f, 0.41f, 0.125f), Color = BuilderPants },
            new PartDef { Bone = ShinL, Center = new Vector3(0f, -0.41f, 0.035f), Size = new Vector3(0.135f, 0.075f, 0.21f), Color = BootsDark },
            new PartDef { Bone = ThighR, Center = new Vector3(0f, -0.21f, 0f), Size = new Vector3(0.135f, 0.44f, 0.135f), Color = BuilderPants },
            new PartDef { Bone = ShinR, Center = new Vector3(0f, -0.20f, 0f), Size = new Vector3(0.125f, 0.41f, 0.125f), Color = BuilderPants },
            new PartDef { Bone = ShinR, Center = new Vector3(0f, -0.41f, 0.035f), Size = new Vector3(0.135f, 0.075f, 0.21f), Color = BootsDark },
        };

        // Màu riêng của Thợ đốn gỗ (Lumberjack)
        static readonly Color LumberCap = new Color(0.58f, 0.16f, 0.14f);
        static readonly Color LumberShirt = new Color(0.72f, 0.20f, 0.18f);
        static readonly Color LumberSuspenders = new Color(0.32f, 0.20f, 0.11f);
        static readonly Color LumberPants = new Color(0.20f, 0.28f, 0.36f);
        static readonly Color SteelBlade = new Color(0.72f, 0.76f, 0.80f);

        // Màu riêng của Thợ mỏ đá (Miner)
        static readonly Color MinerHelmet = new Color(0.32f, 0.36f, 0.40f);
        static readonly Color MinerLamp = new Color(0.98f, 0.88f, 0.32f);
        static readonly Color MinerShirt = new Color(0.36f, 0.38f, 0.42f);
        static readonly Color MinerVest = new Color(0.52f, 0.40f, 0.24f);
        static readonly Color MinerPants = new Color(0.24f, 0.24f, 0.27f);

        // Màu riêng của Nông dân (Farmer)
        static readonly Color StrawHat = new Color(0.90f, 0.78f, 0.36f);
        static readonly Color StrawBand = new Color(0.68f, 0.22f, 0.16f);
        static readonly Color FarmerShirt = new Color(0.34f, 0.62f, 0.28f);
        static readonly Color FarmerOveralls = new Color(0.46f, 0.32f, 0.18f);
        static readonly Color FarmerPants = new Color(0.38f, 0.27f, 0.16f);

        static readonly PartDef[] LumberjackParts =
        {
            new PartDef { Bone = Hips, Center = new Vector3(0f, 0.02f, 0f), Size = new Vector3(0.34f, 0.16f, 0.20f), Color = LumberPants },
            new PartDef { Bone = Spine, Center = new Vector3(0f, 0.25f, 0f), Size = new Vector3(0.40f, 0.45f, 0.23f), Color = LumberShirt },
            new PartDef { Bone = Spine, Center = new Vector3(-0.10f, 0.25f, 0.118f), Size = new Vector3(0.05f, 0.44f, 0.02f), Color = LumberSuspenders },
            new PartDef { Bone = Spine, Center = new Vector3(0.10f, 0.25f, 0.118f), Size = new Vector3(0.05f, 0.44f, 0.02f), Color = LumberSuspenders },
            new PartDef { Bone = Head, Center = new Vector3(0f, 0.13f, 0f), Size = new Vector3(0.23f, 0.24f, 0.23f), Color = HumanSkin },
            // Râu quai nón + mũ len tiều phu
            new PartDef { Bone = Head, Center = new Vector3(0f, 0.05f, 0.08f), Size = new Vector3(0.24f, 0.10f, 0.12f), Color = HairBrown },
            new PartDef { Bone = Head, Center = new Vector3(0f, 0.25f, -0.01f), Size = new Vector3(0.26f, 0.10f, 0.26f), Color = LumberCap },
            new PartDef { Bone = Head, Center = new Vector3(-0.05f, 0.14f, 0.116f), Size = new Vector3(0.04f, 0.035f, 0.01f), Color = EyeDark },
            new PartDef { Bone = Head, Center = new Vector3(0.05f, 0.14f, 0.116f), Size = new Vector3(0.04f, 0.035f, 0.01f), Color = EyeDark },
            new PartDef { Bone = ArmL, Center = new Vector3(0f, -0.13f, 0f), Size = new Vector3(0.115f, 0.28f, 0.115f), Color = LumberShirt },
            new PartDef { Bone = ForearmL, Center = new Vector3(0f, -0.13f, 0f), Size = new Vector3(0.10f, 0.28f, 0.10f), Color = HumanSkin },
            new PartDef { Bone = ArmR, Center = new Vector3(0f, -0.13f, 0f), Size = new Vector3(0.115f, 0.28f, 0.115f), Color = LumberShirt },
            new PartDef { Bone = ForearmR, Center = new Vector3(0f, -0.13f, 0f), Size = new Vector3(0.10f, 0.28f, 0.10f), Color = HumanSkin },
            // Rìu chặt cây: cán gỗ dài + lưỡi rìu bản rộng
            new PartDef { Bone = ForearmR, Center = new Vector3(0f, -0.26f, 0.20f), Size = new Vector3(0.04f, 0.04f, 0.46f), Color = WoodHandle },
            new PartDef { Bone = ForearmR, Center = new Vector3(0f, -0.31f, 0.38f), Size = new Vector3(0.03f, 0.16f, 0.14f), Color = SteelBlade },
            new PartDef { Bone = ThighL, Center = new Vector3(0f, -0.21f, 0f), Size = new Vector3(0.135f, 0.44f, 0.135f), Color = LumberPants },
            new PartDef { Bone = ShinL, Center = new Vector3(0f, -0.20f, 0f), Size = new Vector3(0.125f, 0.41f, 0.125f), Color = LumberPants },
            new PartDef { Bone = ShinL, Center = new Vector3(0f, -0.41f, 0.035f), Size = new Vector3(0.135f, 0.075f, 0.21f), Color = BootsDark },
            new PartDef { Bone = ThighR, Center = new Vector3(0f, -0.21f, 0f), Size = new Vector3(0.135f, 0.44f, 0.135f), Color = LumberPants },
            new PartDef { Bone = ShinR, Center = new Vector3(0f, -0.20f, 0f), Size = new Vector3(0.125f, 0.41f, 0.125f), Color = LumberPants },
            new PartDef { Bone = ShinR, Center = new Vector3(0f, -0.41f, 0.035f), Size = new Vector3(0.135f, 0.075f, 0.21f), Color = BootsDark },
        };

        static readonly PartDef[] MinerParts =
        {
            new PartDef { Bone = Hips, Center = new Vector3(0f, 0.02f, 0f), Size = new Vector3(0.35f, 0.16f, 0.21f), Color = MinerPants },
            new PartDef { Bone = Spine, Center = new Vector3(0f, 0.25f, 0f), Size = new Vector3(0.41f, 0.45f, 0.24f), Color = MinerShirt },
            new PartDef { Bone = Spine, Center = new Vector3(0f, 0.24f, 0f), Size = new Vector3(0.43f, 0.36f, 0.25f), Color = MinerVest },
            new PartDef { Bone = Head, Center = new Vector3(0f, 0.13f, 0f), Size = new Vector3(0.23f, 0.24f, 0.23f), Color = HumanSkin },
            // Mũ sắt thợ mỏ + đèn pin trước trán
            new PartDef { Bone = Head, Center = new Vector3(0f, 0.25f, 0f), Size = new Vector3(0.27f, 0.09f, 0.27f), Color = MinerHelmet },
            new PartDef { Bone = Head, Center = new Vector3(0f, 0.26f, 0.15f), Size = new Vector3(0.07f, 0.07f, 0.05f), Color = MinerLamp },
            new PartDef { Bone = Head, Center = new Vector3(-0.05f, 0.14f, 0.116f), Size = new Vector3(0.04f, 0.035f, 0.01f), Color = EyeDark },
            new PartDef { Bone = Head, Center = new Vector3(0.05f, 0.14f, 0.116f), Size = new Vector3(0.04f, 0.035f, 0.01f), Color = EyeDark },
            new PartDef { Bone = ArmL, Center = new Vector3(0f, -0.13f, 0f), Size = new Vector3(0.115f, 0.28f, 0.115f), Color = MinerShirt },
            new PartDef { Bone = ForearmL, Center = new Vector3(0f, -0.13f, 0f), Size = new Vector3(0.10f, 0.28f, 0.10f), Color = HumanSkin },
            new PartDef { Bone = ArmR, Center = new Vector3(0f, -0.13f, 0f), Size = new Vector3(0.115f, 0.28f, 0.115f), Color = MinerShirt },
            new PartDef { Bone = ForearmR, Center = new Vector3(0f, -0.13f, 0f), Size = new Vector3(0.10f, 0.28f, 0.10f), Color = HumanSkin },
            // Cuốc chim (Pickaxe): cán gỗ + mũi cuốc nhọn 2 đầu
            new PartDef { Bone = ForearmR, Center = new Vector3(0f, -0.26f, 0.20f), Size = new Vector3(0.04f, 0.04f, 0.46f), Color = WoodHandle },
            new PartDef { Bone = ForearmR, Center = new Vector3(0f, -0.26f, 0.40f), Size = new Vector3(0.04f, 0.28f, 0.05f), Color = IronHead },
            new PartDef { Bone = ThighL, Center = new Vector3(0f, -0.21f, 0f), Size = new Vector3(0.135f, 0.44f, 0.135f), Color = MinerPants },
            new PartDef { Bone = ShinL, Center = new Vector3(0f, -0.20f, 0f), Size = new Vector3(0.125f, 0.41f, 0.125f), Color = MinerPants },
            new PartDef { Bone = ShinL, Center = new Vector3(0f, -0.41f, 0.035f), Size = new Vector3(0.135f, 0.075f, 0.21f), Color = BootsDark },
            new PartDef { Bone = ThighR, Center = new Vector3(0f, -0.21f, 0f), Size = new Vector3(0.135f, 0.44f, 0.135f), Color = MinerPants },
            new PartDef { Bone = ShinR, Center = new Vector3(0f, -0.20f, 0f), Size = new Vector3(0.125f, 0.41f, 0.125f), Color = MinerPants },
            new PartDef { Bone = ShinR, Center = new Vector3(0f, -0.41f, 0.035f), Size = new Vector3(0.135f, 0.075f, 0.21f), Color = BootsDark },
        };

        static readonly PartDef[] FarmerParts =
        {
            new PartDef { Bone = Hips, Center = new Vector3(0f, 0.02f, 0f), Size = new Vector3(0.34f, 0.16f, 0.20f), Color = FarmerOveralls },
            new PartDef { Bone = Spine, Center = new Vector3(0f, 0.25f, 0f), Size = new Vector3(0.39f, 0.44f, 0.22f), Color = FarmerShirt },
            new PartDef { Bone = Spine, Center = new Vector3(0f, 0.19f, 0.115f), Size = new Vector3(0.30f, 0.32f, 0.02f), Color = FarmerOveralls },
            new PartDef { Bone = Head, Center = new Vector3(0f, 0.13f, 0f), Size = new Vector3(0.23f, 0.24f, 0.23f), Color = HumanSkin },
            // Mũ rơm nông dân vành rộng + dải băng đỏ
            new PartDef { Bone = Head, Center = new Vector3(0f, 0.22f, 0f), Size = new Vector3(0.42f, 0.03f, 0.42f), Color = StrawHat },
            new PartDef { Bone = Head, Center = new Vector3(0f, 0.25f, 0f), Size = new Vector3(0.25f, 0.04f, 0.25f), Color = StrawBand },
            new PartDef { Bone = Head, Center = new Vector3(0f, 0.29f, 0f), Size = new Vector3(0.23f, 0.07f, 0.23f), Color = StrawHat },
            new PartDef { Bone = Head, Center = new Vector3(-0.05f, 0.14f, 0.116f), Size = new Vector3(0.04f, 0.035f, 0.01f), Color = EyeDark },
            new PartDef { Bone = Head, Center = new Vector3(0.05f, 0.14f, 0.116f), Size = new Vector3(0.04f, 0.035f, 0.01f), Color = EyeDark },
            new PartDef { Bone = ArmL, Center = new Vector3(0f, -0.13f, 0f), Size = new Vector3(0.11f, 0.28f, 0.11f), Color = FarmerShirt },
            new PartDef { Bone = ForearmL, Center = new Vector3(0f, -0.13f, 0f), Size = new Vector3(0.095f, 0.28f, 0.095f), Color = HumanSkin },
            new PartDef { Bone = ArmR, Center = new Vector3(0f, -0.13f, 0f), Size = new Vector3(0.11f, 0.28f, 0.11f), Color = FarmerShirt },
            new PartDef { Bone = ForearmR, Center = new Vector3(0f, -0.13f, 0f), Size = new Vector3(0.095f, 0.28f, 0.095f), Color = HumanSkin },
            // Cuốc làm ruộng (Hoe): cán gỗ dài + lưỡi cuốc bản ngang
            new PartDef { Bone = ForearmR, Center = new Vector3(0f, -0.26f, 0.22f), Size = new Vector3(0.038f, 0.038f, 0.50f), Color = WoodHandle },
            new PartDef { Bone = ForearmR, Center = new Vector3(0f, -0.34f, 0.44f), Size = new Vector3(0.14f, 0.14f, 0.03f), Color = IronHead },
            new PartDef { Bone = ThighL, Center = new Vector3(0f, -0.21f, 0f), Size = new Vector3(0.13f, 0.44f, 0.13f), Color = FarmerPants },
            new PartDef { Bone = ShinL, Center = new Vector3(0f, -0.20f, 0f), Size = new Vector3(0.12f, 0.41f, 0.12f), Color = FarmerPants },
            new PartDef { Bone = ShinL, Center = new Vector3(0f, -0.41f, 0.035f), Size = new Vector3(0.13f, 0.075f, 0.20f), Color = BootsDark },
            new PartDef { Bone = ThighR, Center = new Vector3(0f, -0.21f, 0f), Size = new Vector3(0.13f, 0.44f, 0.13f), Color = FarmerPants },
            new PartDef { Bone = ShinR, Center = new Vector3(0f, -0.20f, 0f), Size = new Vector3(0.12f, 0.41f, 0.12f), Color = FarmerPants },
            new PartDef { Bone = ShinR, Center = new Vector3(0f, -0.41f, 0.035f), Size = new Vector3(0.13f, 0.075f, 0.20f), Color = BootsDark },
        };

        [MenuItem("SurvivalRts/Tạo dân và công trình xây dựng")]
        public static void GenerateAll()
        {
            EnsureFolder(ModelDir);
            EnsureFolder(AnimDir);
            EnsureFolder(TextureDir);
            EnsureFolder(MaterialDir);
            EnsureFolder(AnimDataDir);
            EnsureFolder(UnitPrefabDir);
            EnsureFolder(BuildingPrefabDir);
            EnsureFolder(BuildingDataDir);

            OrganizeLegacyAssets();
            GenerateResourceIcons();
            GenerateVillager();
            GenerateBuilder();
            GenerateLumberjack();
            GenerateMiner();
            GenerateFarmer();
            GenerateConstructionStages();
            GenerateHouseBuilding();
            GenerateBuilderHutBuilding();
            GenerateLumberCampBuilding();
            GenerateStoneQuarryBuilding();
            GenerateIronMineBuilding();
            GenerateFarmBuilding();
            ReassignTestBuildings();
            WireActiveScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Đã tạo xong bộ model & animation cho Villager, Builder, Lumberjack, Miner, Farmer và các công trình House, BuilderHut, LumberCamp, StoneQuarry, IronMine, Farm.");
        }

        // Di chuyển các file cũ nằm ngoài thư mục gốc Models và Animations vào từng thư mục con theo tên nhân vật
        static void OrganizeLegacyAssets()
        {
            string[] units = { "Villager", "Builder", "Lumberjack", "Miner", "Farmer", "EnemyGrunt" };
            string[] clips = { "Idle", "Walk", "Build", "Work", "Attack", "Death" };

            for (int u = 0; u < units.Length; u++)
            {
                string unit = units[u];
                EnsureFolder(ModelDir + "/" + unit);
                EnsureFolder(AnimDir + "/" + unit);

                MoveIfExists(ModelDir + "/" + unit + "_Mesh.asset", ModelDir + "/" + unit + "/" + unit + "_Mesh.asset");
                MoveIfExists(ModelDir + "/" + unit + "_Rig.prefab", ModelDir + "/" + unit + "/" + unit + "_Rig.prefab");

                for (int c = 0; c < clips.Length; c++)
                {
                    string fileName = unit + "_" + clips[c] + ".anim";
                    MoveIfExists(AnimDir + "/" + fileName, AnimDir + "/" + unit + "/" + fileName);
                }
            }
        }

        static void MoveIfExists(string oldPath, string newPath)
        {
            if (AssetDatabase.LoadMainAssetAtPath(oldPath) == null) return;
            if (AssetDatabase.LoadMainAssetAtPath(newPath) != null)
            {
                AssetDatabase.DeleteAsset(oldPath);
                return;
            }
            AssetDatabase.MoveAsset(oldPath, newPath);
        }

        static void GenerateVillager()
        {
            EnsureFolder(ModelDir + "/Villager");
            EnsureFolder(AnimDir + "/Villager");
            var root = new GameObject("Villager_Rig");
            try
            {
                var bones = BuildRig(root);
                var bindposes = BuildBindposes(root, bones);
                var mesh = BuildCharacterMesh("Villager_Mesh", bones, bindposes, VillagerParts);
                mesh = SaveAsset(mesh, ModelDir + "/Villager/Villager_Mesh.asset");

                var sources = new List<GpuAnimationBaker.ClipSource>
                {
                    MakeClip(bones, root, "Villager", "Idle", 2f, true, VillagerIdlePose),
                    MakeClip(bones, root, "Villager", "Walk", 0.8f, true, VillagerWalkPose),
                    MakeClip(bones, root, "Villager", "Death", 1.2f, false, DeathPose),
                };

                var animData = GpuAnimationBaker.Bake(root, bones, bindposes, sources, BakeFps,
                    TextureDir + "/Villager_BoneTex.asset", AnimDataDir + "/Villager_Anim.asset");

                ResetPose(bones);
                SaveRigPrefab(root, bones, mesh, "Villager", VillagerShirt);

                var material = CreateGpuSkinnedMaterial("M_Villager", animData.boneTexture);
                CreateUnitPrefab("Villager", mesh, material, animData);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static void GenerateBuilder()
        {
            EnsureFolder(ModelDir + "/Builder");
            EnsureFolder(AnimDir + "/Builder");
            var root = new GameObject("Builder_Rig");
            try
            {
                var bones = BuildRig(root);
                var bindposes = BuildBindposes(root, bones);
                var mesh = BuildCharacterMesh("Builder_Mesh", bones, bindposes, BuilderParts);
                mesh = SaveAsset(mesh, ModelDir + "/Builder/Builder_Mesh.asset");

                var sources = new List<GpuAnimationBaker.ClipSource>
                {
                    MakeClip(bones, root, "Builder", "Idle", 2f, true, BuilderIdlePose),
                    MakeClip(bones, root, "Builder", "Walk", 0.8f, true, BuilderWalkPose),
                    MakeClip(bones, root, "Builder", "Build", 0.75f, true, BuilderHammerPose),
                    MakeClip(bones, root, "Builder", "Death", 1.2f, false, DeathPose),
                };

                var animData = GpuAnimationBaker.Bake(root, bones, bindposes, sources, BakeFps,
                    TextureDir + "/Builder_BoneTex.asset", AnimDataDir + "/Builder_Anim.asset");

                ResetPose(bones);
                SaveRigPrefab(root, bones, mesh, "Builder", BuilderShirt);

                var material = CreateGpuSkinnedMaterial("M_Builder", animData.boneTexture);
                CreateUnitPrefab("Builder", mesh, material, animData);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static void GenerateLumberjack()
        {
            EnsureFolder(ModelDir + "/Lumberjack");
            EnsureFolder(AnimDir + "/Lumberjack");
            var root = new GameObject("Lumberjack_Rig");
            try
            {
                var bones = BuildRig(root);
                var bindposes = BuildBindposes(root, bones);
                var mesh = BuildCharacterMesh("Lumberjack_Mesh", bones, bindposes, LumberjackParts);
                mesh = SaveAsset(mesh, ModelDir + "/Lumberjack/Lumberjack_Mesh.asset");

                var sources = new List<GpuAnimationBaker.ClipSource>
                {
                    MakeClip(bones, root, "Lumberjack", "Idle", 2f, true, BuilderIdlePose),
                    MakeClip(bones, root, "Lumberjack", "Walk", 0.8f, true, BuilderWalkPose),
                    MakeClip(bones, root, "Lumberjack", "Work", 0.85f, true, LumberjackChopPose),
                    MakeClip(bones, root, "Lumberjack", "Death", 1.2f, false, DeathPose),
                };

                var animData = GpuAnimationBaker.Bake(root, bones, bindposes, sources, BakeFps,
                    TextureDir + "/Lumberjack_BoneTex.asset", AnimDataDir + "/Lumberjack_Anim.asset");

                ResetPose(bones);
                SaveRigPrefab(root, bones, mesh, "Lumberjack", LumberShirt);

                var material = CreateGpuSkinnedMaterial("M_Lumberjack", animData.boneTexture);
                CreateUnitPrefab("Lumberjack", mesh, material, animData);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static void GenerateMiner()
        {
            EnsureFolder(ModelDir + "/Miner");
            EnsureFolder(AnimDir + "/Miner");
            var root = new GameObject("Miner_Rig");
            try
            {
                var bones = BuildRig(root);
                var bindposes = BuildBindposes(root, bones);
                var mesh = BuildCharacterMesh("Miner_Mesh", bones, bindposes, MinerParts);
                mesh = SaveAsset(mesh, ModelDir + "/Miner/Miner_Mesh.asset");

                var sources = new List<GpuAnimationBaker.ClipSource>
                {
                    MakeClip(bones, root, "Miner", "Idle", 2f, true, BuilderIdlePose),
                    MakeClip(bones, root, "Miner", "Walk", 0.8f, true, BuilderWalkPose),
                    MakeClip(bones, root, "Miner", "Work", 0.80f, true, MinerPickPose),
                    MakeClip(bones, root, "Miner", "Death", 1.2f, false, DeathPose),
                };

                var animData = GpuAnimationBaker.Bake(root, bones, bindposes, sources, BakeFps,
                    TextureDir + "/Miner_BoneTex.asset", AnimDataDir + "/Miner_Anim.asset");

                ResetPose(bones);
                SaveRigPrefab(root, bones, mesh, "Miner", MinerVest);

                var material = CreateGpuSkinnedMaterial("M_Miner", animData.boneTexture);
                CreateUnitPrefab("Miner", mesh, material, animData);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static void GenerateFarmer()
        {
            EnsureFolder(ModelDir + "/Farmer");
            EnsureFolder(AnimDir + "/Farmer");
            var root = new GameObject("Farmer_Rig");
            try
            {
                var bones = BuildRig(root);
                var bindposes = BuildBindposes(root, bones);
                var mesh = BuildCharacterMesh("Farmer_Mesh", bones, bindposes, FarmerParts);
                mesh = SaveAsset(mesh, ModelDir + "/Farmer/Farmer_Mesh.asset");

                var sources = new List<GpuAnimationBaker.ClipSource>
                {
                    MakeClip(bones, root, "Farmer", "Idle", 2f, true, BuilderIdlePose),
                    MakeClip(bones, root, "Farmer", "Walk", 0.8f, true, BuilderWalkPose),
                    MakeClip(bones, root, "Farmer", "Work", 0.90f, true, FarmerHoePose),
                    MakeClip(bones, root, "Farmer", "Death", 1.2f, false, DeathPose),
                };

                var animData = GpuAnimationBaker.Bake(root, bones, bindposes, sources, BakeFps,
                    TextureDir + "/Farmer_BoneTex.asset", AnimDataDir + "/Farmer_Anim.asset");

                ResetPose(bones);
                SaveRigPrefab(root, bones, mesh, "Farmer", FarmerShirt);

                var material = CreateGpuSkinnedMaterial("M_Farmer", animData.boneTexture);
                CreateUnitPrefab("Farmer", mesh, material, animData);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ---------- Sinh 3 trạng thái khung xây dựng (chuẩn kích thước 1x1x1m) ----------

        static void GenerateConstructionStages()
        {
            var woodMat = LoadOrCreateLitMaterial(TestMatDir + "/M_Wood.mat", new Color(0.55f, 0.36f, 0.18f));
            var stoneMat = LoadOrCreateLitMaterial(TestMatDir + "/M_Stone.mat", new Color(0.52f, 0.52f, 0.50f));
            var bodyMat = LoadOrCreateLitMaterial(TestMatDir + "/M_Building.mat", new Color(0.68f, 0.62f, 0.52f));

            // Stage 1 (0% - 33%): Nền móng + 4 cọc góc thấp + ván sàn
            var stage1 = new GameObject("Construction_Stage1");
            try
            {
                AddBox(stage1.transform, "Foundation", new Vector3(0f, 0.04f, 0f), new Vector3(0.92f, 0.08f, 0.92f), stoneMat, false);
                AddCornerPosts(stage1.transform, 0.44f, 0.30f, 0.05f, woodMat);
                AddBox(stage1.transform, "Plank1", new Vector3(-0.15f, 0.09f, 0f), new Vector3(0.22f, 0.02f, 0.75f), woodMat, false);
                AddBox(stage1.transform, "Plank2", new Vector3(0.18f, 0.09f, 0.05f), new Vector3(0.22f, 0.02f, 0.65f), woodMat, false);
                PrefabUtility.SaveAsPrefabAsset(stage1, BuildingPrefabDir + "/Construction_Stage1.prefab");
            }
            finally
            {
                Object.DestroyImmediate(stage1);
            }

            // Stage 2 (33% - 66%): Khung giàn giáo tầng 1 + thanh giằng ngang + lõi công trình cao 45%
            var stage2 = new GameObject("Construction_Stage2");
            try
            {
                AddBox(stage2.transform, "Foundation", new Vector3(0f, 0.04f, 0f), new Vector3(0.92f, 0.08f, 0.92f), stoneMat, false);
                AddBox(stage2.transform, "CoreHalf", new Vector3(0f, 0.24f, 0f), new Vector3(0.68f, 0.40f, 0.68f), bodyMat, false);
                AddCornerPosts(stage2.transform, 0.44f, 0.62f, 0.045f, woodMat);
                AddRingBeams(stage2.transform, 0.44f, 0.32f, 0.035f, woodMat);
                AddRingBeams(stage2.transform, 0.44f, 0.58f, 0.035f, woodMat);
                PrefabUtility.SaveAsPrefabAsset(stage2, BuildingPrefabDir + "/Construction_Stage2.prefab");
            }
            finally
            {
                Object.DestroyImmediate(stage2);
            }

            // Stage 3 (66% - 99%): Giàn giáo 2 tầng cao tới đỉnh + sàn gỗ + lõi công trình cao 85%
            var stage3 = new GameObject("Construction_Stage3");
            try
            {
                AddBox(stage3.transform, "Foundation", new Vector3(0f, 0.04f, 0f), new Vector3(0.92f, 0.08f, 0.92f), stoneMat, false);
                AddBox(stage3.transform, "CoreNearlyDone", new Vector3(0f, 0.44f, 0f), new Vector3(0.72f, 0.80f, 0.72f), bodyMat, false);
                AddCornerPosts(stage3.transform, 0.44f, 0.96f, 0.045f, woodMat);
                AddRingBeams(stage3.transform, 0.44f, 0.32f, 0.035f, woodMat);
                AddRingBeams(stage3.transform, 0.44f, 0.62f, 0.035f, woodMat);
                AddRingBeams(stage3.transform, 0.44f, 0.92f, 0.035f, woodMat);
                AddBox(stage3.transform, "ScaffoldBoardL", new Vector3(-0.44f, 0.64f, 0f), new Vector3(0.10f, 0.02f, 0.88f), woodMat, false);
                AddBox(stage3.transform, "ScaffoldBoardR", new Vector3(0.44f, 0.64f, 0f), new Vector3(0.10f, 0.02f, 0.88f), woodMat, false);
                PrefabUtility.SaveAsPrefabAsset(stage3, BuildingPrefabDir + "/Construction_Stage3.prefab");
            }
            finally
            {
                Object.DestroyImmediate(stage3);
            }
        }

        static void AddCornerPosts(Transform parent, float offset, float height, float thickness, Material mat)
        {
            float y = height * 0.5f;
            var size = new Vector3(thickness, height, thickness);
            AddBox(parent, "PostFL", new Vector3(-offset, y, offset), size, mat, false);
            AddBox(parent, "PostFR", new Vector3(offset, y, offset), size, mat, false);
            AddBox(parent, "PostBL", new Vector3(-offset, y, -offset), size, mat, false);
            AddBox(parent, "PostBR", new Vector3(offset, y, -offset), size, mat, false);
        }

        static void AddRingBeams(Transform parent, float offset, float y, float thickness, Material mat)
        {
            float span = offset * 2f;
            AddBox(parent, "BeamF_" + y, new Vector3(0f, y, offset), new Vector3(span, thickness, thickness), mat, false);
            AddBox(parent, "BeamB_" + y, new Vector3(0f, y, -offset), new Vector3(span, thickness, thickness), mat, false);
            AddBox(parent, "BeamL_" + y, new Vector3(-offset, y, 0f), new Vector3(thickness, thickness, span), mat, false);
            AddBox(parent, "BeamR_" + y, new Vector3(offset, y, 0f), new Vector3(thickness, thickness, span), mat, false);
        }

        // ---------- Sinh các công trình kinh tế ----------

        static void GenerateHouseBuilding()
        {
            var stoneMat = LoadOrCreateLitMaterial(TestMatDir + "/M_Stone.mat", new Color(0.52f, 0.52f, 0.50f));
            var wallMat = LoadOrCreateLitMaterial(MaterialDir + "/M_House_Wall.mat", new Color(0.82f, 0.75f, 0.62f));
            var roofMat = LoadOrCreateLitMaterial(MaterialDir + "/M_House_Roof.mat", new Color(0.65f, 0.26f, 0.18f));
            var woodMat = LoadOrCreateLitMaterial(TestMatDir + "/M_Wood.mat", new Color(0.55f, 0.36f, 0.18f));
            var frontMat = LoadOrCreateLitMaterial(TestMatDir + "/M_Front.mat", new Color(0.90f, 0.75f, 0.20f));

            var house = new GameObject("House");
            try
            {
                AddBox(house.transform, "Foundation", new Vector3(0f, 0.1f, 0f), new Vector3(1.85f, 0.2f, 1.85f), stoneMat, true);
                AddBox(house.transform, "Body", new Vector3(0f, 0.9f, 0f), new Vector3(1.65f, 1.4f, 1.65f), wallMat, true);
                AddBox(house.transform, "RoofLower", new Vector3(0f, 1.75f, 0f), new Vector3(1.9f, 0.3f, 1.9f), roofMat, true);
                AddBox(house.transform, "RoofUpper", new Vector3(0f, 2.05f, 0f), new Vector3(1.35f, 0.3f, 1.35f), roofMat, true);
                AddBox(house.transform, "RoofPeak", new Vector3(0f, 2.28f, 0f), new Vector3(0.75f, 0.18f, 0.75f), roofMat, false);
                AddBox(house.transform, "Chimney", new Vector3(0.45f, 2.15f, -0.35f), new Vector3(0.28f, 0.7f, 0.28f), stoneMat, false);
                AddBox(house.transform, "DoorFrame", new Vector3(0f, 0.55f, 0.83f), new Vector3(0.46f, 0.85f, 0.08f), woodMat, false);
                AddBox(house.transform, "FrontMarker", new Vector3(0f, 0.22f, 0.90f), new Vector3(0.55f, 0.4f, 0.22f), frontMat, false);

                var prefab = PrefabUtility.SaveAsPrefabAsset(house, BuildingPrefabDir + "/House.prefab");
                SaveBuildingData(BuildingDataDir + "/Building_House.asset", "House", BuildingCategory.Economy,
                    prefab, new Vector2Int(2, 2), 2.5f, 250f, 5f, 3, 0, ResourceType.None, 0f, false, 0f, 0);
            }
            finally
            {
                Object.DestroyImmediate(house);
            }
        }

        static void GenerateBuilderHutBuilding()
        {
            var stoneMat = LoadOrCreateLitMaterial(TestMatDir + "/M_Stone.mat", new Color(0.52f, 0.52f, 0.50f));
            var woodMat = LoadOrCreateLitMaterial(TestMatDir + "/M_Wood.mat", new Color(0.55f, 0.36f, 0.18f));
            var hutBodyMat = LoadOrCreateLitMaterial(MaterialDir + "/M_BuilderHut_Body.mat", new Color(0.68f, 0.50f, 0.32f));
            var hutRoofMat = LoadOrCreateLitMaterial(MaterialDir + "/M_BuilderHut_Roof.mat", new Color(0.84f, 0.58f, 0.16f));
            var frontMat = LoadOrCreateLitMaterial(TestMatDir + "/M_Front.mat", new Color(0.90f, 0.75f, 0.20f));

            var hut = new GameObject("BuilderHut");
            try
            {
                AddBox(hut.transform, "Foundation", new Vector3(0f, 0.1f, 0f), new Vector3(1.9f, 0.2f, 1.9f), stoneMat, true);
                AddBox(hut.transform, "MainCabin", new Vector3(-0.22f, 0.95f, -0.15f), new Vector3(1.25f, 1.5f, 1.4f), hutBodyMat, true);
                AddBox(hut.transform, "CabinRoof", new Vector3(-0.22f, 1.85f, -0.15f), new Vector3(1.45f, 0.3f, 1.6f), hutRoofMat, true);
                // Mái hiên và khu vực bàn thợ bên phải
                AddBox(hut.transform, "CanopyRoof", new Vector3(0.52f, 1.45f, 0f), new Vector3(0.7f, 0.14f, 1.5f), hutRoofMat, false);
                AddBox(hut.transform, "CanopyPostF", new Vector3(0.78f, 0.75f, 0.62f), new Vector3(0.1f, 1.3f, 0.1f), woodMat, false);
                AddBox(hut.transform, "CanopyPostB", new Vector3(0.78f, 0.75f, -0.62f), new Vector3(0.1f, 1.3f, 0.1f), woodMat, false);
                AddBox(hut.transform, "Worktable", new Vector3(0.55f, 0.45f, -0.15f), new Vector3(0.42f, 0.5f, 0.75f), woodMat, false);
                AddBox(hut.transform, "TimberLog1", new Vector3(0.52f, 0.28f, 0.50f), new Vector3(0.45f, 0.16f, 0.22f), woodMat, false);
                AddBox(hut.transform, "TimberLog2", new Vector3(0.52f, 0.44f, 0.50f), new Vector3(0.38f, 0.15f, 0.18f), woodMat, false);
                AddBox(hut.transform, "FrontMarker", new Vector3(-0.22f, 0.22f, 0.85f), new Vector3(0.55f, 0.4f, 0.22f), frontMat, false);

                var prefab = PrefabUtility.SaveAsPrefabAsset(hut, BuildingPrefabDir + "/BuilderHut.prefab");
                SaveBuildingData(BuildingDataDir + "/Building_BuilderHut.asset", "Builder's Hut", BuildingCategory.Economy,
                    prefab, new Vector2Int(2, 2), 2.2f, 300f, 6f, 0, 3, ResourceType.None, 0f, false, 0f, 0);
            }
            finally
            {
                Object.DestroyImmediate(hut);
            }
        }

        // Trại khai thác gỗ (Lumber Camp): kích thước 2x2, có lán cưa, bàn cưa và đống gỗ tròn xếp chồng
        static void GenerateLumberCampBuilding()
        {
            var stoneMat = LoadOrCreateLitMaterial(TestMatDir + "/M_Stone.mat", new Color(0.52f, 0.52f, 0.50f));
            var woodMat = LoadOrCreateLitMaterial(TestMatDir + "/M_Wood.mat", new Color(0.55f, 0.36f, 0.18f));
            var logMat = LoadOrCreateLitMaterial(MaterialDir + "/M_Lumber_Log.mat", new Color(0.44f, 0.28f, 0.14f));
            var roofMat = LoadOrCreateLitMaterial(MaterialDir + "/M_Lumber_Roof.mat", new Color(0.28f, 0.46f, 0.22f));
            var sawMat = LoadOrCreateLitMaterial(MaterialDir + "/M_Steel.mat", new Color(0.74f, 0.78f, 0.82f));
            var frontMat = LoadOrCreateLitMaterial(TestMatDir + "/M_Front.mat", new Color(0.90f, 0.75f, 0.20f));

            var camp = new GameObject("LumberCamp");
            try
            {
                AddBox(camp.transform, "Foundation", new Vector3(0f, 0.08f, 0f), new Vector3(1.9f, 0.16f, 1.9f), stoneMat, true);
                AddBox(camp.transform, "Cabin", new Vector3(-0.32f, 0.80f, -0.22f), new Vector3(1.10f, 1.30f, 1.25f), woodMat, true);
                AddBox(camp.transform, "CabinRoof", new Vector3(-0.32f, 1.56f, -0.22f), new Vector3(1.30f, 0.24f, 1.45f), roofMat, true);
                // Bàn cưa gỗ và lưỡi cưa tròn bên phải
                AddBox(camp.transform, "SawTable", new Vector3(0.48f, 0.42f, -0.15f), new Vector3(0.50f, 0.52f, 0.95f), woodMat, false);
                AddBox(camp.transform, "SawBlade", new Vector3(0.48f, 0.76f, -0.15f), new Vector3(0.04f, 0.32f, 0.32f), sawMat, false);
                // Đống khúc gỗ xếp chồng phía trước
                AddBox(camp.transform, "LogPile1", new Vector3(0.45f, 0.26f, 0.58f), new Vector3(0.68f, 0.20f, 0.22f), logMat, false);
                AddBox(camp.transform, "LogPile2", new Vector3(0.45f, 0.26f, 0.80f), new Vector3(0.68f, 0.20f, 0.22f), logMat, false);
                AddBox(camp.transform, "LogPile3", new Vector3(0.45f, 0.45f, 0.69f), new Vector3(0.62f, 0.18f, 0.20f), logMat, false);
                AddBox(camp.transform, "ChoppingBlock", new Vector3(-0.42f, 0.28f, 0.62f), new Vector3(0.32f, 0.24f, 0.32f), logMat, false);
                AddBox(camp.transform, "FrontMarker", new Vector3(-0.32f, 0.22f, 0.88f), new Vector3(0.50f, 0.36f, 0.20f), frontMat, false);

                var prefab = PrefabUtility.SaveAsPrefabAsset(camp, BuildingPrefabDir + "/LumberCamp.prefab");
                SaveBuildingData(BuildingDataDir + "/Building_LumberCamp.asset", "Lumber Camp", BuildingCategory.Economy,
                    prefab, new Vector2Int(2, 2), 1.9f, 280f, 5f, 0, 3, ResourceType.Wood, 8f, false, 2.5f, 2);
            }
            finally
            {
                Object.DestroyImmediate(camp);
            }
        }

        // Xưởng khai thác đá (Stone Quarry): kích thước 2x2, mặt trước (+Z) có cần trục khai thác và máng trượt hướng thẳng vào mỏ đá
        static void GenerateStoneQuarryBuilding()
        {
            var stoneMat = LoadOrCreateLitMaterial(TestMatDir + "/M_Stone.mat", new Color(0.52f, 0.52f, 0.50f));
            var darkStoneMat = LoadOrCreateLitMaterial(MaterialDir + "/M_Quarry_Stone.mat", new Color(0.36f, 0.38f, 0.42f));
            var woodMat = LoadOrCreateLitMaterial(TestMatDir + "/M_Wood.mat", new Color(0.55f, 0.36f, 0.18f));
            var ironMat = LoadOrCreateLitMaterial(MaterialDir + "/M_Steel.mat", new Color(0.74f, 0.78f, 0.82f));
            var frontMat = LoadOrCreateLitMaterial(TestMatDir + "/M_Front.mat", new Color(0.90f, 0.75f, 0.20f));

            var quarry = new GameObject("StoneQuarry");
            try
            {
                AddBox(quarry.transform, "Foundation", new Vector3(0f, 0.10f, 0f), new Vector3(1.92f, 0.20f, 1.92f), stoneMat, true);
                // Khối nhà xưởng đá phía sau (-Z)
                AddBox(quarry.transform, "RearBunker", new Vector3(0f, 0.85f, -0.35f), new Vector3(1.65f, 1.30f, 1.05f), darkStoneMat, true);
                AddBox(quarry.transform, "BunkerRoof", new Vector3(0f, 1.60f, -0.35f), new Vector3(1.78f, 0.22f, 1.18f), stoneMat, true);
                // Khung cần cẩu & máng trượt đá vươn về phía mặt khai thác (+Z)
                AddBox(quarry.transform, "CranePostL", new Vector3(-0.55f, 1.25f, 0.35f), new Vector3(0.14f, 2.10f, 0.14f), woodMat, false);
                AddBox(quarry.transform, "CranePostR", new Vector3(0.55f, 1.25f, 0.35f), new Vector3(0.14f, 2.10f, 0.14f), woodMat, false);
                AddBox(quarry.transform, "CraneCrossbar", new Vector3(0f, 2.24f, 0.35f), new Vector3(1.30f, 0.14f, 0.14f), woodMat, false);
                AddBox(quarry.transform, "CraneArmForward", new Vector3(0f, 2.12f, 0.68f), new Vector3(0.16f, 0.14f, 0.82f), woodMat, false);
                AddBox(quarry.transform, "CraneHook", new Vector3(0f, 1.62f, 0.96f), new Vector3(0.08f, 0.88f, 0.08f), ironMat, false);
                // Máng trượt đá và hộc chứa đá ở mặt trước (+Z)
                AddBox(quarry.transform, "MiningChute", new Vector3(0f, 0.55f, 0.55f), new Vector3(0.88f, 0.36f, 0.78f), ironMat, false);
                AddBox(quarry.transform, "FrontMarker", new Vector3(0f, 0.25f, 0.94f), new Vector3(0.95f, 0.38f, 0.16f), frontMat, false);

                var prefab = PrefabUtility.SaveAsPrefabAsset(quarry, BuildingPrefabDir + "/StoneQuarry.prefab");
                SaveBuildingData(BuildingDataDir + "/Building_StoneQuarry.asset", "Stone Quarry", BuildingCategory.Economy,
                    prefab, new Vector2Int(2, 2), 2.3f, 350f, 6f, 0, 3, ResourceType.Stone, 0f, true, 3f, 2);
            }
            finally
            {
                Object.DestroyImmediate(quarry);
            }
        }

        // Trang trại (Farm): ô ruộng canh tác 2x2 bao quanh bởi hàng rào gỗ 2 tầng, chia 2 luống rau xanh và lúa vàng như ảnh thiết kế
        static void GenerateFarmBuilding()
        {
            var soilDarkMat = LoadOrCreateLitMaterial(MaterialDir + "/M_Farm_Soil.mat", new Color(0.32f, 0.21f, 0.12f));
            var soilRidgeMat = LoadOrCreateLitMaterial(MaterialDir + "/M_Farm_SoilRidge.mat", new Color(0.40f, 0.27f, 0.16f));
            var fencePostMat = LoadOrCreateLitMaterial(MaterialDir + "/M_Farm_FencePost.mat", new Color(0.38f, 0.24f, 0.13f));
            var fenceRailMat = LoadOrCreateLitMaterial(TestMatDir + "/M_Wood.mat", new Color(0.55f, 0.36f, 0.18f));
            var leafDarkMat = LoadOrCreateLitMaterial(MaterialDir + "/M_Farm_LeafDark.mat", new Color(0.24f, 0.48f, 0.16f));
            var leafBrightMat = LoadOrCreateLitMaterial(MaterialDir + "/M_Farm_Leaf.mat", new Color(0.40f, 0.68f, 0.22f));
            var wheatStalkMat = LoadOrCreateLitMaterial(MaterialDir + "/M_Farm_WheatStalk.mat", new Color(0.78f, 0.62f, 0.22f));
            var wheatEarMat = LoadOrCreateLitMaterial(MaterialDir + "/M_Farm_Crop.mat", new Color(0.94f, 0.78f, 0.28f));

            // Cập nhật lại màu cho các material nếu đã tồn tại từ trước
            soilDarkMat.SetColor("_BaseColor", new Color(0.32f, 0.21f, 0.12f));
            soilRidgeMat.SetColor("_BaseColor", new Color(0.40f, 0.27f, 0.16f));
            fencePostMat.SetColor("_BaseColor", new Color(0.38f, 0.24f, 0.13f));
            leafDarkMat.SetColor("_BaseColor", new Color(0.24f, 0.48f, 0.16f));
            leafBrightMat.SetColor("_BaseColor", new Color(0.40f, 0.68f, 0.22f));
            wheatStalkMat.SetColor("_BaseColor", new Color(0.78f, 0.62f, 0.22f));
            wheatEarMat.SetColor("_BaseColor", new Color(0.94f, 0.78f, 0.28f));

            var farm = new GameObject("Farm");
            try
            {
                // Nền đất canh tác 2x2 và BoxCollider bao phủ ô ruộng để dễ click chọn
                AddBox(farm.transform, "SoilBed", new Vector3(0f, 0.05f, 0f), new Vector3(1.88f, 0.10f, 1.88f), soilDarkMat, false);
                var selectCollider = farm.AddComponent<BoxCollider>();
                selectCollider.center = new Vector3(0f, 0.28f, 0f);
                selectCollider.size = new Vector3(1.90f, 0.56f, 1.90f);

                // Thanh gỗ ngăn đôi 2 khu luống trồng ở giữa (X = 0)
                AddBox(farm.transform, "CenterDivider", new Vector3(0f, 0.13f, 0f), new Vector3(0.06f, 0.10f, 1.76f), fencePostMat, false);

                // 4 luống đất cày nổi chạy dọc theo chiều Z: 2 luống trái trồng rau củ xanh, 2 luống phải trồng lúa mì vàng
                float[] furrowX = { -0.60f, -0.24f, 0.24f, 0.60f };
                float[] cropZ = { -0.60f, -0.20f, 0.20f, 0.60f };

                for (int r = 0; r < furrowX.Length; r++)
                {
                    float fx = furrowX[r];
                    AddBox(farm.transform, "Furrow_" + r, new Vector3(fx, 0.12f, 0f), new Vector3(0.28f, 0.07f, 1.62f), soilRidgeMat, false);

                    for (int c = 0; c < cropZ.Length; c++)
                    {
                        float fz = cropZ[c];
                        if (r < 2)
                        {
                            // Nửa bên trái: bụi rau/bắp cải xanh nhiều lớp lá xòe tự nhiên
                            AddRotatedBox(farm.transform, $"LeafBaseA_{r}_{c}", new Vector3(fx, 0.20f, fz),
                                new Vector3(0.25f, 0.14f, 0.25f), new Vector3(0f, (r + c) * 25f, 0f), leafDarkMat);
                            AddRotatedBox(farm.transform, $"LeafBaseB_{r}_{c}", new Vector3(fx, 0.22f, fz),
                                new Vector3(0.22f, 0.15f, 0.22f), new Vector3(8f, 45f + (r + c) * 25f, -6f), leafDarkMat);
                            AddRotatedBox(farm.transform, $"LeafHeart_{r}_{c}", new Vector3(fx, 0.28f, fz),
                                new Vector3(0.16f, 0.14f, 0.16f), new Vector3(0f, 20f * c, 0f), leafBrightMat);
                        }
                        else
                        {
                            // Nửa bên phải: khóm lúa mì vàng cao vươn lên với nhiều nhánh nghiêng nhẹ
                            AddRotatedBox(farm.transform, $"WheatBase_{r}_{c}", new Vector3(fx, 0.22f, fz),
                                new Vector3(0.20f, 0.20f, 0.20f), new Vector3(0f, 18f * c, 0f), wheatStalkMat);
                            AddRotatedBox(farm.transform, $"WheatStalkL_{r}_{c}", new Vector3(fx - 0.04f, 0.34f, fz),
                                new Vector3(0.08f, 0.30f, 0.08f), new Vector3(6f, 12f, 10f), wheatEarMat);
                            AddRotatedBox(farm.transform, $"WheatStalkR_{r}_{c}", new Vector3(fx + 0.04f, 0.36f, fz),
                                new Vector3(0.08f, 0.32f, 0.08f), new Vector3(-5f, -15f, -10f), wheatEarMat);
                            AddRotatedBox(farm.transform, $"WheatTop_{r}_{c}", new Vector3(fx, 0.42f, fz),
                                new Vector3(0.12f, 0.16f, 0.12f), new Vector3(0f, 35f, 0f), wheatEarMat);
                        }
                    }
                }

                // Hệ thống cọc gỗ và hàng rào 2 thanh ngang bao quanh chu vi 2x2 (chừa lối cổng nhỏ ở mặt trước +Z)
                float edge = 0.91f;
                float[] postOffsets = { -edge, -0.32f, 0.32f, edge };
                for (int i = 0; i < postOffsets.Length; i++)
                {
                    float p = postOffsets[i];
                    // Cọc cạnh trái (-X) và cạnh phải (+X)
                    AddBox(farm.transform, "PostL_" + i, new Vector3(-edge, 0.22f, p), new Vector3(0.08f, 0.40f, 0.08f), fencePostMat, false);
                    AddBox(farm.transform, "PostR_" + i, new Vector3(edge, 0.22f, p), new Vector3(0.08f, 0.40f, 0.08f), fencePostMat, false);
                    // Cọc cạnh sau (-Z) và cạnh trước (+Z)
                    if (i > 0 && i < postOffsets.Length - 1)
                    {
                        AddBox(farm.transform, "PostB_" + i, new Vector3(p, 0.22f, -edge), new Vector3(0.08f, 0.40f, 0.08f), fencePostMat, false);
                        AddBox(farm.transform, "PostF_" + i, new Vector3(p, 0.22f, edge), new Vector3(0.08f, 0.40f, 0.08f), fencePostMat, false);
                    }
                }

                // Thanh rào ngang 2 tầng (trên & dưới) cho cạnh trái, phải, sau
                float[] railHeights = { 0.17f, 0.32f };
                for (int h = 0; h < railHeights.Length; h++)
                {
                    float ry = railHeights[h];
                    AddBox(farm.transform, "RailL_" + h, new Vector3(-edge, ry, 0f), new Vector3(0.045f, 0.045f, 1.82f), fenceRailMat, false);
                    AddBox(farm.transform, "RailR_" + h, new Vector3(edge, ry, 0f), new Vector3(0.045f, 0.045f, 1.82f), fenceRailMat, false);
                    AddBox(farm.transform, "RailB_" + h, new Vector3(0f, ry, -edge), new Vector3(1.82f, 0.045f, 0.045f), fenceRailMat, false);
                    // Cạnh trước (+Z) chừa cổng vào rộng 0.64m ở giữa
                    AddBox(farm.transform, "RailFL_" + h, new Vector3(-0.615f, ry, edge), new Vector3(0.59f, 0.045f, 0.045f), fenceRailMat, false);
                    AddBox(farm.transform, "RailFR_" + h, new Vector3(0.615f, ry, edge), new Vector3(0.59f, 0.045f, 0.045f), fenceRailMat, false);
                }

                var prefab = PrefabUtility.SaveAsPrefabAsset(farm, BuildingPrefabDir + "/Farm.prefab");
                SaveBuildingData(BuildingDataDir + "/Building_Farm.asset", "Farm", BuildingCategory.Economy,
                    prefab, new Vector2Int(2, 2), 0.7f, 240f, 5f, 0, 3, ResourceType.Food, 0f, false, 2.5f, 3);
            }
            finally
            {
                Object.DestroyImmediate(farm);
            }
        }

        static void AddRotatedBox(Transform parent, string partName, Vector3 localPos, Vector3 localScale, Vector3 localEuler, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = partName;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            go.transform.localScale = localScale;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
        }

        // Xưởng khai thác sắt (Iron Mine) và Mỏ quặng sắt (IronDeposit): đặt sát mỏ sắt và hướng mặt trước (+Z) vào bề mặt quặng
        static void GenerateIronMineBuilding()
        {
            const string resourcePrefabDir = "Assets/Prefabs/TestObjects/Resources";
            EnsureFolder(resourcePrefabDir);

            var darkRockMat = LoadOrCreateLitMaterial(MaterialDir + "/M_Iron_Basalt.mat", new Color(0.24f, 0.26f, 0.30f));
            var oreVeinMat = LoadOrCreateLitMaterial(MaterialDir + "/M_Iron_OreVein.mat", new Color(0.62f, 0.72f, 0.84f));
            var rustVeinMat = LoadOrCreateLitMaterial(MaterialDir + "/M_Iron_Rust.mat", new Color(0.68f, 0.34f, 0.18f));
            var stoneMat = LoadOrCreateLitMaterial(TestMatDir + "/M_Stone.mat", new Color(0.52f, 0.52f, 0.50f));
            var ironMat = LoadOrCreateLitMaterial(MaterialDir + "/M_Steel.mat", new Color(0.74f, 0.78f, 0.82f));
            var forgeGlowMat = LoadOrCreateLitMaterial(MaterialDir + "/M_Iron_ForgeGlow.mat", new Color(0.95f, 0.45f, 0.12f));
            var woodMat = LoadOrCreateLitMaterial(TestMatDir + "/M_Wood.mat", new Color(0.55f, 0.36f, 0.18f));
            var frontMat = LoadOrCreateLitMaterial(TestMatDir + "/M_Front.mat", new Color(0.90f, 0.75f, 0.20f));

            // 1. Tạo Prefab mỏ quặng sắt tự nhiên (IronDeposit) kích thước 2x2
            var deposit = new GameObject("IronDeposit");
            try
            {
                var occ = deposit.AddComponent<GridOccupant>();
                occ.footprint = new Vector2Int(2, 2);
                occ.isBuilding = false;
                var node = deposit.AddComponent<ResourceNode>();
                node.resourceType = ResourceType.Iron;

                AddBox(deposit.transform, "MainRock", new Vector3(0f, 0.55f, 0f), new Vector3(1.55f, 1.10f, 1.50f), darkRockMat, true);
                AddBox(deposit.transform, "SideRockL", new Vector3(-0.48f, 0.38f, 0.35f), new Vector3(0.85f, 0.76f, 0.85f), darkRockMat, false);
                AddBox(deposit.transform, "SideRockR", new Vector3(0.45f, 0.42f, -0.32f), new Vector3(0.82f, 0.84f, 0.82f), darkRockMat, false);
                // Các mạch quặng sắt ánh kim và gỉ đỏ nổi trên bề mặt đá
                AddBox(deposit.transform, "IronVeinTop", new Vector3(-0.15f, 0.98f, 0.12f), new Vector3(0.58f, 0.32f, 0.48f), oreVeinMat, false);
                AddBox(deposit.transform, "IronVeinFront", new Vector3(0.28f, 0.65f, 0.64f), new Vector3(0.46f, 0.42f, 0.34f), oreVeinMat, false);
                AddBox(deposit.transform, "IronVeinBack", new Vector3(-0.32f, 0.62f, -0.62f), new Vector3(0.52f, 0.38f, 0.32f), rustVeinMat, false);
                AddBox(deposit.transform, "IronCrystal", new Vector3(0.22f, 1.05f, -0.18f), new Vector3(0.36f, 0.36f, 0.36f), oreVeinMat, false);

                PrefabUtility.SaveAsPrefabAsset(deposit, resourcePrefabDir + "/IronDeposit.prefab");
            }
            finally
            {
                Object.DestroyImmediate(deposit);
            }

            // 2. Tạo Prefab công trình Xưởng khai thác sắt (IronMine) kích thước 2x2
            var mine = new GameObject("IronMine");
            try
            {
                AddBox(mine.transform, "Foundation", new Vector3(0f, 0.10f, 0f), new Vector3(1.92f, 0.20f, 1.92f), stoneMat, true);
                // Lò luyện sắt phía sau (-Z)
                AddBox(mine.transform, "SmelterBody", new Vector3(-0.20f, 0.88f, -0.34f), new Vector3(1.36f, 1.36f, 1.06f), darkRockMat, true);
                AddBox(mine.transform, "SmelterRoof", new Vector3(-0.20f, 1.64f, -0.34f), new Vector3(1.50f, 0.20f, 1.18f), ironMat, true);
                // Ống khói lò cao và cửa lò rực lửa
                AddBox(mine.transform, "SmelterStack", new Vector3(-0.48f, 2.15f, -0.45f), new Vector3(0.44f, 1.10f, 0.44f), darkRockMat, false);
                AddBox(mine.transform, "StackRim", new Vector3(-0.48f, 2.72f, -0.45f), new Vector3(0.54f, 0.12f, 0.54f), ironMat, false);
                AddBox(mine.transform, "ForgeCore", new Vector3(-0.20f, 0.52f, 0.20f), new Vector3(0.56f, 0.48f, 0.08f), forgeGlowMat, false);
                // Khung đỡ và băng chuyền gầu tải quặng sắt hướng về mặt khai thác (+Z)
                AddBox(mine.transform, "SupportBeamL", new Vector3(-0.56f, 1.05f, 0.42f), new Vector3(0.14f, 1.70f, 0.14f), ironMat, false);
                AddBox(mine.transform, "SupportBeamR", new Vector3(0.56f, 1.05f, 0.42f), new Vector3(0.14f, 1.70f, 0.14f), ironMat, false);
                AddBox(mine.transform, "TopCrossbar", new Vector3(0f, 1.86f, 0.42f), new Vector3(1.32f, 0.14f, 0.16f), ironMat, false);
                AddBox(mine.transform, "OreHopper", new Vector3(0f, 0.56f, 0.56f), new Vector3(0.92f, 0.42f, 0.76f), darkRockMat, false);
                AddBox(mine.transform, "IngotStack", new Vector3(0.62f, 0.32f, -0.25f), new Vector3(0.36f, 0.24f, 0.52f), oreVeinMat, false);
                AddBox(mine.transform, "FrontMarker", new Vector3(0f, 0.25f, 0.94f), new Vector3(0.95f, 0.36f, 0.16f), frontMat, false);

                var prefab = PrefabUtility.SaveAsPrefabAsset(mine, BuildingPrefabDir + "/IronMine.prefab");
                SaveBuildingData(BuildingDataDir + "/Building_IronMine.asset", "Iron Mine", BuildingCategory.Economy,
                    prefab, new Vector2Int(2, 2), 2.5f, 380f, 6f, 0, 3, ResourceType.Iron, 0f, true, 3f, 2);
            }
            finally
            {
                Object.DestroyImmediate(mine);
            }
        }

        // Chuyển các công trình thử nghiệm kích thước (Building 1x1..4x4, 2x3, 1x4) sang danh mục Test
        static void ReassignTestBuildings()
        {
            string[] testAssets =
            {
                "Building_1x1.asset",
                "Building_2x2.asset",
                "Building_3x3.asset",
                "Building_4x4.asset",
                "Building_2x3.asset",
                "Building_1x4.asset"
            };

            for (int i = 0; i < testAssets.Length; i++)
            {
                var data = AssetDatabase.LoadAssetAtPath<BuildingData>(BuildingDataDir + "/" + testAssets[i]);
                if (data != null && data.category != BuildingCategory.Test)
                {
                    data.category = BuildingCategory.Test;
                    EditorUtility.SetDirty(data);
                }
            }
        }

        static void AddBox(Transform parent, string partName, Vector3 localPos, Vector3 localScale, Material mat, bool withCollider)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = partName;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            if (!withCollider)
            {
                var col = go.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col);
            }
        }

        static void SaveBuildingData(string path, string displayName, BuildingCategory category,
            GameObject prefab, Vector2Int footprint, float height, float maxHealth,
            float buildTime, int populationProvided, int maxWorkers,
            ResourceType harvestResource, float harvestRadius, bool requireAdjacentResource,
            float harvestInterval, int harvestAmount)
        {
            var data = AssetDatabase.LoadAssetAtPath<BuildingData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<BuildingData>();
                AssetDatabase.CreateAsset(data, path);
            }
            data.displayName = displayName;
            data.category = category;
            data.prefab = prefab;
            data.footprint = footprint;
            data.height = height;
            data.maxHealth = maxHealth;
            data.buildTime = buildTime;
            data.populationProvided = populationProvided;
            data.maxWorkers = maxWorkers;
            data.harvestResource = harvestResource;
            data.harvestRadius = harvestRadius;
            data.requireAdjacentResource = requireAdjacentResource;
            data.harvestInterval = harvestInterval;
            data.harvestAmount = harvestAmount;
            EditorUtility.SetDirty(data);
        }

        // ---------- Tạo Rig, Mesh, AnimationClip cho nhân vật ----------

        static Transform[] BuildRig(GameObject root)
        {
            var bones = new Transform[Bones.Length];
            for (int b = 0; b < Bones.Length; b++)
            {
                var bone = new GameObject(Bones[b].Name).transform;
                bone.SetParent(Bones[b].Parent < 0 ? root.transform : bones[Bones[b].Parent], false);
                bone.localPosition = Bones[b].Position;
                bone.localRotation = Quaternion.identity;
                bones[b] = bone;
            }
            return bones;
        }

        static Matrix4x4[] BuildBindposes(GameObject root, Transform[] bones)
        {
            var bindposes = new Matrix4x4[bones.Length];
            for (int b = 0; b < bones.Length; b++)
                bindposes[b] = bones[b].worldToLocalMatrix * root.transform.localToWorldMatrix;
            return bindposes;
        }

        static void ResetPose(Transform[] bones)
        {
            for (int b = 0; b < bones.Length; b++)
            {
                bones[b].localPosition = Bones[b].Position;
                bones[b].localRotation = Quaternion.identity;
            }
        }

        static Mesh BuildCharacterMesh(string meshName, Transform[] bones, Matrix4x4[] bindposes, PartDef[] parts)
        {
            var cubeObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var cube = cubeObject.GetComponent<MeshFilter>().sharedMesh;
            var cubeVertices = cube.vertices;
            var cubeNormals = cube.normals;
            var cubeTriangles = cube.triangles;
            Object.DestroyImmediate(cubeObject);

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var colors = new List<Color>();
            var boneUv = new List<Vector2>();
            var weights = new List<BoneWeight>();
            var triangles = new List<int>();

            foreach (var part in parts)
            {
                var boneOrigin = bones[part.Bone].position;
                int start = vertices.Count;
                for (int i = 0; i < cubeVertices.Length; i++)
                {
                    vertices.Add(boneOrigin + part.Center + Vector3.Scale(cubeVertices[i], part.Size));
                    normals.Add(cubeNormals[i]);
                    colors.Add(part.Color);
                    boneUv.Add(new Vector2(part.Bone, 0f));
                    weights.Add(new BoneWeight { boneIndex0 = part.Bone, weight0 = 1f });
                }
                foreach (int index in cubeTriangles) triangles.Add(start + index);
            }

            var mesh = new Mesh { name = meshName };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetUVs(1, boneUv);
            mesh.SetTriangles(triangles, 0);
            mesh.boneWeights = weights.ToArray();
            mesh.bindposes = bindposes;
            mesh.bounds = new Bounds(new Vector3(0f, 1.2f, 0f), new Vector3(3f, 2.4f, 3f));
            return mesh;
        }

        static GpuAnimationBaker.ClipSource MakeClip(Transform[] bones, GameObject root, string prefix, string clipName,
            float length, bool loop, PoseFunction pose)
        {
            var clip = new AnimationClip { name = prefix + "_" + clipName, frameRate = BakeFps };
            int keyCount = Mathf.RoundToInt(length * BakeFps) + 1;
            var euler = new Vector3[Bones.Length];

            var rotationKeys = new Keyframe[Bones.Length, 4][];
            var positionKeys = new Keyframe[3][];
            for (int b = 0; b < Bones.Length; b++)
                for (int c = 0; c < 4; c++) rotationKeys[b, c] = new Keyframe[keyCount];
            for (int c = 0; c < 3; c++) positionKeys[c] = new Keyframe[keyCount];

            var previous = new Quaternion[Bones.Length];
            for (int k = 0; k < keyCount; k++)
            {
                float t = k / BakeFps;
                System.Array.Clear(euler, 0, euler.Length);
                var hips = Bones[Hips].Position;
                pose(t, euler, ref hips);

                for (int b = 0; b < Bones.Length; b++)
                {
                    var q = Quaternion.Euler(euler[b]);
                    if (k > 0 && Quaternion.Dot(q, previous[b]) < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                    previous[b] = q;
                    rotationKeys[b, 0][k] = new Keyframe(t, q.x);
                    rotationKeys[b, 1][k] = new Keyframe(t, q.y);
                    rotationKeys[b, 2][k] = new Keyframe(t, q.z);
                    rotationKeys[b, 3][k] = new Keyframe(t, q.w);
                }
                positionKeys[0][k] = new Keyframe(t, hips.x);
                positionKeys[1][k] = new Keyframe(t, hips.y);
                positionKeys[2][k] = new Keyframe(t, hips.z);
            }

            string[] rotationProps = { "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w" };
            string[] positionProps = { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z" };
            for (int b = 0; b < Bones.Length; b++)
            {
                string path = AnimationUtility.CalculateTransformPath(bones[b], root.transform);
                for (int c = 0; c < 4; c++)
                    AnimationUtility.SetEditorCurve(clip,
                        EditorCurveBinding.FloatCurve(path, typeof(Transform), rotationProps[c]),
                        new AnimationCurve(rotationKeys[b, c]));
            }
            string hipsPath = AnimationUtility.CalculateTransformPath(bones[Hips], root.transform);
            for (int c = 0; c < 3; c++)
                AnimationUtility.SetEditorCurve(clip,
                    EditorCurveBinding.FloatCurve(hipsPath, typeof(Transform), positionProps[c]),
                    new AnimationCurve(positionKeys[c]));

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            clip = SaveAsset(clip, AnimDir + "/" + prefix + "/" + prefix + "_" + clipName + ".anim");
            return new GpuAnimationBaker.ClipSource { Name = clipName, Clip = clip, Loop = loop };
        }

        // ---------- Tư thế animation của Villager, Builder, Lumberjack, Miner, Farmer ----------

        static void VillagerIdlePose(float t, Vector3[] e, ref Vector3 hips)
        {
            float p = t / 2f * Mathf.PI * 2f;
            float s = Mathf.Sin(p);
            e[Spine] = new Vector3(2f + 1.5f * s, 0f, 0f);
            e[Head] = new Vector3(-1f + 1.5f * Mathf.Sin(p + 0.8f), 4f * s, 0f);
            e[ArmL] = new Vector3(-3f + 2f * s, 0f, -5f);
            e[ForearmL] = new Vector3(-10f, 0f, 0f);
            e[ArmR] = new Vector3(-3f - 2f * s, 0f, 5f);
            e[ForearmR] = new Vector3(-10f, 0f, 0f);
            e[ThighL] = new Vector3(0f, 0f, -1.5f);
            e[ThighR] = new Vector3(0f, 0f, 1.5f);
            hips.y = 0.95f - 0.008f * (1f - Mathf.Cos(p));
        }

        static void VillagerWalkPose(float t, Vector3[] e, ref Vector3 hips)
        {
            float p = t / 0.8f * Mathf.PI * 2f;
            float s = Mathf.Sin(p);
            float c = Mathf.Cos(p);
            e[Hips] = new Vector3(0f, -4f * s, 0f);
            e[Spine] = new Vector3(5f, 5f * s, 0f);
            e[Head] = new Vector3(-4f, -4f * s, 0f);
            e[ThighL] = new Vector3(-26f * s, 0f, 0f);
            e[ThighR] = new Vector3(26f * s, 0f, 0f);
            e[ShinL] = new Vector3(5f + 36f * Mathf.Max(0f, c), 0f, 0f);
            e[ShinR] = new Vector3(5f + 36f * Mathf.Max(0f, -c), 0f, 0f);
            e[ArmL] = new Vector3(22f * s, 0f, -5f);
            e[ForearmL] = new Vector3(-16f, 0f, 0f);
            e[ArmR] = new Vector3(-22f * s, 0f, 5f);
            e[ForearmR] = new Vector3(-16f, 0f, 0f);
            hips.y = 0.92f + 0.035f * Mathf.Abs(c);
        }

        static void BuilderIdlePose(float t, Vector3[] e, ref Vector3 hips)
        {
            float p = t / 2f * Mathf.PI * 2f;
            float s = Mathf.Sin(p);
            e[Spine] = new Vector3(3f + 2f * s, 0f, 0f);
            e[Head] = new Vector3(-2f + 1.5f * Mathf.Sin(p + 0.6f), 3f * s, 0f);
            e[ArmL] = new Vector3(-4f + 2f * s, 0f, -6f);
            e[ForearmL] = new Vector3(-15f, 0f, 0f);
            e[ArmR] = new Vector3(-12f + 2f * s, 0f, 6f);
            e[ForearmR] = new Vector3(-35f, 0f, 0f);
            e[ThighL] = new Vector3(0f, 0f, -2f);
            e[ThighR] = new Vector3(0f, 0f, 2f);
            hips.y = 0.95f - 0.01f * (1f - Mathf.Cos(p));
        }

        static void BuilderWalkPose(float t, Vector3[] e, ref Vector3 hips)
        {
            float p = t / 0.8f * Mathf.PI * 2f;
            float s = Mathf.Sin(p);
            float c = Mathf.Cos(p);
            e[Hips] = new Vector3(0f, -5f * s, 0f);
            e[Spine] = new Vector3(6f, 5f * s, 0f);
            e[Head] = new Vector3(-5f, -5f * s, 0f);
            e[ThighL] = new Vector3(-27f * s, 0f, 0f);
            e[ThighR] = new Vector3(27f * s, 0f, 0f);
            e[ShinL] = new Vector3(5f + 38f * Mathf.Max(0f, c), 0f, 0f);
            e[ShinR] = new Vector3(5f + 38f * Mathf.Max(0f, -c), 0f, 0f);
            e[ArmL] = new Vector3(24f * s, 0f, -6f);
            e[ForearmL] = new Vector3(-18f, 0f, 0f);
            e[ArmR] = new Vector3(-18f * s - 12f, 0f, 6f);
            e[ForearmR] = new Vector3(-40f, 0f, 0f);
            hips.y = 0.92f + 0.038f * Mathf.Abs(c);
        }

        // Động tác gõ búa xây dựng (chu kỳ 0.75s): vung búa lên ở 0.28s, bổ mạnh xuống ở 0.45s.
        static void BuilderHammerPose(float t, Vector3[] e, ref Vector3 hips)
        {
            e[ArmR] = new Vector3(Keys(t, 0f, -35f, 0.28f, -135f, 0.45f, -25f, 0.75f, -35f), 0f, 6f);
            e[ForearmR] = new Vector3(Keys(t, 0f, -45f, 0.28f, -75f, 0.45f, -10f, 0.75f, -45f), 0f, 0f);
            e[Spine] = new Vector3(Keys(t, 0f, 12f, 0.28f, -4f, 0.45f, 24f, 0.75f, 12f),
                Keys(t, 0f, 0f, 0.28f, -15f, 0.45f, 12f, 0.75f, 0f), 0f);
            e[Hips] = new Vector3(0f, Keys(t, 0f, 0f, 0.28f, -6f, 0.45f, 6f, 0.75f, 0f), 0f);
            e[Head] = new Vector3(Keys(t, 0f, 5f, 0.28f, -8f, 0.45f, 14f, 0.75f, 5f), 0f, 0f);
            e[ArmL] = new Vector3(-30f, 0f, -8f);
            e[ForearmL] = new Vector3(-55f, 0f, 0f);
            e[ThighL] = new Vector3(-15f, 0f, -2f);
            e[ShinL] = new Vector3(15f, 0f, 0f);
            e[ThighR] = new Vector3(12f, 0f, 2f);
            e[ShinR] = new Vector3(10f, 0f, 0f);
            hips.y = Keys(t, 0f, 0.91f, 0.28f, 0.94f, 0.45f, 0.87f, 0.75f, 0.91f);
        }

        // Động tác bổ rìu chặt cây ngang hông (chu kỳ 0.85s)
        static void LumberjackChopPose(float t, Vector3[] e, ref Vector3 hips)
        {
            e[Spine] = new Vector3(
                Keys(t, 0f, 8f, 0.32f, -6f, 0.50f, 22f, 0.85f, 8f),
                Keys(t, 0f, -10f, 0.32f, -38f, 0.50f, 28f, 0.85f, -10f), 0f);
            e[Hips] = new Vector3(0f, Keys(t, 0f, -4f, 0.32f, -16f, 0.50f, 14f, 0.85f, -4f), 0f);
            e[Head] = new Vector3(4f, Keys(t, 0f, 6f, 0.32f, 22f, 0.50f, -18f, 0.85f, 6f), 0f);
            e[ArmR] = new Vector3(
                Keys(t, 0f, -55f, 0.32f, -115f, 0.50f, -40f, 0.85f, -55f),
                Keys(t, 0f, -10f, 0.32f, -25f, 0.50f, 20f, 0.85f, -10f), 12f);
            e[ForearmR] = new Vector3(Keys(t, 0f, -45f, 0.32f, -65f, 0.50f, -15f, 0.85f, -45f), 0f, 0f);
            e[ArmL] = new Vector3(
                Keys(t, 0f, -45f, 0.32f, -85f, 0.50f, -35f, 0.85f, -45f), 20f, -10f);
            e[ForearmL] = new Vector3(-40f, 0f, 0f);
            e[ThighL] = new Vector3(-18f, 0f, -4f);
            e[ShinL] = new Vector3(18f, 0f, 0f);
            e[ThighR] = new Vector3(14f, 0f, 4f);
            e[ShinR] = new Vector3(12f, 0f, 0f);
            hips.y = Keys(t, 0f, 0.90f, 0.32f, 0.94f, 0.50f, 0.86f, 0.85f, 0.90f);
        }

        // Động tác bổ cuốc chim khai thác đá từ trên cao xuống (chu kỳ 0.80s)
        static void MinerPickPose(float t, Vector3[] e, ref Vector3 hips)
        {
            e[Spine] = new Vector3(Keys(t, 0f, 14f, 0.34f, -14f, 0.52f, 34f, 0.80f, 14f), 0f, 0f);
            e[Head] = new Vector3(Keys(t, 0f, 6f, 0.34f, -12f, 0.52f, 18f, 0.80f, 6f), 0f, 0f);
            e[ArmR] = new Vector3(Keys(t, 0f, -45f, 0.34f, -155f, 0.52f, -20f, 0.80f, -45f), 0f, 6f);
            e[ForearmR] = new Vector3(Keys(t, 0f, -50f, 0.34f, -70f, 0.52f, -12f, 0.80f, -50f), 0f, 0f);
            e[ArmL] = new Vector3(Keys(t, 0f, -40f, 0.34f, -135f, 0.52f, -25f, 0.80f, -40f), 15f, -6f);
            e[ForearmL] = new Vector3(Keys(t, 0f, -45f, 0.34f, -60f, 0.52f, -15f, 0.80f, -45f), 0f, 0f);
            e[ThighL] = new Vector3(-16f, 0f, -3f);
            e[ShinL] = new Vector3(16f, 0f, 0f);
            e[ThighR] = new Vector3(14f, 0f, 3f);
            e[ShinR] = new Vector3(14f, 0f, 0f);
            hips.y = Keys(t, 0f, 0.90f, 0.34f, 0.96f, 0.52f, 0.84f, 0.80f, 0.90f);
        }

        // Động tác cuốc đất làm ruộng của nông dân (chu kỳ 0.90s)
        static void FarmerHoePose(float t, Vector3[] e, ref Vector3 hips)
        {
            e[Spine] = new Vector3(Keys(t, 0f, 18f, 0.35f, 4f, 0.58f, 32f, 0.90f, 18f),
                Keys(t, 0f, -6f, 0.35f, -14f, 0.58f, 10f, 0.90f, -6f), 0f);
            e[Head] = new Vector3(Keys(t, 0f, 10f, 0.35f, 0f, 0.58f, 18f, 0.90f, 10f), 0f, 0f);
            e[ArmR] = new Vector3(Keys(t, 0f, -50f, 0.35f, -110f, 0.58f, -28f, 0.90f, -50f), 0f, 8f);
            e[ForearmR] = new Vector3(Keys(t, 0f, -42f, 0.35f, -62f, 0.58f, -18f, 0.90f, -42f), 0f, 0f);
            e[ArmL] = new Vector3(Keys(t, 0f, -42f, 0.35f, -92f, 0.58f, -30f, 0.90f, -42f), 14f, -8f);
            e[ForearmL] = new Vector3(Keys(t, 0f, -38f, 0.35f, -52f, 0.58f, -20f, 0.90f, -38f), 0f, 0f);
            e[ThighL] = new Vector3(-18f, 0f, -3f);
            e[ShinL] = new Vector3(18f, 0f, 0f);
            e[ThighR] = new Vector3(14f, 0f, 3f);
            e[ShinR] = new Vector3(14f, 0f, 0f);
            hips.y = Keys(t, 0f, 0.89f, 0.35f, 0.94f, 0.58f, 0.84f, 0.90f, 0.89f);
        }

        static void DeathPose(float t, Vector3[] e, ref Vector3 hips)
        {
            e[Hips] = new Vector3(Keys(t, 0f, 0f, 0.2f, 6f, 1f, -88f), 0f, 0f);
            e[Spine] = new Vector3(Keys(t, 0f, 0f, 0.2f, -25f, 1f, -5f), 0f, 0f);
            e[Head] = new Vector3(Keys(t, 0f, 0f, 0.25f, -30f, 1f, -10f), 0f, 0f);
            e[ArmL] = new Vector3(Keys(t, 0f, 0f, 0.3f, -50f, 1f, -20f), 0f, Keys(t, 0f, -6f, 0.4f, -40f, 1f, -80f));
            e[ArmR] = new Vector3(Keys(t, 0f, 0f, 0.3f, -50f, 1f, -20f), 0f, Keys(t, 0f, 6f, 0.4f, 40f, 1f, 80f));
            e[ForearmL] = new Vector3(-10f, 0f, 0f);
            e[ForearmR] = new Vector3(-10f, 0f, 0f);
            e[ThighL] = new Vector3(Keys(t, 0f, 0f, 1f, -15f), 0f, 0f);
            e[ThighR] = new Vector3(Keys(t, 0f, 0f, 1f, -8f), 0f, 0f);
            e[ShinL] = new Vector3(Keys(t, 0f, 0f, 1f, 25f), 0f, 0f);
            e[ShinR] = new Vector3(Keys(t, 0f, 0f, 1f, 12f), 0f, 0f);
            hips.y = Keys(t, 0f, 0.95f, 0.2f, 0.97f, 1f, 0.16f);
            hips.z = Keys(t, 0f, 0f, 1f, -0.15f);
        }

        static float Keys(float t, params float[] pairs)
        {
            if (t <= pairs[0]) return pairs[1];
            for (int i = 2; i < pairs.Length; i += 2)
            {
                if (t > pairs[i]) continue;
                float a = pairs[i - 2];
                float u = Mathf.SmoothStep(0f, 1f, (t - a) / (pairs[i] - a));
                return Mathf.Lerp(pairs[i - 1], pairs[i + 1], u);
            }
            return pairs[pairs.Length - 1];
        }

        // ---------- Lưu Prefab & Material ----------

        static void SaveRigPrefab(GameObject root, Transform[] bones, Mesh mesh, string prefix, Color previewColor)
        {
            string previewPath = MaterialDir + "/M_" + prefix + "_Preview.mat";
            var preview = LoadOrCreateLitMaterial(previewPath, previewColor);

            var meshObject = new GameObject("Mesh");
            meshObject.transform.SetParent(root.transform, false);
            var skinned = meshObject.AddComponent<SkinnedMeshRenderer>();
            skinned.sharedMesh = mesh;
            skinned.bones = bones;
            skinned.rootBone = bones[Hips];
            skinned.sharedMaterial = preview;
            skinned.localBounds = mesh.bounds;
            PrefabUtility.SaveAsPrefabAsset(root, ModelDir + "/" + prefix + "/" + prefix + "_Rig.prefab");
        }

        static Material CreateGpuSkinnedMaterial(string matName, Texture2D boneTexture)
        {
            string path = MaterialDir + "/" + matName + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("SurvivalRts/GpuSkinnedLit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_BoneTex", boneTexture);
            EditorUtility.SetDirty(material);
            return material;
        }

        static void CreateUnitPrefab(string unitName, Mesh mesh, Material material, GpuAnimationData animData)
        {
            var go = new GameObject(unitName);
            try
            {
                var filter = go.AddComponent<MeshFilter>();
                filter.sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;

                var animator = go.AddComponent<GpuMeshAnimator>();
                animator.Setup(animData, "Idle");

                if (unitName == "Villager") go.AddComponent<VillagerUnit>();
                else if (unitName == "Builder") go.AddComponent<BuilderUnit>();
                else if (unitName == "Lumberjack" || unitName == "Miner" || unitName == "Farmer") go.AddComponent<HarvesterUnit>();

                var collider = go.AddComponent<CapsuleCollider>();
                collider.center = new Vector3(0f, 0.9f, 0f);
                collider.radius = 0.35f;
                collider.height = 1.8f;

                PrefabUtility.SaveAsPrefabAsset(go, UnitPrefabDir + "/" + unitName + ".prefab");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        static void WireActiveScene()
        {
            var villagerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(UnitPrefabDir + "/Villager.prefab");
            var builderPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(UnitPrefabDir + "/Builder.prefab");
            var lumberjackPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(UnitPrefabDir + "/Lumberjack.prefab");
            var minerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(UnitPrefabDir + "/Miner.prefab");
            var farmerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(UnitPrefabDir + "/Farmer.prefab");

            var popManager = Object.FindAnyObjectByType<PopulationManager>();
            if (popManager == null)
            {
                var go = new GameObject("PopulationManager");
                popManager = go.AddComponent<PopulationManager>();
            }
            popManager.ConfigurePrefabs(villagerPrefab, builderPrefab, lumberjackPrefab, minerPrefab, farmerPrefab);
            EditorUtility.SetDirty(popManager);

            // Đảm bảo có ít nhất 1 mỏ quặng sắt (IronDeposit) trong khu vực TestArea của scene
            EnsureIronDepositInScene();

            // Gắn ResourceNode cho các tài nguyên Cây, Đá, Sắt và Bụi quả trong scene
            ResourceNode.EnsureSceneNodesRegistered();
            var allNodes = Object.FindObjectsByType<ResourceNode>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < allNodes.Length; i++)
            {
                EditorUtility.SetDirty(allNodes[i]);
            }

            // Đảm bảo BuildMenu có gắn MinimapController
            var buildMenu = Object.FindAnyObjectByType<BuildMenuController>();
            if (buildMenu != null && buildMenu.GetComponent<MinimapController>() == null)
            {
                buildMenu.gameObject.AddComponent<MinimapController>();
                EditorUtility.SetDirty(buildMenu.gameObject);
            }

            var placer = Object.FindAnyObjectByType<BuildingPlacer>();
            if (placer != null)
            {
                var so = new SerializedObject(placer);

                var stage1 = AssetDatabase.LoadAssetAtPath<GameObject>(BuildingPrefabDir + "/Construction_Stage1.prefab");
                var stage2 = AssetDatabase.LoadAssetAtPath<GameObject>(BuildingPrefabDir + "/Construction_Stage2.prefab");
                var stage3 = AssetDatabase.LoadAssetAtPath<GameObject>(BuildingPrefabDir + "/Construction_Stage3.prefab");
                var stagesProp = so.FindProperty("constructionStages");
                if (stagesProp != null)
                {
                    stagesProp.arraySize = 3;
                    stagesProp.GetArrayElementAtIndex(0).objectReferenceValue = stage1;
                    stagesProp.GetArrayElementAtIndex(1).objectReferenceValue = stage2;
                    stagesProp.GetArrayElementAtIndex(2).objectReferenceValue = stage3;
                }

                var houseData = AssetDatabase.LoadAssetAtPath<BuildingData>(BuildingDataDir + "/Building_House.asset");
                var hutData = AssetDatabase.LoadAssetAtPath<BuildingData>(BuildingDataDir + "/Building_BuilderHut.asset");
                var lumberData = AssetDatabase.LoadAssetAtPath<BuildingData>(BuildingDataDir + "/Building_LumberCamp.asset");
                var quarryData = AssetDatabase.LoadAssetAtPath<BuildingData>(BuildingDataDir + "/Building_StoneQuarry.asset");
                var ironMineData = AssetDatabase.LoadAssetAtPath<BuildingData>(BuildingDataDir + "/Building_IronMine.asset");
                var farmData = AssetDatabase.LoadAssetAtPath<BuildingData>(BuildingDataDir + "/Building_Farm.asset");
                var buildingsProp = so.FindProperty("buildings");
                if (buildingsProp != null)
                {
                    EnsureBuildingInList(buildingsProp, houseData, 0);
                    EnsureBuildingInList(buildingsProp, hutData, 1);
                    EnsureBuildingInList(buildingsProp, lumberData, 2);
                    EnsureBuildingInList(buildingsProp, quarryData, 3);
                    EnsureBuildingInList(buildingsProp, ironMineData, 4);
                    EnsureBuildingInList(buildingsProp, farmData, 5);
                }

                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(placer);
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        }

        static void EnsureIronDepositInScene()
        {
            var existingNodes = Object.FindObjectsByType<ResourceNode>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < existingNodes.Length; i++)
            {
                if (existingNodes[i] != null && existingNodes[i].resourceType == ResourceType.Iron)
                    return;
            }

            var depositPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/TestObjects/Resources/IronDeposit.prefab");
            if (depositPrefab == null) return;

            var testArea = GameObject.Find("TestArea");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(depositPrefab, testArea != null ? testArea.transform : null);
            instance.name = "IronDeposit";
            instance.transform.position = new Vector3(-10f, 0f, -1f);
            instance.transform.rotation = Quaternion.identity;
            EditorUtility.SetDirty(instance);
        }

        static void EnsureBuildingInList(SerializedProperty arrayProp, BuildingData target, int insertIndex)
        {
            if (target == null) return;
            for (int i = 0; i < arrayProp.arraySize; i++)
            {
                if (arrayProp.GetArrayElementAtIndex(i).objectReferenceValue == target) return;
            }
            int idx = Mathf.Clamp(insertIndex, 0, arrayProp.arraySize);
            arrayProp.InsertArrayElementAtIndex(idx);
            arrayProp.GetArrayElementAtIndex(idx).objectReferenceValue = target;
        }

        static Material LoadOrCreateLitMaterial(string path, Color color)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static T SaveAsset<T>(T asset, string path) where T : Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(asset, existing);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        // ---------- Sinh Icon 2D cho Dân số, Gỗ, Đá, Sắt, Lúa và Tiền tệ ----------

        static void GenerateResourceIcons()
        {
            const int size = 64;

            var woodTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            DrawWoodIcon(woodTex, size);
            SaveIconPng(woodTex, TextureDir + "/Icon_Wood.png");
            Object.DestroyImmediate(woodTex);

            var stoneTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            DrawStoneIcon(stoneTex, size);
            SaveIconPng(stoneTex, TextureDir + "/Icon_Stone.png");
            Object.DestroyImmediate(stoneTex);

            var ironTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            DrawIronIcon(ironTex, size);
            SaveIconPng(ironTex, TextureDir + "/Icon_Iron.png");
            Object.DestroyImmediate(ironTex);

            var foodTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            DrawFoodIcon(foodTex, size);
            SaveIconPng(foodTex, TextureDir + "/Icon_Food.png");
            Object.DestroyImmediate(foodTex);

            var popTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            DrawPopulationIcon(popTex, size);
            SaveIconPng(popTex, TextureDir + "/Icon_Population.png");
            Object.DestroyImmediate(popTex);

            var goldTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            DrawGoldIcon(goldTex, size);
            SaveIconPng(goldTex, TextureDir + "/Icon_Gold.png");
            Object.DestroyImmediate(goldTex);

            AssetDatabase.ImportAsset("Assets/UI Toolkit/Screens/InGame/BuildMenu.uss", ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset("Assets/UI Toolkit/Screens/InGame/BuildMenu.uxml", ImportAssetOptions.ForceUpdate);
        }

        static void DrawFoodIcon(Texture2D tex, int size)
        {
            var clear = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, clear);

            var outline = new Color(0.22f, 0.14f, 0.04f, 1f);
            var stalkGold = new Color(0.84f, 0.66f, 0.18f, 1f);
            var grainBright = new Color(0.98f, 0.84f, 0.28f, 1f);
            var grainWarm = new Color(0.90f, 0.70f, 0.16f, 1f);
            var tieRed = new Color(0.80f, 0.22f, 0.16f, 1f);

            // Vẽ 3 bông lúa xòe nhẹ chụm gốc ở dưới và buộc dây đỏ ở eo
            float[] angles = { -0.26f, 0f, 0.26f };
            for (int s = 0; s < angles.Length; s++)
            {
                float ang = angles[s];
                float cos = Mathf.Cos(ang);
                float sin = Mathf.Sin(ang);

                for (int y = 8; y < 56; y++)
                {
                    for (int x = 8; x < 56; x++)
                    {
                        float dx = x - 32f;
                        float dy = y - 22f;
                        float rx = dx * cos - dy * sin;
                        float ry = dx * sin + dy * cos;

                        // Thân nhánh lúa
                        if (ry >= -10f && ry <= 24f && Mathf.Abs(rx) <= 2.2f)
                        {
                            tex.SetPixel(x, y, Mathf.Abs(rx) > 1.3f ? outline : stalkGold);
                        }

                        // Hạt lúa xếp đối xứng dọc bông phía trên
                        if (ry >= 4f && ry <= 28f)
                        {
                            float localY = (ry - 4f) / 24f;
                            float earWidth = 6.8f * Mathf.Sin(Mathf.Clamp01(localY) * Mathf.PI);
                            float distX = Mathf.Abs(rx);
                            if (distX <= earWidth + 1.2f)
                            {
                                if (distX > earWidth - 0.5f)
                                {
                                    tex.SetPixel(x, y, outline);
                                }
                                else
                                {
                                    bool grainHighlight = ((int)(ry * 0.45f) & 1) == 0;
                                    tex.SetPixel(x, y, grainHighlight ? grainBright : grainWarm);
                                }
                            }
                        }
                    }
                }
            }

            // Dây buộc bó lúa màu đỏ ở phần thân dưới
            for (int y = 17; y <= 23; y++)
            {
                for (int x = 23; x <= 41; x++)
                {
                    float nx = (x - 32f) / 8.5f;
                    float ny = (y - 20f) / 2.8f;
                    float d = nx * nx + ny * ny;
                    if (d <= 1f)
                    {
                        tex.SetPixel(x, y, d > 0.72f ? outline : tieRed);
                    }
                }
            }

            tex.Apply();
        }

        static void DrawIronIcon(Texture2D tex, int size)
        {
            var clear = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, clear);

            // Hai thỏi sắt/thép ánh bạc xếp chồng chéo nhẹ
            DrawIronIngot(tex, size, new Vector2(34f, 39f), 20f, 9.5f, new Color(0.64f, 0.72f, 0.80f, 1f));
            DrawIronIngot(tex, size, new Vector2(28f, 23f), 22f, 10.5f, new Color(0.78f, 0.85f, 0.92f, 1f));
            tex.Apply();
        }

        static void DrawIronIngot(Texture2D tex, int size, Vector2 center, float halfW, float halfH, Color topColor)
        {
            var outline = new Color(0.14f, 0.17f, 0.22f, 1f);
            var bevelBright = Color.Lerp(topColor, Color.white, 0.35f);
            var sideShadow = Color.Lerp(topColor, new Color(0.24f, 0.30f, 0.38f, 1f), 0.45f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dy = (y - center.y) / halfH;
                    if (Mathf.Abs(dy) > 1f) continue;

                    // Hình thang cân (đáy rộng hơn mặt trên) tạo khối thỏi kim loại 3D
                    float taper = 1f - 0.22f * (dy + 1f) * 0.5f;
                    float currentHalfW = halfW * taper;
                    float dx = (x - center.x) / currentHalfW;
                    if (Mathf.Abs(dx) > 1f) continue;

                    if (Mathf.Abs(dx) > 0.88f || Mathf.Abs(dy) > 0.84f)
                    {
                        tex.SetPixel(x, y, outline);
                    }
                    else if (dy > 0.25f)
                    {
                        tex.SetPixel(x, y, Mathf.Abs(dx) < 0.70f ? bevelBright : topColor);
                    }
                    else if (dy > -0.15f)
                    {
                        tex.SetPixel(x, y, topColor);
                    }
                    else
                    {
                        tex.SetPixel(x, y, sideShadow);
                    }
                }
            }
        }

        static void DrawGoldIcon(Texture2D tex, int size)
        {
            var clear = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, clear);

            // Đồng xu vàng phía sau (lệch phải trên)
            DrawCoin(tex, size, new Vector2(39f, 37f), 15.5f, new Color(0.86f, 0.64f, 0.14f, 1f));
            // Đồng xu vàng chính phía trước (lệch trái dưới)
            DrawCoin(tex, size, new Vector2(26f, 27f), 17.5f, new Color(0.98f, 0.80f, 0.22f, 1f));
            tex.Apply();
        }

        static void DrawCoin(Texture2D tex, int size, Vector2 center, float radius, Color baseGold)
        {
            var outline = new Color(0.22f, 0.14f, 0.04f, 1f);
            var rimLight = Color.Lerp(baseGold, Color.white, 0.32f);
            var innerDark = Color.Lerp(baseGold, new Color(0.55f, 0.34f, 0.05f, 1f), 0.35f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center.x) / radius;
                    float dy = (y - center.y) / radius;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist > 1f) continue;

                    if (dist > 0.86f)
                    {
                        tex.SetPixel(x, y, outline);
                    }
                    else if (dist > 0.68f)
                    {
                        tex.SetPixel(x, y, (dy - dx) > 0.15f ? rimLight : baseGold);
                    }
                    else
                    {
                        // Biểu tượng chữ nhật/thoi dập nổi ở tâm đồng xu
                        bool inEmboss = Mathf.Abs(dx) < 0.16f && Mathf.Abs(dy) < 0.38f;
                        tex.SetPixel(x, y, inEmboss ? rimLight : innerDark);
                    }
                }
            }
        }

        static void DrawPopulationIcon(Texture2D tex, int size)
        {
            var clear = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, clear);

            // Người phía sau (nhỏ hơn, lệch phải)
            DrawPersonBust(tex, size, new Vector2(41f, 41f), 7.5f, new Vector2(41f, 16f), 13f, 15f,
                new Color(0.76f, 0.60f, 0.46f, 1f), new Color(0.24f, 0.44f, 0.60f, 1f));
            // Người phía trước (lớn hơn, lệch trái)
            DrawPersonBust(tex, size, new Vector2(25f, 43f), 9.0f, new Vector2(25f, 13f), 16.5f, 18.5f,
                new Color(0.92f, 0.75f, 0.58f, 1f), new Color(0.32f, 0.60f, 0.80f, 1f));
            tex.Apply();
        }

        static void DrawPersonBust(Texture2D tex, int size, Vector2 headCenter, float headRadius,
            Vector2 bodyCenter, float bodyRx, float bodyRy, Color skinColor, Color shirtColor)
        {
            var outline = new Color(0.12f, 0.14f, 0.18f, 1f);
            var shirtShadow = Color.Lerp(shirtColor, Color.black, 0.25f);
            var skinHighlight = Color.Lerp(skinColor, Color.white, 0.18f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Thân vai hình vòm
                    float bdx = (x - bodyCenter.x) / bodyRx;
                    float bdy = (y - bodyCenter.y) / bodyRy;
                    float bodyDist = Mathf.Sqrt(bdx * bdx + bdy * bdy);
                    bool inBody = y >= bodyCenter.y && bodyDist <= 1f;

                    // Đầu hình tròn
                    float hdx = (x - headCenter.x) / headRadius;
                    float hdy = (y - headCenter.y) / headRadius;
                    float headDist = Mathf.Sqrt(hdx * hdx + hdy * hdy);
                    bool inHead = headDist <= 1f;

                    if (inHead)
                    {
                        if (headDist > 0.80f)
                            tex.SetPixel(x, y, outline);
                        else
                            tex.SetPixel(x, y, hdy > 0.2f ? skinHighlight : skinColor);
                    }
                    else if (inBody)
                    {
                        if (bodyDist > 0.86f || y < bodyCenter.y + 2f)
                            tex.SetPixel(x, y, outline);
                        else
                            tex.SetPixel(x, y, bdx < 0.15f ? shirtColor : shirtShadow);
                    }
                }
            }
        }

        static void DrawWoodIcon(Texture2D tex, int size)
        {
            var clear = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, clear);

            // Vẽ 3 khúc gỗ xếp chồng: 2 khúc dưới, 1 khúc trên
            DrawLog(tex, size, 15f, 52f, 20f, 10.5f);
            DrawLog(tex, size, 21f, 56f, 20f, 10.5f);
            DrawLog(tex, size, 18f, 54f, 38f, 11.0f);
            tex.Apply();
        }

        static void DrawLog(Texture2D tex, int size, float xLeft, float xRight, float yCenter, float radius)
        {
            var outline = new Color(0.14f, 0.08f, 0.04f, 1f);
            var barkLight = new Color(0.64f, 0.40f, 0.20f, 1f);
            var barkMid = new Color(0.48f, 0.29f, 0.14f, 1f);
            var barkDark = new Color(0.32f, 0.18f, 0.08f, 1f);
            var cutLight = new Color(0.92f, 0.76f, 0.48f, 1f);
            var cutRing = new Color(0.68f, 0.50f, 0.26f, 1f);

            float rx = radius * 0.65f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dy = y - yCenter;
                    float absDy = Mathf.Abs(dy);

                    // Mặt cắt elip bên trái
                    float dxLeft = (x - xLeft) / rx;
                    float dyNorm = dy / radius;
                    float distLeft = Mathf.Sqrt(dxLeft * dxLeft + dyNorm * dyNorm);

                    // Thân khúc gỗ hình trụ
                    float dxRight = (x - xRight) / rx;
                    float distRight = Mathf.Sqrt(dxRight * dxRight + dyNorm * dyNorm);
                    bool inBody = x >= xLeft && x <= xRight && absDy <= radius;
                    bool inRightCap = x > xRight && distRight <= 1f;

                    if (distLeft <= 1f)
                    {
                        if (distLeft > 0.82f)
                            tex.SetPixel(x, y, outline);
                        else if (Mathf.Abs(distLeft - 0.52f) < 0.09f || distLeft < 0.16f)
                            tex.SetPixel(x, y, cutRing);
                        else
                            tex.SetPixel(x, y, cutLight);
                    }
                    else if (inBody || inRightCap)
                    {
                        bool isBorder = absDy > radius - 1.8f || (inRightCap && distRight > 0.82f);
                        if (isBorder)
                        {
                            tex.SetPixel(x, y, outline);
                        }
                        else
                        {
                            float t = dy / radius;
                            Color c = t > 0.25f ? barkLight : (t < -0.25f ? barkDark : barkMid);
                            if (Mathf.Abs(dy - 1.5f) < 0.7f && ((x + (int)yCenter) % 9 < 5))
                                c = Color.Lerp(c, barkDark, 0.35f);
                            tex.SetPixel(x, y, c);
                        }
                    }
                }
            }
        }

        static void DrawStoneIcon(Texture2D tex, int size)
        {
            var clear = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, clear);

            // Vẽ 2 khối đá đa diện góc cạnh xếp cạnh nhau
            DrawFacetedRock(tex, size, new Vector2(26f, 33f), 19f, 17f);
            DrawFacetedRock(tex, size, new Vector2(41f, 23f), 15f, 13.5f);
            tex.Apply();
        }

        static void DrawFacetedRock(Texture2D tex, int size, Vector2 center, float rx, float ry)
        {
            var outline = new Color(0.14f, 0.16f, 0.19f, 1f);
            var topLight = new Color(0.84f, 0.87f, 0.91f, 1f);
            var midSlate = new Color(0.62f, 0.66f, 0.72f, 1f);
            var shadowDark = new Color(0.40f, 0.44f, 0.50f, 1f);
            var ridgeBright = new Color(0.94f, 0.96f, 0.98f, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x - center.x) / rx;
                    float ny = (y - center.y) / ry;

                    // Đa giác 6 cạnh vát góc tạo khối đá tảng
                    float d1 = Mathf.Abs(nx) * 0.85f + Mathf.Abs(ny) * 0.55f;
                    float d2 = Mathf.Abs(nx) * 0.45f + Mathf.Abs(ny) * 0.92f;
                    float d3 = Mathf.Max(Mathf.Abs(nx), Mathf.Abs(ny)) * 0.95f;
                    float dist = Mathf.Max(d1, Mathf.Max(d2, d3));
                    if (ny < -0.65f) dist = 2f;

                    if (dist <= 1f)
                    {
                        if (dist > 0.87f || ny < -0.56f)
                        {
                            tex.SetPixel(x, y, outline);
                        }
                        else
                        {
                            float ridge1 = Mathf.Abs(ny - (0.15f - 0.35f * nx));
                            float ridge2 = Mathf.Abs(nx - (0.12f - 0.25f * ny));
                            if (ridge1 < 0.055f || (ny <= 0.15f - 0.35f * nx && ridge2 < 0.055f))
                            {
                                tex.SetPixel(x, y, ridgeBright);
                            }
                            else if (ny > 0.15f - 0.35f * nx)
                            {
                                tex.SetPixel(x, y, topLight);
                            }
                            else if (nx < 0.12f - 0.25f * ny)
                            {
                                tex.SetPixel(x, y, midSlate);
                            }
                            else
                            {
                                tex.SetPixel(x, y, shadowDark);
                            }
                        }
                    }
                }
            }
        }

        static void SaveIconPng(Texture2D tex, string assetPath)
        {
            byte[] png = tex.EncodeToPNG();
            string fullPath = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), assetPath);
            System.IO.File.WriteAllBytes(fullPath, png);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.GUI;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
        }
    }
}
