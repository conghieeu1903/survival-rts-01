using UnityEngine;

namespace SurvivalRts
{
    // Phát animation nướng sẵn (bone texture) trên MeshRenderer thường thông qua MaterialPropertyBlock.
    [ExecuteAlways]
    [RequireComponent(typeof(MeshRenderer))]
    public class GpuMeshAnimator : MonoBehaviour
    {
        static readonly int AnimDataId = Shader.PropertyToID("_AnimData");

        [Header("Dữ liệu")]
        [Tooltip("Bảng clip và bone texture đã nướng bằng GpuAnimationBaker")]
        [SerializeField] GpuAnimationData animationData;

        [Tooltip("Tên clip phát mặc định khi khởi tạo")]
        [SerializeField] string defaultClip = "Idle";

        MeshRenderer meshRenderer;
        MaterialPropertyBlock propertyBlock;
        GpuAnimationData.Clip currentClip;
        string currentName;
        float time;
        float phaseOffset;

        public GpuAnimationData AnimationData => animationData;
        public string CurrentClip => currentName;

        public void Setup(GpuAnimationData data, string initialClip = "Idle")
        {
            animationData = data;
            defaultClip = initialClip;
            currentName = null;
            Play(defaultClip, true);
        }

        public void Play(string clipName, bool restart = false)
        {
            if (animationData == null || animationData.clips == null || animationData.clips.Length == 0) return;
            if (!restart && currentName == clipName) return;

            if (!animationData.TryGetClip(clipName, out var clip))
            {
                if (!animationData.TryGetClip("Idle", out clip)) clip = animationData.clips[0];
            }

            bool wasLoop = currentClip.loop;
            currentClip = clip;
            currentName = clipName;
            if (restart || !clip.loop || !wasLoop) time = 0f;
            SampleAndApply();
        }

        void OnEnable()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
            phaseOffset = Application.isPlaying ? Random.Range(0f, 5f) : 0f;
            Play(string.IsNullOrEmpty(currentName) ? defaultClip : currentName, true);
        }

        void Update()
        {
            if (animationData == null || currentClip.frameCount <= 0) return;
            if (Application.isPlaying) time += Time.deltaTime;
            SampleAndApply();
        }

        void SampleAndApply()
        {
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
            if (currentClip.frameCount <= 0) return;

            float sampleTime = currentClip.loop ? time + phaseOffset : time;
            float frame = sampleTime * currentClip.fps;
            int a;
            int b;
            if (currentClip.loop)
            {
                frame -= Mathf.Floor(frame / currentClip.frameCount) * currentClip.frameCount;
                a = (int)frame;
                b = (a + 1) % currentClip.frameCount;
            }
            else
            {
                frame = Mathf.Min(frame, currentClip.frameCount - 1);
                a = (int)frame;
                b = Mathf.Min(a + 1, currentClip.frameCount - 1);
            }

            propertyBlock.SetVector(AnimDataId, new Vector4(currentClip.startRow + a, currentClip.startRow + b, frame - a, 0f));
            meshRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
