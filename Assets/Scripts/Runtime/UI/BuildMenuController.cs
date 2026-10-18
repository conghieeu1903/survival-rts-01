using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SurvivalRts
{
    [RequireComponent(typeof(UIDocument))]
    public class BuildMenuController : MonoBehaviour
    {
        const string ItemClass = "build-card";
        const string SelectedClass = "build-card--selected";
        const string DisabledClass = "build-card--disabled";
        const string CostClass = "build-card-cost";
        const string CostLackClass = "build-card-cost--lack";
        const string TabClass = "build-tab";
        const string ActiveTabClass = "build-tab--active";
        const string DemolishTabClass = "build-tab--demolish";
        const string ActiveDemolishTabClass = "build-tab--demolish-active";
        const string HiddenClass = "build-hidden";

        static readonly Dictionary<BuildingCategory, string> CategoryNames = new Dictionary<BuildingCategory, string>
        {
            { BuildingCategory.Economy, "Kinh tế" },
            { BuildingCategory.Military, "Quân sự" },
            { BuildingCategory.Defense, "Phòng thủ" },
            { BuildingCategory.Test, "Test" }
        };

        [Header("Tham chiếu")]
        [Tooltip("Placer cung cấp danh sách công trình và nhận lệnh chọn từ menu")]
        [SerializeField] BuildingPlacer placer;

        readonly List<Button> items = new List<Button>();
        readonly List<Label> itemCosts = new List<Label>();
        readonly List<BuildingData> itemData = new List<BuildingData>();
        readonly Dictionary<BuildingCategory, Button> tabs = new Dictionary<BuildingCategory, Button>();

        VisualElement list;
        Label emptyLabel;
        VisualElement placingBar;
        Label placingLabel;
        Button rotateButton;
        Button confirmDemolishButton;
        Button cancelButton;
        Button demolishTabButton;
        Label popLabel;
        Label popValueLabel;
        Label woodValueLabel;
        Label stoneValueLabel;
        Label ironValueLabel;
        Label foodValueLabel;
        Label goldValueLabel;
        PopulationManager subscribedPop;

        void OnEnable()
        {
            if (GetComponent<MinimapController>() == null)
            {
                gameObject.AddComponent<MinimapController>();
            }

            var root = GetComponent<UIDocument>().rootVisualElement;
            list = root.Q<VisualElement>("buildList");
            emptyLabel = root.Q<Label>("emptyLabel");
            placingBar = root.Q<VisualElement>("placingBar");
            placingLabel = root.Q<Label>("placingLabel");
            rotateButton = root.Q<Button>("rotateButton");
            confirmDemolishButton = root.Q<Button>("confirmDemolishButton");
            cancelButton = root.Q<Button>("cancelButton");
            popLabel = root.Q<Label>("popLabel");
            popValueLabel = root.Q<Label>("popValueLabel");
            woodValueLabel = root.Q<Label>("woodValueLabel");
            stoneValueLabel = root.Q<Label>("stoneValueLabel");
            ironValueLabel = root.Q<Label>("ironValueLabel");
            foodValueLabel = root.Q<Label>("foodValueLabel");
            goldValueLabel = root.Q<Label>("goldValueLabel");

            if (rotateButton != null) rotateButton.clicked += placer.Rotate;
            if (confirmDemolishButton != null) confirmDemolishButton.clicked += OnConfirmDemolishClicked;
            if (cancelButton != null) cancelButton.clicked += OnCancelClicked;
            var panel = root.Q<VisualElement>("buildPanel");

            panel.RegisterCallback<PointerEnterEvent>(_ => UiPointerState.IsOverUI = true);
            panel.RegisterCallback<PointerLeaveEvent>(_ => UiPointerState.IsOverUI = false);

            BuildTabs(root.Q<VisualElement>("tabRow"));
            RebuildList(placer.Category);

            placer.CurrentChanged += Refresh;
            placer.CategoryChanged += RebuildList;
            placer.DemolishStateChanged += RefreshDemolishState;
            RefreshPopulation();
        }

        void OnDisable()
        {
            if (rotateButton != null && placer != null) rotateButton.clicked -= placer.Rotate;
            if (confirmDemolishButton != null) confirmDemolishButton.clicked -= OnConfirmDemolishClicked;
            if (cancelButton != null) cancelButton.clicked -= OnCancelClicked;
            if (placer != null)
            {
                placer.CurrentChanged -= Refresh;
                placer.CategoryChanged -= RebuildList;
                placer.DemolishStateChanged -= RefreshDemolishState;
            }
            if (subscribedPop != null)
            {
                subscribedPop.Changed -= RefreshPopulation;
                subscribedPop = null;
            }
            UiPointerState.IsOverUI = false;
        }

        void OnConfirmDemolishClicked()
        {
            AudioDirector.Post(SoundId.UiClick, Vector3.zero);
            if (placer != null) placer.ConfirmDemolish();
        }

        void OnCancelClicked()
        {
            AudioDirector.Post(SoundId.UiClick, Vector3.zero);
            if (placer != null) placer.Cancel();
        }

        void Update()
        {
            if (subscribedPop == null && PopulationManager.Instance != null)
            {
                subscribedPop = PopulationManager.Instance;
                subscribedPop.Changed += RefreshPopulation;
                RefreshPopulation();
            }
        }

        void RefreshPopulation()
        {
            var pop = PopulationManager.Instance;
            if (popValueLabel != null)
                popValueLabel.text = pop != null ? $"{pop.FreePopulation} / {pop.TotalPopulation}" : "0 / 0";
            if (woodValueLabel != null) woodValueLabel.text = pop != null ? pop.Wood.ToString() : "0";
            if (stoneValueLabel != null) stoneValueLabel.text = pop != null ? pop.Stone.ToString() : "0";
            if (ironValueLabel != null) ironValueLabel.text = pop != null ? pop.Iron.ToString() : "0";
            if (foodValueLabel != null) foodValueLabel.text = pop != null ? pop.Food.ToString() : "0";
            if (goldValueLabel != null) goldValueLabel.text = pop != null ? pop.Gold.ToString() : "0";

            RefreshAffordability();

            if (popLabel == null) return;
            if (pop == null)
            {
                popLabel.text = "";
                return;
            }
            popLabel.text = $"Dân rỗi: {pop.FreePopulation}/{pop.TotalPopulation}  ·  Thợ xây: {pop.BuilderCount}";
        }

        void RefreshAffordability()
        {
            var pop = PopulationManager.Instance;
            for (int i = 0; i < items.Count; i++)
            {
                bool canAfford = pop == null || pop.CanAfford(itemData[i]);
                items[i].EnableInClassList(DisabledClass, !canAfford);
                itemCosts[i].EnableInClassList(CostLackClass, !canAfford);
            }
        }

        void BuildTabs(VisualElement tabRow)
        {
            tabRow.Clear();
            tabs.Clear();
            foreach (BuildingCategory category in System.Enum.GetValues(typeof(BuildingCategory)))
            {
                var value = category;
                var tab = new Button(() =>
                {
                    AudioDirector.Post(SoundId.UiClick, Vector3.zero);
                    placer.SetCategory(value);
                });
                tab.AddToClassList(TabClass);

                var title = new Label(CategoryNames.TryGetValue(value, out var name) ? name : value.ToString())
                {
                    pickingMode = PickingMode.Ignore
                };
                var count = new Label(placer.CountIn(value).ToString()) { pickingMode = PickingMode.Ignore };
                count.AddToClassList("build-tab-count");

                tab.Add(title);
                tab.Add(count);
                tabRow.Add(tab);
                tabs[value] = tab;
            }

            // Nút công cụ Tháo dỡ công trình nằm ở góc phải hàng tab.
            demolishTabButton = new Button(() =>
            {
                AudioDirector.Post(SoundId.UiClick, Vector3.zero);
                placer.ToggleDemolishMode();
            });
            demolishTabButton.name = "demolishModeButton";
            demolishTabButton.text = "Tháo dỡ (X)";
            demolishTabButton.AddToClassList(TabClass);
            demolishTabButton.AddToClassList(DemolishTabClass);
            tabRow.Add(demolishTabButton);
        }

        void RebuildList(BuildingCategory category)
        {
            foreach (var pair in tabs) pair.Value.EnableInClassList(ActiveTabClass, pair.Key == category);

            list.Clear();
            items.Clear();
            itemCosts.Clear();
            itemData.Clear();
            var buildings = placer.Visible;
            foreach (var data in buildings)
            {
                var item = new Button(() =>
                {
                    AudioDirector.Post(SoundId.UiClick, Vector3.zero);
                    placer.Toggle(data);
                });
                item.AddToClassList(ItemClass);

                var preview = new VisualElement { pickingMode = PickingMode.Ignore };
                preview.AddToClassList("build-card-preview");
                var footprint = new VisualElement { pickingMode = PickingMode.Ignore };
                footprint.AddToClassList("build-card-footprint");
                footprint.AddToClassList("fp-w-" + Mathf.Clamp(data.footprint.x, 1, 6));
                footprint.AddToClassList("fp-d-" + Mathf.Clamp(data.footprint.y, 1, 6));
                preview.Add(footprint);

                var title = new Label(data.displayName) { pickingMode = PickingMode.Ignore };
                title.AddToClassList("build-card-name");
                var size = new Label(data.footprint.x + "x" + data.footprint.y) { pickingMode = PickingMode.Ignore };
                size.AddToClassList("build-card-size");
                var cost = new Label(data.FormatCost()) { pickingMode = PickingMode.Ignore };
                cost.AddToClassList(CostClass);

                item.Add(preview);
                item.Add(title);
                item.Add(size);
                item.Add(cost);
                list.Add(item);
                items.Add(item);
                itemCosts.Add(cost);
                itemData.Add(data);
            }

            emptyLabel.EnableInClassList(HiddenClass, items.Count > 0);
            list.EnableInClassList(HiddenClass, items.Count == 0);
            Refresh(placer.Current);
            RefreshAffordability();
        }

        void RefreshDemolishState()
        {
            Refresh(placer != null ? placer.Current : null);
        }

        void Refresh(BuildingData current)
        {
            for (int i = 0; i < items.Count; i++)
                items[i].EnableInClassList(SelectedClass, itemData[i] == current);

            bool isDemolishing = placer != null && placer.IsDemolishing;
            if (demolishTabButton != null)
            {
                demolishTabButton.EnableInClassList(ActiveDemolishTabClass, isDemolishing);
            }

            if (isDemolishing)
            {
                placingBar.EnableInClassList(HiddenClass, false);
                if (rotateButton != null) rotateButton.EnableInClassList(HiddenClass, true);

                var pending = placer.PendingDemolishTarget;
                if (pending != null && DemolishSite.CanDemolish(pending))
                {
                    var occ = pending.GetComponent<GridOccupant>();
                    string name = occ != null ? occ.DisplayName : pending.name;
                    float time = occ != null ? occ.DemolishTime : 2.5f;
                    string refund = occ != null && occ.buildingData != null ? occ.buildingData.FormatCost() : "Miễn phí";
                    placingLabel.text = $"Xác nhận phá: {name}  ·  Hoàn 100% ({refund})  ·  Thời gian: {time:0.#}s";
                    if (confirmDemolishButton != null) confirmDemolishButton.EnableInClassList(HiddenClass, false);
                    if (cancelButton != null) cancelButton.text = "Hủy";
                }
                else
                {
                    placingLabel.text = "Chế độ tháo dỡ: Click vào công trình cần phá (Chuột phải / Esc để thoát)";
                    if (confirmDemolishButton != null) confirmDemolishButton.EnableInClassList(HiddenClass, true);
                    if (cancelButton != null) cancelButton.text = "Thoát";
                }
                return;
            }

            if (rotateButton != null) rotateButton.EnableInClassList(HiddenClass, false);
            if (confirmDemolishButton != null) confirmDemolishButton.EnableInClassList(HiddenClass, true);
            if (cancelButton != null) cancelButton.text = "Hủy";

            placingBar.EnableInClassList(HiddenClass, current == null);
            if (current != null)
                placingLabel.text = $"Đang đặt: {current.displayName} ({current.FormatCost()})";
        }
    }
}
