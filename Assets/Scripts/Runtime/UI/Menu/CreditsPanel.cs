using System;
using TMPro;
using UnityEngine;

namespace SurvivalRts
{
    // Màn Credits theo panel Godot: ô huy hiệu giữ chỗ, tiêu đề và danh sách công nghệ dùng trong game, mỗi dòng có nút mở trang chủ.
    public class CreditsPanel
    {
        static readonly string[,] Entries =
        {
            { "Unity 6", "Engine, Entities (ECS), Burst và Entities Graphics cho hàng chục nghìn lính", "https://unity.com" },
            { "FishNet", "Thư viện mạng; game dùng khóa bước (lockstep) trên nền Tugboat", "https://fish-networking.gitbook.io" },
            { "TextMeshPro", "Hiển thị chữ rõ nét, hỗ trợ tiếng Việt có dấu", "https://docs.unity3d.com/Packages/com.unity.textmeshpro@latest" },
        };

        readonly Action onBack;
        RectTransform root;

        public GameObject Root => root.gameObject;

        public CreditsPanel(Action onBack)
        {
            this.onBack = onBack;
        }

        public void Build(Transform parent)
        {
            var card = BattleUiKit.ScreenCard(parent, "Credits", "Credits", "Survival RTS", 480f, 110f);
            root = card.parent as RectTransform;

            var body = BattleUiKit.Node(card, "Body");
            var bodyElement = body.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            bodyElement.flexibleHeight = 1f;
            BattleUiKit.Vertical(body, 24f, 0, TextAnchor.MiddleCenter);

            var badgeRow = BattleUiKit.Node(body, "BadgeRow");
            BattleUiKit.Size(badgeRow, -1f, 84f);
            BattleUiKit.Horizontal(badgeRow, 0f, 0, TextAnchor.MiddleCenter);
            var badge = BattleUiKit.Placeholder(badgeRow, "SURVIVAL RTS", 84f, 24f);
            BattleUiKit.Size(badge, 220f, 84f);
            var title = BattleUiKit.Label(body, "Công nghệ sử dụng trong game", 26f, TextAlignmentOptions.Center);
            BattleUiKit.Size(title, -1f, 40f);

            for (int i = 0; i < Entries.GetLength(0); i++)
            {
                string url = Entries[i, 2];
                var row = BattleUiKit.SettingRow(body, "");
                BattleUiKit.Size(row, -1f, 84f);
                var info = BattleUiKit.Node(row, "Info");
                BattleUiKit.Vertical(info, 4f, 0, TextAnchor.MiddleLeft);
                BattleUiKit.Size(info, -1f, -1f, 1f);
                BattleUiKit.Size(BattleUiKit.Label(info, Entries[i, 0], 20f), -1f, 28f);
                BattleUiKit.Size(BattleUiKit.Label(info, Entries[i, 1], 15f, TextAlignmentOptions.Left, BattleUiKit.DimTextColor), -1f, 40f);
                BattleUiKit.PushButton(row, "Mở trang chủ", () => Application.OpenURL(url), 190f, 44f, 18f);
                // Bỏ nhãn tên rỗng mà SettingRow tạo sẵn
                Destroy(row.GetChild(0).gameObject);
            }

            var footer = BattleUiKit.Footer(card);
            var status = BattleUiKit.Label(footer, "Nhấn vào nút để mở trên trình duyệt", 16f, TextAlignmentOptions.Left, BattleUiKit.DimTextColor);
            BattleUiKit.Size(status, -1f, -1f, 1f);
            BattleUiKit.PushButton(footer, "Quay lại", () => onBack?.Invoke(), 160f, 48f);
        }

        static void Destroy(GameObject target) => UnityEngine.Object.Destroy(target);

        public void Show() => root.gameObject.SetActive(true);

        public void Hide() => root.gameObject.SetActive(false);
    }
}
