using UnityEngine.EventSystems;

namespace SurvivalRts
{
    // Trạng thái con trỏ chuột có đang nằm trên giao diện hay không để chặn click xuống bản đồ.
    public static class UiPointerState
    {
        static int hoverCount; // Số khung UI đang có chuột đè lên
        static bool forcedOver;

        public static bool IsOverUI
        {
            get => forcedOver || hoverCount > 0 || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject());
            set
            {
                forcedOver = value;
                if (!value) hoverCount = 0;
            }
        }

        // Tăng bộ đếm khi chuột đi vào vùng giao diện.
        public static void Enter()
        {
            hoverCount++;
        }

        // Giảm bộ đếm khi chuột rời khỏi hoặc bảng giao diện tắt.
        public static void Exit()
        {
            if (hoverCount > 0) hoverCount--;
        }
    }
}
