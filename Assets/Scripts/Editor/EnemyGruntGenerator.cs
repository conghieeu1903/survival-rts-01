using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SurvivalRts.Editor
{
    // Tạo kẻ địch thử "Grunt" hoàn toàn bằng code: rig khối hộp, 4 clip animation, bone texture, material,
    // rồi gán vào EnemyData để ECS dùng ngay. Chạy lại menu sẽ ghi đè các asset cũ.
    public static class EnemyGruntGenerator
    {
        const float BakeFps = 30f;
        const string ModelDir = "Assets/Arts/Models";
        const string AnimDir = "Assets/Arts/Animations";
        const string TextureDir = "Assets/Arts/Textures";
        const string MaterialDir = "Assets/Arts/Materials";
        const string AnimDataDir = "Assets/ScriptableObjects/Animations";
        const string EnemyDataPath = "Assets/ScriptableObjects/Enemies/Enemy_Basic.asset";

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

        // Vị trí xương là vị trí cục bộ so với xương cha lúc bind (không xoay).
        static readonly BoneDef[] Bones =
        {
            new BoneDef { Name = "Hips", Parent = -1, Position = new Vector3(0f, 0.95f, 0f) },
            new BoneDef { Name = "Spine", Parent = Hips, Position = new Vector3(0f, 0.1f, 0f) },
            new BoneDef { Name = "Head", Parent = Spine, Position = new Vector3(0f, 0.55f, 0f) },
            new BoneDef { Name = "UpperArm.L", Parent = Spine, Position = new Vector3(-0.28f, 0.48f, 0f) },
            new BoneDef { Name = "Forearm.L", Parent = ArmL, Position = new Vector3(0f, -0.3f, 0f) },
            new BoneDef { Name = "UpperArm.R", Parent = Spine, Position = new Vector3(0.28f, 0.48f, 0f) },
            new BoneDef { Name = "Forearm.R", Parent = ArmR, Position = new Vector3(0f, -0.3f, 0f) },
            new BoneDef { Name = "Thigh.L", Parent = Hips, Position = new Vector3(-0.12f, -0.05f, 0f) },
            new BoneDef { Name = "Shin.L", Parent = ThighL, Position = new Vector3(0f, -0.45f, 0f) },
            new BoneDef { Name = "Thigh.R", Parent = Hips, Position = new Vector3(0.12f, -0.05f, 0f) },
            new BoneDef { Name = "Shin.R", Parent = ThighR, Position = new Vector3(0f, -0.45f, 0f) },
        };

        static readonly Color Skin = new Color(0.45f, 0.62f, 0.35f);
        static readonly Color Cloth = new Color(0.5f, 0.22f, 0.16f);
        static readonly Color Pants = new Color(0.26f, 0.23f, 0.2f);
        static readonly Color Boots = new Color(0.12f, 0.1f, 0.09f);
        static readonly Color Wood = new Color(0.42f, 0.28f, 0.15f);
        static readonly Color Eye = new Color(0.05f, 0.03f, 0.02f);

        // Tâm khối tính so với gốc xương; trục +Z là mặt trước.
        static readonly PartDef[] Parts =
        {
            new PartDef { Bone = Hips, Center = new Vector3(0f, 0.02f, 0f), Size = new Vector3(0.34f, 0.16f, 0.2f), Color = Pants },
            new PartDef { Bone = Spine, Center = new Vector3(0f, 0.25f, 0f), Size = new Vector3(0.42f, 0.45f, 0.24f), Color = Cloth },
            new PartDef { Bone = Head, Center = new Vector3(0f, 0.13f, 0f), Size = new Vector3(0.24f, 0.26f, 0.24f), Color = Skin },
            new PartDef { Bone = Head, Center = new Vector3(-0.055f, 0.15f, 0.121f), Size = new Vector3(0.045f, 0.035f, 0.01f), Color = Eye },
            new PartDef { Bone = Head, Center = new Vector3(0.055f, 0.15f, 0.121f), Size = new Vector3(0.045f, 0.035f, 0.01f), Color = Eye },
            new PartDef { Bone = ArmL, Center = new Vector3(0f, -0.14f, 0f), Size = new Vector3(0.11f, 0.3f, 0.11f), Color = Skin },
            new PartDef { Bone = ForearmL, Center = new Vector3(0f, -0.14f, 0f), Size = new Vector3(0.1f, 0.3f, 0.1f), Color = Skin },
            new PartDef { Bone = ArmR, Center = new Vector3(0f, -0.14f, 0f), Size = new Vector3(0.11f, 0.3f, 0.11f), Color = Skin },
            new PartDef { Bone = ForearmR, Center = new Vector3(0f, -0.14f, 0f), Size = new Vector3(0.1f, 0.3f, 0.1f), Color = Skin },
            new PartDef { Bone = ForearmR, Center = new Vector3(0f, -0.3f, 0.2f), Size = new Vector3(0.08f, 0.08f, 0.45f), Color = Wood },
            new PartDef { Bone = ThighL, Center = new Vector3(0f, -0.22f, 0f), Size = new Vector3(0.14f, 0.45f, 0.14f), Color = Pants },
            new PartDef { Bone = ShinL, Center = new Vector3(0f, -0.2f, 0f), Size = new Vector3(0.13f, 0.42f, 0.13f), Color = Pants },
            new PartDef { Bone = ShinL, Center = new Vector3(0f, -0.42f, 0.04f), Size = new Vector3(0.14f, 0.07f, 0.22f), Color = Boots },
            new PartDef { Bone = ThighR, Center = new Vector3(0f, -0.22f, 0f), Size = new Vector3(0.14f, 0.45f, 0.14f), Color = Pants },
            new PartDef { Bone = ShinR, Center = new Vector3(0f, -0.2f, 0f), Size = new Vector3(0.13f, 0.42f, 0.13f), Color = Pants },
            new PartDef { Bone = ShinR, Center = new Vector3(0f, -0.42f, 0.04f), Size = new Vector3(0.14f, 0.07f, 0.22f), Color = Boots },
        };

        delegate void PoseFunction(float t, Vector3[] euler, ref Vector3 hips);

        [MenuItem("SurvivalRts/Tạo kẻ địch thử (Grunt)")]
        public static void Generate()
        {
            EnsureFolder(ModelDir);
            EnsureFolder(AnimDir);
            EnsureFolder(TextureDir);
            EnsureFolder(AnimDataDir);

            var root = new GameObject("EnemyGrunt_Rig");
            try
            {
                var bones = BuildRig(root);
                var bindposes = new Matrix4x4[bones.Length];
                for (int b = 0; b < bones.Length; b++)
                    bindposes[b] = bones[b].worldToLocalMatrix * root.transform.localToWorldMatrix;

                var mesh = BuildMesh(bones, bindposes);
                mesh = SaveAsset(mesh, ModelDir + "/EnemyGrunt_Mesh.asset");

                var sources = new List<GpuAnimationBaker.ClipSource>
                {
                    MakeClip(bones, root, "Idle", 2f, true, IdlePose),
                    MakeClip(bones, root, "Walk", 0.8f, true, WalkPose),
                    MakeClip(bones, root, "Attack", 1f, true, AttackPose),
                    MakeClip(bones, root, "Death", 1.2f, false, DeathPose),
                };

                var animData = GpuAnimationBaker.Bake(root, bones, bindposes, sources, BakeFps,
                    TextureDir + "/EnemyGrunt_BoneTex.asset", AnimDataDir + "/EnemyGrunt_Anim.asset");

                ResetPose(bones);
                SaveRigPrefab(root, bones, mesh);

                var material = CreateMaterial(animData.boneTexture);
                AssignToEnemyData(mesh, material, animData);

                AssetDatabase.SaveAssets();
                Debug.Log($"EnemyGrunt: {mesh.triangles.Length / 3} tam giác, {bones.Length} xương, {animData.boneTexture.height} frame.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

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

        static void ResetPose(Transform[] bones)
        {
            for (int b = 0; b < bones.Length; b++)
            {
                bones[b].localPosition = Bones[b].Position;
                bones[b].localRotation = Quaternion.identity;
            }
        }

        static Mesh BuildMesh(Transform[] bones, Matrix4x4[] bindposes)
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

            foreach (var part in Parts)
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

            var mesh = new Mesh { name = "EnemyGrunt_Mesh" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetUVs(1, boneUv);
            mesh.SetTriangles(triangles, 0);
            mesh.boneWeights = weights.ToArray();
            mesh.bindposes = bindposes;
            // Bounds đủ rộng cho mọi tư thế (giơ gậy, ngã ra sau), đáy ở y = 0 tức chân nhân vật.
            mesh.bounds = new Bounds(new Vector3(0f, 1.2f, 0f), new Vector3(3f, 2.4f, 3f));
            return mesh;
        }

        static GpuAnimationBaker.ClipSource MakeClip(Transform[] bones, GameObject root, string clipName,
            float length, bool loop, PoseFunction pose)
        {
            var clip = new AnimationClip { name = "EnemyGrunt_" + clipName, frameRate = BakeFps };
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
                    // Giữ dấu quaternion liên tục để nội suy không quay vòng ngược.
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

            clip = SaveAsset(clip, AnimDir + "/EnemyGrunt_" + clipName + ".anim");
            return new GpuAnimationBaker.ClipSource { Name = clipName, Clip = clip, Loop = loop };
        }

        // ---------- Tư thế (độ, trục Euler của Unity; +X ở chân/tay = vung ra sau, -X = vung ra trước) ----------

        static void IdlePose(float t, Vector3[] e, ref Vector3 hips)
        {
            float p = t / 2f * Mathf.PI * 2f;
            float s = Mathf.Sin(p);
            e[Spine] = new Vector3(4f + 2f * s, 0f, 0f);
            e[Head] = new Vector3(-3f + 1.5f * Mathf.Sin(p + 1f), 0f, 0f);
            e[ArmL] = new Vector3(-5f + 2f * s, 0f, -6f);
            e[ForearmL] = new Vector3(-15f, 0f, 0f);
            e[ArmR] = new Vector3(-8f + 2f * s, 0f, 6f);
            e[ForearmR] = new Vector3(-30f, 0f, 0f);
            e[ThighL] = new Vector3(0f, 0f, -2f);
            e[ThighR] = new Vector3(0f, 0f, 2f);
            hips.y = 0.95f - 0.01f * (1f - Mathf.Cos(p));
        }

        static void WalkPose(float t, Vector3[] e, ref Vector3 hips)
        {
            float p = t / 0.8f * Mathf.PI * 2f;
            float s = Mathf.Sin(p);
            float c = Mathf.Cos(p);
            e[Hips] = new Vector3(0f, -5f * s, 0f);
            e[Spine] = new Vector3(8f, 6f * s, 0f);
            e[Head] = new Vector3(-6f, -6f * s, 0f);
            e[ThighL] = new Vector3(-28f * s, 0f, 0f);
            e[ThighR] = new Vector3(28f * s, 0f, 0f);
            e[ShinL] = new Vector3(5f + 40f * Mathf.Max(0f, c), 0f, 0f);
            e[ShinR] = new Vector3(5f + 40f * Mathf.Max(0f, -c), 0f, 0f);
            e[ArmL] = new Vector3(25f * s, 0f, -6f);
            e[ForearmL] = new Vector3(-20f, 0f, 0f);
            e[ArmR] = new Vector3(-25f * s - 10f, 0f, 6f);
            e[ForearmR] = new Vector3(-35f, 0f, 0f);
            hips.y = 0.91f + 0.04f * Mathf.Abs(c);
        }

        static void AttackPose(float t, Vector3[] e, ref Vector3 hips)
        {
            e[ArmR] = new Vector3(Keys(t, 0f, -20f, 0.4f, -160f, 0.55f, -30f, 1f, -20f), 0f, 8f);
            e[ForearmR] = new Vector3(Keys(t, 0f, -30f, 0.4f, -70f, 0.55f, -5f, 1f, -30f), 0f, 0f);
            e[Spine] = new Vector3(Keys(t, 0f, 8f, 0.4f, -6f, 0.55f, 22f, 1f, 8f),
                Keys(t, 0f, 0f, 0.4f, -25f, 0.55f, 18f, 1f, 0f), 0f);
            e[Hips] = new Vector3(0f, Keys(t, 0f, 0f, 0.4f, -10f, 0.55f, 10f, 1f, 0f), 0f);
            e[Head] = new Vector3(Keys(t, 0f, -5f, 0.55f, -15f, 1f, -5f), 0f, 0f);
            e[ArmL] = new Vector3(-35f, 0f, -10f);
            e[ForearmL] = new Vector3(-70f, 0f, 0f);
            e[ThighL] = new Vector3(-18f, 0f, 0f);
            e[ShinL] = new Vector3(12f, 0f, 0f);
            e[ThighR] = new Vector3(14f, 0f, 0f);
            e[ShinR] = new Vector3(8f, 0f, 0f);
            hips.y = Keys(t, 0f, 0.92f, 0.55f, 0.88f, 1f, 0.92f);
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

        // Nội suy mượt (smoothstep) qua các cặp (thời điểm, giá trị); ngoài khoảng thì giữ giá trị biên.
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

        // ---------- Lưu asset ----------

        static void SaveRigPrefab(GameObject root, Transform[] bones, Mesh mesh)
        {
            var preview = AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + "/M_EnemyGrunt_Preview.mat");
            if (preview == null)
            {
                preview = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                preview.SetColor("_BaseColor", Cloth);
                AssetDatabase.CreateAsset(preview, MaterialDir + "/M_EnemyGrunt_Preview.mat");
            }

            var meshObject = new GameObject("Mesh");
            meshObject.transform.SetParent(root.transform, false);
            var skinned = meshObject.AddComponent<SkinnedMeshRenderer>();
            skinned.sharedMesh = mesh;
            skinned.bones = bones;
            skinned.rootBone = bones[Hips];
            skinned.sharedMaterial = preview;
            skinned.localBounds = mesh.bounds;
            PrefabUtility.SaveAsPrefabAsset(root, ModelDir + "/EnemyGrunt_Rig.prefab");
        }

        static Material CreateMaterial(Texture2D boneTexture)
        {
            string path = MaterialDir + "/M_EnemyGrunt.mat";
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

        static void AssignToEnemyData(Mesh mesh, Material material, GpuAnimationData animation)
        {
            var data = AssetDatabase.LoadAssetAtPath<EnemyData>(EnemyDataPath);
            if (data == null) return;
            data.mesh = mesh;
            data.material = material;
            data.animation = animation;
            data.scale = 0.9f;
            EditorUtility.SetDirty(data);
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
    }
}
