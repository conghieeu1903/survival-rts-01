using UnityEngine;
using UnityEngine.EventSystems;

namespace SurvivalRts
{
    // Gắn lên các khung bảng uGUI để báo trạng thái chuột đè lên giao diện.
    public class UiPointerBlocker : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        bool isInside;

        // Ghi nhận khi con trỏ chuột đi vào khung UI.
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (isInside) return;
            isInside = true;
            UiPointerState.Enter();
        }

        // Ghi nhận khi con trỏ chuột rời khỏi khung UI.
        public void OnPointerExit(PointerEventData eventData)
        {
            if (!isInside) return;
            isInside = false;
            UiPointerState.Exit();
        }

        // Tự động gỡ trạng thái đè chuột nếu khung UI bị ẩn đi.
        void OnDisable()
        {
            if (!isInside) return;
            isInside = false;
            UiPointerState.Exit();
        }
    }
}
