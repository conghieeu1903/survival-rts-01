using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SurvivalRts
{
    // Phòng chờ: bốn ô đội (người chơi / máy), địa chỉ LAN của host, nút bắt đầu và rời phòng.
    public class BattleLobbyScreen
    {
        static readonly Color[] DefaultTeamColors =
        {
            new Color(0.35f, 0.55f, 1f, 1f),
            new Color(1f, 0.4f, 0.35f, 1f),
            new Color(0.4f, 0.9f, 0.45f, 1f),
            new Color(1f, 0.85f, 0.3f, 1f)
        };

        readonly Action onLeave;
        readonly Color backdropColor;
        RectTransform root;
        readonly TMP_Text[] slotLabels = new TMP_Text[BattleSimulation.TeamCount];
        readonly Image[] slotSwatches = new Image[BattleSimulation.TeamCount];
        TMP_Text addressLabel;
        TMP_Text infoLabel;
        Button startButton;

        public GameObject Root => root.gameObject;

        public BattleLobbyScreen(Action onLeave, Color? backdrop = null)
        {
            this.onLeave = onLeave;
            backdropColor = backdrop ?? BattleUiKit.BackdropColor;
        }

        public BattleLobbyScreen(BattleUiController owner)
            : this(owner != null ? (Action)owner.LeaveRoom : null, BattleUiKit.BackdropColor)
        {
        }

        public void Build(Transform parent)
        {
            var overlay = BattleUiKit.Background(parent, "Lobby", backdropColor);
            root = overlay.rectTransform;

            var panel = BattleUiKit.Panel(root, "Panel", BattleUiKit.PanelColor);
            BattleUiKit.Anchor(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(640f, 0f), Vector2.zero);
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            BattleUiKit.Vertical(panel.rectTransform, 14f, 24);

            BattleUiKit.Heading(panel.transform, "Phòng chờ", 30f);
            addressLabel = BattleUiKit.Label(panel.transform, "", 18f, TextAlignmentOptions.Left, BattleUiKit.DimTextColor);
            BattleUiKit.Size(addressLabel, -1f, 28f);

            for (int i = 0; i < BattleSimulation.TeamCount; i++)
            {
                var row = BattleUiKit.Panel(panel.transform, "Slot" + i, BattleUiKit.RowColor);
                BattleUiKit.Size(row, -1f, 52f);
                BattleUiKit.Horizontal(row.rectTransform, 14f, 10);

                var swatch = BattleUiKit.Panel(row.transform, "Swatch", Color.white);
                BattleUiKit.Size(swatch, 30f, 30f);
                slotSwatches[i] = swatch;
                slotLabels[i] = BattleUiKit.Label(row.transform, "", 20f);
                BattleUiKit.Size(slotLabels[i], -1f, -1f, 1f);
            }

            infoLabel = BattleUiKit.Label(panel.transform, "", 18f, TextAlignmentOptions.Left, BattleUiKit.DimTextColor);
            BattleUiKit.Size(infoLabel, -1f, 30f);

            BattleUiKit.Separator(panel.transform);
            var actions = BattleUiKit.Node(panel.transform, "Actions");
            BattleUiKit.Size(actions, -1f, 52f);
            BattleUiKit.Horizontal(actions, 12f, 0, TextAnchor.MiddleRight);
            BattleUiKit.PushButton(actions, "Rời phòng", OnLeave, 170f, 52f);
            startButton = BattleUiKit.PushButton(actions, "Bắt đầu", OnStart, 170f, 52f, 20f, true);
        }

        void OnStart()
        {
            var net = LockstepNetwork.Instance;
            if (net != null && net.IsHost) net.StartMatch();
        }

        void OnLeave()
        {
            onLeave?.Invoke();
        }

        public void Show()
        {
            root.gameObject.SetActive(true);
            var net = LockstepNetwork.Instance;
            if (net != null) Refresh(net, null);
        }

        public void Hide()
        {
            root.gameObject.SetActive(false);
        }

        public void Refresh(LockstepNetwork net, BattleData data = null)
        {
            if (net == null) return;
            var colors = data != null && data.teamColors != null && data.teamColors.Length > 0 ? data.teamColors : DefaultTeamColors;
            for (int i = 0; i < BattleSimulation.TeamCount; i++)
            {
                string text;
                if (i < net.PlayerCount) text = "Người chơi" + (i == net.LocalTeam ? " (bạn)" : "");
                else text = "Máy";
                BattleUiKit.SetText(slotLabels[i], $"Đội {i + 1}: {text}");
                slotSwatches[i].color = i < colors.Length ? colors[i] : Color.gray;
            }

            if (net.IsHost)
            {
                BattleUiKit.SetText(addressLabel, "Địa chỉ phòng: " + string.Join(", ", LockstepNetwork.GetLocalAddresses()));
                BattleUiKit.SetText(infoLabel, "Đội trống sẽ do máy điều khiển");
            }
            else
            {
                BattleUiKit.SetText(addressLabel, "Đã vào phòng của chủ phòng");
                BattleUiKit.SetText(infoLabel, "Đang chờ chủ phòng bắt đầu...");
            }
            startButton.gameObject.SetActive(net.IsHost);
        }
    }
}
