using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SurvivalRts
{
    // Màn hình kết quả: đội thắng, bảng thống kê bốn đội, chơi lại / về phòng chờ / thoát.
    public class BattleResultScreen
    {
        const int Columns = 4;

        readonly BattleUiController owner;
        readonly BattleSimulation simulation;
        RectTransform root;
        TMP_Text titleLabel;
        TMP_Text hintLabel;
        Button restartButton;
        Button lobbyButton;
        readonly TMP_Text[,] cells = new TMP_Text[BattleSimulation.TeamCount, Columns];
        readonly Image[] swatches = new Image[BattleSimulation.TeamCount];

        public GameObject Root => root.gameObject;

        public BattleResultScreen(BattleUiController owner, BattleSimulation simulation)
        {
            this.owner = owner;
            this.simulation = simulation;
        }

        public void Build(Transform parent)
        {
            var overlay = BattleUiKit.Background(parent, "Result", BattleUiKit.BackdropColor);
            root = overlay.rectTransform;

            var panel = BattleUiKit.Panel(root, "Panel", BattleUiKit.PanelColor);
            BattleUiKit.Anchor(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(700f, 0f), Vector2.zero);
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            BattleUiKit.Vertical(panel.rectTransform, 12f, 24);

            var heading = BattleUiKit.Heading(panel.transform, "", 32f);
            titleLabel = heading.GetComponentInChildren<TMP_Text>();

            BuildRow(panel.transform, null, new[] { "Đội", "Còn sống", "Đã hạ", "Đã mất" });
            for (int t = 0; t < BattleSimulation.TeamCount; t++) BuildRow(panel.transform, t, new string[Columns]);

            hintLabel = BattleUiKit.Label(panel.transform, "", 18f, TextAlignmentOptions.Center, BattleUiKit.DimTextColor);
            BattleUiKit.Size(hintLabel, -1f, 28f);

            BattleUiKit.Separator(panel.transform);
            var actions = BattleUiKit.Node(panel.transform, "Actions");
            BattleUiKit.Size(actions, -1f, 52f);
            BattleUiKit.Horizontal(actions, 12f, 0, TextAnchor.MiddleRight);
            BattleUiKit.PushButton(actions, "Thoát", OnLeave, 130f, 52f);
            lobbyButton = BattleUiKit.PushButton(actions, "Về phòng chờ", OnLobby, 190f, 52f);
            restartButton = BattleUiKit.PushButton(actions, "Chơi lại", OnRestart, 150f, 52f, 20f, true);
        }

        // Dòng tiêu đề cột (team = null) hoặc dòng của một đội, đội có ô màu ở đầu dòng.
        void BuildRow(Transform parent, int? team, string[] texts)
        {
            var row = BattleUiKit.Panel(parent, team.HasValue ? "Team" + team.Value : "Header",
                team.HasValue ? BattleUiKit.RowColor : BattleUiKit.PanelColor);
            BattleUiKit.Size(row, -1f, 40f);
            BattleUiKit.Horizontal(row.rectTransform, 10f, 6);

            if (team.HasValue)
            {
                var swatch = BattleUiKit.Panel(row.transform, "Swatch", Color.white);
                BattleUiKit.Size(swatch, 24f, 24f);
                swatches[team.Value] = swatch;
            }
            else
            {
                BattleUiKit.Size(BattleUiKit.Node(row.transform, "SwatchSpace"), 24f, 24f);
            }

            for (int c = 0; c < Columns; c++)
            {
                var label = BattleUiKit.Label(row.transform, texts[c] ?? "", 20f,
                    c == 0 ? TextAlignmentOptions.Left : TextAlignmentOptions.Center,
                    team.HasValue ? BattleUiKit.InkColor : BattleUiKit.DimTextColor);
                BattleUiKit.Size(label, -1f, -1f, 1f);
                if (team.HasValue) cells[team.Value, c] = label;
            }
        }

        void OnRestart()
        {
            var net = LockstepNetwork.Instance;
            if (net != null && net.IsHost) net.RestartMatch();
        }

        void OnLobby()
        {
            var net = LockstepNetwork.Instance;
            if (net != null && net.IsHost) net.ReturnToLobby();
        }

        void OnLeave()
        {
            owner.LeaveRoom();
        }

        public void Refresh(LockstepNetwork net, int winner)
        {
            int local = simulation.LocalTeam;
            string title = winner < 0 ? "Trận kết thúc" : winner == local ? "Bạn thắng!" : $"Đội {winner + 1} thắng";
            BattleUiKit.SetText(titleLabel, title);

            var colors = simulation.Data != null ? simulation.Data.teamColors : null;
            for (int t = 0; t < BattleSimulation.TeamCount; t++)
            {
                BattleUiKit.SetText(cells[t, 0], $"Đội {t + 1}" + (t == local ? " (bạn)" : ""));
                BattleUiKit.SetText(cells[t, 1], simulation.TeamAlive(t).ToString());
                BattleUiKit.SetText(cells[t, 2], simulation.TeamKills(t).ToString());
                BattleUiKit.SetText(cells[t, 3], simulation.TeamLosses(t).ToString());
                if (colors != null && t < colors.Length) swatches[t].color = colors[t];
            }

            BattleUiKit.SetText(hintLabel, net.IsHost ? "" : "Chờ chủ phòng chọn...");
            restartButton.interactable = net.IsHost;
            lobbyButton.interactable = net.IsHost;
        }
    }
}
