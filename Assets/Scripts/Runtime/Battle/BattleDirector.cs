using Unity.Entities;
using UnityEngine;

namespace SurvivalRts
{
    // Nối mô phỏng với hiển thị: tạo entity cho toàn bộ lính khi trận bắt đầu và vẽ mũi tên của lính bắn xa.
    [RequireComponent(typeof(BattleSimulation))]
    public class BattleDirector : MonoBehaviour
    {
        [Tooltip("Vẽ tia mũi tên cho lính bắn xa; để trống thì không vẽ")]
        [SerializeField] TracerPool arrowTracers;

        [Tooltip("Camera nhìn bản đồ, để chỉ báo đòn đánh trong khung nhìn")]
        [SerializeField] CameraController cameraController;

        [Tooltip("Lề (m) cộng thêm quanh khung nhìn khi lọc đòn đánh")]
        [SerializeField] float viewMargin = 6f;

        readonly Vector3[] corners = new Vector3[4];
        BattleSimulation simulation;

        // Chỉ báo hình ảnh và âm thanh của những đòn nằm trong khung nhìn camera.
        void LateUpdate()
        {
            if (cameraController == null || !cameraController.GetGroundFrustumCorners(corners)) return;

            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            foreach (var c in corners)
            {
                minX = Mathf.Min(minX, c.x);
                minZ = Mathf.Min(minZ, c.z);
                maxX = Mathf.Max(maxX, c.x);
                maxZ = Mathf.Max(maxZ, c.z);
            }
            simulation.EffectArea = Rect.MinMaxRect(minX - viewMargin, minZ - viewMargin, maxX + viewMargin, maxZ + viewMargin);
        }

        void Awake()
        {
            simulation = GetComponent<BattleSimulation>();
        }

        void OnEnable()
        {
            simulation.MatchStarted += OnMatchStarted;
            if (arrowTracers != null) simulation.ShotFired += arrowTracers.Emit;
        }

        void OnDisable()
        {
            simulation.MatchStarted -= OnMatchStarted;
            if (arrowTracers != null) simulation.ShotFired -= arrowTracers.Emit;
            var world = World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
                world.GetExistingSystemManaged<BattleUnitPresentation>()?.Clear();
        }

        void OnMatchStarted()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;

            world.GetExistingSystemManaged<BattleUnitPresentation>().Build(simulation.Data, simulation);
        }
    }
}
