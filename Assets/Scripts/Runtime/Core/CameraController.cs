using UnityEngine;
using UnityEngine.InputSystem;

namespace SurvivalRts
{
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        [Header("Di chuyển")]
        [Tooltip("Tốc độ di chuyển bằng WASD (m/giây)")]
        [SerializeField] float moveSpeed = 20f;

        [Tooltip("Nửa cạnh vùng camera được phép nhìn vào, tính từ gốc tọa độ (m)")]
        [SerializeField] float boundsHalfSize = 25f;

        [Header("Zoom")]
        [Tooltip("Mức thay đổi orthographic size mỗi nấc con lăn")]
        [SerializeField] float zoomStep = 2f;

        [Tooltip("Orthographic size nhỏ nhất (x) và lớn nhất (y)")]
        [SerializeField] Vector2 zoomRange = new Vector2(6f, 25f);

        [Header("Xoay")]
        [Tooltip("Số độ xoay trên mỗi pixel chuột kéo khi giữ chuột phải")]
        [SerializeField] float rotateSpeed = 0.2f;

        [Tooltip("Giới hạn góc nghiêng camera nhìn xuống mặt đất (độ): nhỏ nhất (x) và lớn nhất (y)")]
        [SerializeField] Vector2 pitchRange = new Vector2(20f, 80f);

        [Tooltip("Quãng kéo chuột phải (pixel) tối thiểu để tính là xoay camera thay vì click")]
        [SerializeField] float dragThreshold = 5f;

        Camera cam;
        float rotateDragDistance;

        // True khi lần giữ chuột phải vừa rồi là xoay camera, dùng để không coi nó là click hủy.
        public bool RotatedWithRightButton => rotateDragDistance > dragThreshold;

        public float OrthographicSize => cam != null ? cam.orthographicSize : zoomRange.y;

        public Vector2 ZoomRange => zoomRange;

        void Awake()
        {
            cam = GetComponent<Camera>();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null) return;

            if (mouse.rightButton.wasPressedThisFrame) rotateDragDistance = 0f;
            if (mouse.rightButton.isPressed)
            {
                var delta = mouse.delta.ReadValue();
                rotateDragDistance += delta.magnitude;
                if (RotatedWithRightButton)
                {
                    OrbitAroundPivot(delta.x * rotateSpeed, -delta.y * rotateSpeed);
                }
            }

            var input = Vector2.zero;
            if (keyboard.wKey.isPressed) input.y += 1f;
            if (keyboard.sKey.isPressed) input.y -= 1f;
            if (keyboard.dKey.isPressed) input.x += 1f;
            if (keyboard.aKey.isPressed) input.x -= 1f;

            var yawRotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            var forward = yawRotation * Vector3.forward;
            var right = yawRotation * Vector3.right;
            var move = (forward * input.y + right * input.x) * (moveSpeed * Time.unscaledDeltaTime);

            if (mouse.middleButton.isPressed)
            {
                var drag = mouse.delta.ReadValue();
                float worldPerPixel = 2f * cam.orthographicSize / Screen.height;
                float pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, transform.eulerAngles.x), pitchRange.x, pitchRange.y) * Mathf.Deg2Rad;
                move -= (right * drag.x + forward * (drag.y / Mathf.Sin(pitch))) * worldPerPixel;
            }

            transform.position += move;
            ClampToBounds();

            float scroll = mouse.scroll.ReadValue().y;
            if (scroll != 0f)
            {
                cam.orthographicSize = Mathf.Clamp(
                    cam.orthographicSize - Mathf.Sign(scroll) * zoomStep, zoomRange.x, zoomRange.y);
            }
        }

        void OrbitAroundPivot(float yawDelta, float pitchDelta)
        {
            var pivot = GroundPivot();
            float distance = Vector3.Distance(transform.position, pivot);
            var euler = transform.eulerAngles;
            float yaw = euler.y + yawDelta;
            float pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, euler.x) + pitchDelta, pitchRange.x, pitchRange.y);
            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.SetPositionAndRotation(pivot - rotation * Vector3.forward * distance, rotation);
        }

        // Điểm trên mặt đất mà camera đang nhìn vào (giữa màn hình).
        public Vector3 GroundPivot()
        {
            var ray = new Ray(transform.position, transform.forward);
            return new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance)
                ? ray.GetPoint(distance)
                : transform.position;
        }

        // Dịch chuyển camera tới nhìn thẳng vào một điểm (x, z) trên mặt đất (dùng khi bấm hoặc kéo trên Minimap).
        public void FocusOnGroundPoint(Vector3 groundTarget)
        {
            var pivot = GroundPivot();
            var target = new Vector3(groundTarget.x, pivot.y, groundTarget.z);
            transform.position += target - pivot;
            ClampToBounds();
        }

        // Tính 4 góc khung nhìn của camera chiếu xuống mặt phẳng y = 0 để vẽ khung trắng trên Minimap.
        public bool GetGroundFrustumCorners(Vector3[] outCorners)
        {
            if (cam == null || outCorners == null || outCorners.Length < 4) return false;
            var plane = new Plane(Vector3.up, Vector3.zero);
            Vector2[] viewportCorners =
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0f)
            };

            for (int i = 0; i < 4; i++)
            {
                var ray = cam.ViewportPointToRay(new Vector3(viewportCorners[i].x, viewportCorners[i].y, 0f));
                if (plane.Raycast(ray, out float dist))
                {
                    outCorners[i] = ray.GetPoint(dist);
                }
                else
                {
                    outCorners[i] = GroundPivot();
                }
            }
            return true;
        }

        void ClampToBounds()
        {
            var pivot = GroundPivot();
            var clamped = new Vector3(
                Mathf.Clamp(pivot.x, -boundsHalfSize, boundsHalfSize),
                pivot.y,
                Mathf.Clamp(pivot.z, -boundsHalfSize, boundsHalfSize));
            transform.position += clamped - pivot;
        }
    }
}
