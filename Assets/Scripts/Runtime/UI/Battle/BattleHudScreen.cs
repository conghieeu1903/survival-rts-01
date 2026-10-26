using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SurvivalRts
{
    // HUD trong trận: lượt và độ trễ, vàng, quân số bốn đội, nút huấn luyện, bảng tiểu đội, thông báo.
    public class BattleHudScreen
    {
        const float SquadRefreshSeconds = 0.25f;

        readonly BattleUiController owner;
        readonly BattleSimulation simulation;
        readonly BattleInput input;
        RectTransform root;
        TMP_Text turnLabel;
        TMP_Text waitLabel;
        TMP_Text goldLabel;
        readonly TMP_Text[] teamLabels = new TMP_Text[BattleSimulation.TeamCount];
        readonly Image[] teamSwatches = new Image[BattleSimulation.TeamCount];
        Button[] trainButtons;
        TMP_Text[] trainLabels;
        Button[] selectTypeButtons;
        TMP_Text[] selectTypeLabels;
        TMP_Text selectedLabel;
        GameObject noticeBox;
        TMP_Text noticeLabel;
        float nextSquadRefresh;

        public GameObject Root => root.gameObject;

        public BattleHudScreen(BattleUiController owner, BattleSimulation simulation, BattleInput input)
        {
            this.owner = owner;
            this.simulation = simulation;
            this.input = input;
        }

        public void Build(Transform parent)
        {
            root = BattleUiKit.Node(parent, "Hud");
            BuildTopBar();
            BuildTeamPanel();
            BuildTrainBar();
            BuildSquadPanel();
            BuildNotice();
        }

        void BuildTopBar()
        {
            var bar = BattleUiKit.Panel(root, "TopBar", BattleUiKit.PanelColor);
            BattleUiKit.StretchX(bar.rectTransform, 0f, 40f, true);
            BattleUiKit.Horizontal(bar.rectTransform, 12f, 8);

            turnLabel = BattleUiKit.Label(bar.transform, "", 18f);
            BattleUiKit.Size(turnLabel, -1f, -1f, 1f);
            waitLabel = BattleUiKit.Label(bar.transform, "", 18f, TextAlignmentOptions.Center);
            BattleUiKit.Size(waitLabel, -1f, -1f, 1f);
            goldLabel = BattleUiKit.Label(bar.transform, "", 20f, TextAlignmentOptions.Right);
            BattleUiKit.Size(goldLabel, -1f, -1f, 1f);
        }

        // Bảng quân số bốn đội, mỗi dòng tô màu đội.
        void BuildTeamPanel()
        {
            var panel = BattleUiKit.Panel(root, "Teams", BattleUiKit.PanelColor);
            BattleUiKit.Anchor(panel.rectTransform, new Vector2(0f, 1f), new Vector2(240f, 0f), new Vector2(8f, -48f));
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            BattleUiKit.Vertical(panel.rectTransform, 4f, 8);
            for (int t = 0; t < BattleSimulation.TeamCount; t++)
            {
                var row = BattleUiKit.Node(panel.transform, "Team" + t);
                BattleUiKit.Size(row, -1f, 28f);
                BattleUiKit.Horizontal(row, 8f, 0);
                teamSwatches[t] = BattleUiKit.Panel(row, "Swatch", Color.white);
                BattleUiKit.Size(teamSwatches[t], 20f, 20f);
                teamLabels[t] = BattleUiKit.Label(row, "", 18f);
                BattleUiKit.Size(teamLabels[t], -1f, -1f, 1f);
            }
        }

        // Nút huấn luyện thêm tiểu đội cho từng loại lính.
        void BuildTrainBar()
        {
            var types = simulation.Data != null ? simulation.Data.unitTypes : new BattleUnitType[0];
            var bar = BattleUiKit.Panel(root, "TrainBar", BattleUiKit.PanelColor);
            BattleUiKit.Anchor(bar.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(0f, 8f));
            var fitter = bar.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            BattleUiKit.Horizontal(bar.rectTransform, 8f, 8).childForceExpandHeight = false;

            var title = BattleUiKit.Label(bar.transform, "Huấn luyện", 18f, TextAlignmentOptions.Center, BattleUiKit.DimTextColor);
            BattleUiKit.Size(title, 110f, 60f);

            trainButtons = new Button[types.Length];
            trainLabels = new TMP_Text[types.Length];
            for (int i = 0; i < types.Length; i++)
            {
                int index = i;
                trainButtons[i] = BattleUiKit.PushButton(bar.transform, "", () => OnTrain(index), 190f, 60f, 16f);
                trainLabels[i] = BattleUiKit.ButtonLabel(trainButtons[i]);
            }
        }

        void OnTrain(int type)
        {
            var net = LockstepNetwork.Instance;
            if (net == null || !net.InMatch) return;
            net.SendCommand(new LockstepCommand { Type = BattleCommand.Train, Param = type });
        }

        // Bảng điều khiển tiểu đội gọn bên phải: số đang chọn và chọn nhanh theo loại.
        void BuildSquadPanel()
        {
            var types = simulation.Data != null ? simulation.Data.unitTypes : new BattleUnitType[0];
            var panel = BattleUiKit.Panel(root, "Squads", BattleUiKit.PanelColor);
            BattleUiKit.Anchor(panel.rectTransform, new Vector2(1f, 0.5f), new Vector2(260f, 0f), new Vector2(-8f, 0f));
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            BattleUiKit.Vertical(panel.rectTransform, 6f, 8);

            BattleUiKit.Size(BattleUiKit.Label(panel.transform, "Tiểu đội", 20f), -1f, 28f);
            selectedLabel = BattleUiKit.Label(panel.transform, "", 18f);
            BattleUiKit.Size(selectedLabel, -1f, 26f);
            BattleUiKit.PushButton(panel.transform, "Chọn cả quân (Q)", () => { if (input != null) input.SelectAll(); }, -1f, 36f, 16f);

            selectTypeButtons = new Button[types.Length];
            selectTypeLabels = new TMP_Text[types.Length];
            for (int i = 0; i < types.Length; i++)
            {
                int index = i;
                selectTypeButtons[i] = BattleUiKit.PushButton(panel.transform, "", () => { if (input != null) input.SelectType(index); }, -1f, 36f, 15f);
                selectTypeLabels[i] = BattleUiKit.ButtonLabel(selectTypeButtons[i]);
            }

            var hint = BattleUiKit.Label(panel.transform,
                "Kéo chuột: chọn khung\nShift: thêm vào\nNhấp đúp: cùng loại\nCtrl+1..9: gán nhóm\n1..9: gọi nhóm\nChuột phải: di chuyển",
                14f, TextAlignmentOptions.Left, BattleUiKit.DimTextColor);
            BattleUiKit.Size(hint, -1f, 100f);
        }

        // Thông báo desync hoặc mất kết nối ngay dưới thanh trên.
        void BuildNotice()
        {
            var box = BattleUiKit.Panel(root, "Notice", BattleUiKit.RowColor);
            BattleUiKit.Anchor(box.rectTransform, new Vector2(0.5f, 1f), new Vector2(640f, 56f), new Vector2(0f, -52f));
            BattleUiKit.Horizontal(box.rectTransform, 12f, 8);
            noticeLabel = BattleUiKit.Label(box.transform, "", 18f, TextAlignmentOptions.Left);
            BattleUiKit.Size(noticeLabel, -1f, -1f, 1f);
            BattleUiKit.PushButton(box.transform, "Đóng", () => owner.SetNotice(""), 90f, 40f, 16f);
            noticeBox = box.gameObject;
            noticeBox.SetActive(false);
        }

        public void Refresh(LockstepNetwork net, string notice)
        {
            int team = simulation.LocalTeam;
            BattleUiKit.SetText(turnLabel, $"Lượt {net.CurrentTurn}  |  Trễ {net.LagTurns} lượt");
            BattleUiKit.SetText(waitLabel, net.IsWaitingForPlayers ? "Đang chờ người chơi..." : "");
            BattleUiKit.SetText(goldLabel, team >= 0 ? $"Vàng: {simulation.TeamGold(team)}" : "");

            var colors = simulation.Data != null ? simulation.Data.teamColors : null;
            for (int t = 0; t < BattleSimulation.TeamCount; t++)
            {
                string tag = t == team ? " (bạn)" : simulation.IsAiTeam(t) ? " (máy)" : "";
                BattleUiKit.SetText(teamLabels[t], $"Đội {t + 1}{tag}: {simulation.TeamAlive(t)} lính");
                if (colors != null && t < colors.Length) teamSwatches[t].color = colors[t];
            }

            RefreshTrainButtons(team);

            if (Time.unscaledTime >= nextSquadRefresh)
            {
                nextSquadRefresh = Time.unscaledTime + SquadRefreshSeconds;
                RefreshSquadPanel(team);
            }

            bool hasNotice = !string.IsNullOrEmpty(notice);
            if (noticeBox.activeSelf != hasNotice) noticeBox.SetActive(hasNotice);
            if (hasNotice) BattleUiKit.SetText(noticeLabel, notice);
        }

        // Hiện giá và tắt nút khi thiếu vàng hoặc trận đã kết thúc.
        void RefreshTrainButtons(int team)
        {
            var types = simulation.Data != null ? simulation.Data.unitTypes : null;
            if (types == null || team < 0) return;
            int gold = simulation.TeamGold(team);
            bool ended = simulation.WinnerTeam >= 0;
            for (int i = 0; i < trainButtons.Length; i++)
            {
                var type = types[i];
                BattleUiKit.SetText(trainLabels[i], $"{type.displayName}\n{type.cost} vàng · {type.trainSeconds:0.#}s");
                trainButtons[i].interactable = !ended && gold >= type.cost;
            }
        }

        void RefreshSquadPanel(int team)
        {
            var types = simulation.Data != null ? simulation.Data.unitTypes : null;
            if (types == null || team < 0) return;

            var squadCount = new int[types.Length];
            var unitCount = new int[types.Length];
            for (int s = 0; s < simulation.SquadsPerTeam; s++)
            {
                if (!simulation.TryGetSquad(team, s, out _, out int alive)) continue;
                int type = simulation.SquadType(team, s);
                squadCount[type]++;
                unitCount[type] += alive;
            }

            BattleUiKit.SetText(selectedLabel, $"Đang chọn: {(input != null ? input.SelectedCount : 0)} tiểu đội");
            for (int i = 0; i < types.Length; i++)
            {
                BattleUiKit.SetText(selectTypeLabels[i], $"{types[i].displayName}: {squadCount[i]} đội · {unitCount[i]} lính");
                selectTypeButtons[i].interactable = squadCount[i] > 0;
            }
        }
    }
}
