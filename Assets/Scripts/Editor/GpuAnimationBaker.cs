using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SurvivalRts.Editor
{
    // Nướng AnimationClip thành bone texture cho shader SurvivalRts/GpuSkinnedLit.
    // Dùng được cho mọi rig: chỉ cần root, danh sách xương theo đúng thứ tự chỉ số xương trong mesh, và bindpose.
    public static class GpuAnimationBaker
    {
        public struct ClipSource
        {
            public string Name;
            public AnimationClip Clip;
            public bool Loop;
        }

        public static GpuAnimationData Bake(GameObject root, Transform[] bones, Matrix4x4[] bindposes,
            IList<ClipSource> sources, float fps, string texturePath, string dataPath)
        {
            var clips = new GpuAnimationData.Clip[sources.Count];
            int totalRows = 0;
            for (int i = 0; i < sources.Count; i++)
            {
                float length = sources[i].Clip.length;
                int frames = sources[i].Loop
                    ? Mathf.Max(1, Mathf.RoundToInt(length * fps))
                    : Mathf.RoundToInt(length * fps) + 1;
                clips[i] = new GpuAnimationData.Clip
                {
                    name = sources[i].Name,
                    startRow = totalRows,
                    frameCount = frames,
                    fps = fps,
                    loop = sources[i].Loop
                };
                totalRows += frames;
            }

            var texture = new Texture2D(bones.Length * 3, totalRows, TextureFormat.RGBAFloat, false, true)
            {
                name = System.IO.Path.GetFileNameWithoutExtension(texturePath),
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            // bindpose = bone.worldToLocal * root.localToWorld lúc bind (quy ước của Unity),
            // nên ma trận skin trong không gian root = worldToRoot * bone.localToWorld(t) * bindpose.
            var pixels = new Color[texture.width * texture.height];
            for (int c = 0; c < clips.Length; c++)
            {
                for (int f = 0; f < clips[c].frameCount; f++)
                {
                    sources[c].Clip.SampleAnimation(root, f / fps);
                    var worldToRoot = root.transform.worldToLocalMatrix;
                    int row = clips[c].startRow + f;
                    for (int b = 0; b < bones.Length; b++)
                    {
                        var skin = worldToRoot * bones[b].localToWorldMatrix * bindposes[b];
                        int x = b * 3;
                        pixels[row * texture.width + x] = skin.GetRow(0);
                        pixels[row * texture.width + x + 1] = skin.GetRow(1);
                        pixels[row * texture.width + x + 2] = skin.GetRow(2);
                    }
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, false);

            var existingTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (existingTexture != null) AssetDatabase.DeleteAsset(texturePath);
            AssetDatabase.CreateAsset(texture, texturePath);

            var data = AssetDatabase.LoadAssetAtPath<GpuAnimationData>(dataPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<GpuAnimationData>();
                AssetDatabase.CreateAsset(data, dataPath);
            }
            data.boneTexture = texture;
            data.boneCount = bones.Length;
            data.clips = clips;
            EditorUtility.SetDirty(data);
            return data;
        }
    }
}
