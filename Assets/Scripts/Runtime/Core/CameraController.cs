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
        [Tooltip("Số độ xoay trên mỗi pixel chuột kéo ngang khi giữ chuột phải")]
        [SerializeField] float rotateSpeed = 0.2f;

        [Tooltip("Quãng kéo chuột phải (pixel) tối thiểu để tính là xoay camera thay vì click")]
        [SerializeField] float dragThreshold = 5f;

        Camera cam;
        float rotateDragDistance;

        // True khi lần giữ chuột phải vừa rồi là xoay camera, dùng để không coi nó là click hủy.
        public bool RotatedWithRightButton => rotateDragDistance > dragThreshold;

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
                float dx = mouse.delta.ReadValue().x;
                rotateDragDistance += Mathf.Abs(dx);
                if (RotatedWithRightButton) transform.RotateAround(GroundPivot(), Vector3.up, dx * rotateSpeed);
            }

            var input = Vector2.zero;
            if (keyboard.wKey.isPressed) input.y += 1f;
            if (keyboard.sKey.isPressed) input.y -= 1f;
            if (keyboard.dKey.isPressed) input.x += 1f;
            if (keyboard.aKey.isPressed) input.x -= 1f;

            var forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            var right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            var move = (forward * input.y + right * input.x) * (moveSpeed * Time.unscaledDeltaTime);

            if (mouse.middleButton.isPressed)
            {
                var drag = mouse.delta.ReadValue();
                float worldPerPixel = 2f * cam.orthographicSize / Screen.height;
                float pitch = transform.eulerAngles.x * Mathf.Deg2Rad;
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

        // Điểm trên mặt đất mà camera đang nhìn vào (giữa màn hình).
        Vector3 GroundPivot()
        {
            var ray = new Ray(transform.position, transform.forward);
            return new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance)
                ? ray.GetPoint(distance)
                : transform.position;
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
