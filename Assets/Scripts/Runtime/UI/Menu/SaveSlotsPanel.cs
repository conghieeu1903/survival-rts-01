using System;
using System.IO;
using TMPro;
using UnityEngine;

namespace SurvivalRts
{
    // Màn danh sách bản lưu theo panel Godot: tối đa 6 bản, liệt kê file *.sav trong thư mục saves, có nút xoá.
    // Game chưa có chức năng ghi bản lưu nên danh sách hiện trống.
    public class SaveSlotsPanel
    {
        public const int MaxSlots = 6;

        readonly Action onBack;
        RectTransform root;
        RectTransform list;
        TMP_Text noteLabel;
        TMP_Text statusLabel;

        static string Folder => Path.Combine(Application.persistentDataPath, "saves");

        public GameObject Root => root.gameObject;

        public SaveSlotsPanel(Action onBack)
        {
            this.onBack = onBack;
        }

        public void Build(Transform parent)
        {
            var card = BattleUiKit.ScreenCard(parent, "SaveSlots", "Danh sách bản lưu", "", 180f, 48f);
            root = card.parent as RectTransform;
            noteLabel = card.GetChild(0).GetChild(1).GetComponent<TMP_Text>();

            list = BattleUiKit.ScrollList(card, 4f);

            var footer = BattleUiKit.Footer(card);
            statusLabel = BattleUiKit.Label(footer, "Tối đa " + MaxSlots + " bản lưu", 16f, TextAlignmentOptions.Left, BattleUiKit.DimTextColor);
            BattleUiKit.Size(statusLabel, -1f, -1f, 1f);
            BattleUiKit.PushButton(footer, "Quay lại", () => onBack?.Invoke(), 160f, 48f);

            Refresh();
        }

        public void Show()
        {
            Refresh();
            root.gameObject.SetActive(true);
        }

        public void Hide()
        {
            root.gameObject.SetActive(false);
        }

        void Refresh()
        {
            for (int i = list.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(list.GetChild(i).gameObject);

            var files = Directory.Exists(Folder) ? Directory.GetFiles(Folder, "*.sav") : new string[0];
            int count = Mathf.Min(files.Length, MaxSlots);
            BattleUiKit.SetText(noteLabel, $"Bản lưu · {count} / {MaxSlots}");

            if (count == 0)
            {
                var empty = BattleUiKit.Panel(list, "Empty", BattleUiKit.RowColor);
                BattleUiKit.Size(empty, -1f, 150f);
                BattleUiKit.Vertical(empty.rectTransform, 0f, 24, TextAnchor.MiddleCenter);
                var text = BattleUiKit.Label(empty.transform,
                    $"Chưa có bản lưu nào (0 / {MaxSlots}).\nChức năng lưu game sẽ được thêm sau.",
                    18f, TextAlignmentOptions.Center, BattleUiKit.DimTextColor);
                BattleUiKit.Size(text, -1f, 70f);
                return;
            }

            for (int i = 0; i < count; i++)
            {
                string file = files[i];
                var row = BattleUiKit.Panel(list, "Slot" + i, BattleUiKit.RowColor);
                BattleUiKit.Size(row, -1f, 64f);
                BattleUiKit.Horizontal(row.rectTransform, 12f, 10);
                var info = BattleUiKit.Label(row.transform,
                    Path.GetFileNameWithoutExtension(file) + "   " + File.GetLastWriteTime(file).ToString("dd/MM/yyyy HH:mm"), 20f);
                BattleUiKit.Size(info, -1f, -1f, 1f);
                BattleUiKit.PushButton(row.transform, "Xoá", () => Delete(file), 110f, 44f, 18f);
            }
        }

        void Delete(string file)
        {
            try { File.Delete(file); }
            catch (Exception e) { BattleUiKit.SetText(statusLabel, "Không xoá được: " + e.Message); return; }
            BattleUiKit.SetText(statusLabel, "Đã xoá bản lưu");
            Refresh();
        }
    }
}
