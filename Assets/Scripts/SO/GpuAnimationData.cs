using System;
using UnityEngine;

namespace SurvivalRts
{
    // Kết quả nướng animation: bone texture + bảng vị trí các clip trong texture.
    [CreateAssetMenu(menuName = "SurvivalRts/GPU Animation Data", fileName = "GpuAnimationData")]
    public class GpuAnimationData : ScriptableObject
    {
        [Serializable]
        public struct Clip
        {
            [Tooltip("Tên clip, ví dụ Idle, Walk, Attack, Death")]
            public string name;

            [Tooltip("Hàng đầu tiên của clip trong bone texture")]
            public int startRow;

            [Tooltip("Số frame (số hàng) của clip")]
            public int frameCount;

            [Tooltip("Số frame mỗi giây khi phát")]
            public float fps;

            [Tooltip("Lặp lại khi hết clip; tắt thì giữ frame cuối (ví dụ clip chết)")]
            public bool loop;

            public float Length => loop ? frameCount / fps : (frameCount - 1) / fps;
        }

        [Header("Texture")]
        [Tooltip("Bone texture: mỗi hàng một frame, mỗi xương 3 pixel (3 hàng đầu của ma trận skin)")]
        public Texture2D boneTexture;

        [Tooltip("Số xương của rig")]
        public int boneCount;

        [Header("Clip")]
        [Tooltip("Các clip đã nướng, theo thứ tự trong texture")]
        public Clip[] clips;

        public bool TryGetClip(string clipName, out Clip clip)
        {
            foreach (var c in clips)
            {
                if (c.name != clipName) continue;
                clip = c;
                return true;
            }
            clip = default;
            return false;
        }
    }
}
