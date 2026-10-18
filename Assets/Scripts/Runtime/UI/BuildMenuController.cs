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
        const string TabClass = "build-tab";
        const string ActiveTabClass = "build-tab--active";
        const string HiddenClass = "build-hidden";

        static readonly Dictionary<BuildingCategory, string> CategoryNames = new Dictionary<BuildingCategory, string>
        {
            { BuildingCategory.Economy, "Kinh tế" },
            { BuildingCategory.Military, "Quân sự" },
            { BuildingCategory.Defense, "Phòng thủ" }
        };

        [Header("Tham chiếu")]
        [Tooltip("Placer cung cấp danh sách công trình và nhận lệnh chọn từ menu")]
        [SerializeField] BuildingPlacer placer;

        readonly List<Button> items = new List<Button>();
        readonly List<BuildingData> itemData = new List<BuildingData>();
        readonly Dictionary<BuildingCategory, Button> tabs = new Dictionary<BuildingCategory, Button>();

        VisualElement list;
        Label emptyLabel;
        VisualElement placingBar;
        Label placingLabel;

        void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            list = root.Q<VisualElement>("buildList");
            emptyLabel = root.Q<Label>("emptyLabel");
            placingBar = root.Q<VisualElement>("placingBar");
            placingLabel = root.Q<Label>("placingLabel");
            root.Q<Button>("rotateButton").clicked += placer.Rotate;
            root.Q<Button>("cancelButton").clicked += placer.Cancel;
            var panel = root.Q<VisualElement>("buildPanel");

            panel.RegisterCallback<PointerEnterEvent>(_ => UiPointerState.IsOverUI = true);
            panel.RegisterCallback<PointerLeaveEvent>(_ => UiPointerState.IsOverUI = false);

            BuildTabs(root.Q<VisualElement>("tabRow"));
            RebuildList(placer.Category);

            placer.CurrentChanged += Refresh;
            placer.CategoryChanged += RebuildList;
        }

        void OnDisable()
        {
            if (placer != null)
            {
                placer.CurrentChanged -= Refresh;
                placer.CategoryChanged -= RebuildList;
            }
            UiPointerState.IsOverUI = false;
        }

        void BuildTabs(VisualElement tabRow)
        {
            tabRow.Clear();
            tabs.Clear();
            foreach (BuildingCategory category in System.Enum.GetValues(typeof(BuildingCategory)))
            {
                var value = category;
                var tab = new Button(() => placer.SetCategory(value));
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
        }

        void RebuildList(BuildingCategory category)
        {
            foreach (var pair in tabs) pair.Value.EnableInClassList(ActiveTabClass, pair.Key == category);

            list.Clear();
            items.Clear();
            itemData.Clear();
            var buildings = placer.Visible;
            foreach (var data in buildings)
            {
                var item = new Button(() => placer.Toggle(data));
                item.AddToClassList(ItemClass);

                var preview = new VisualElement { pickingMode = PickingMode.Ignore };
                preview.AddToClassList("build-card-preview");
                var footprint = new VisualElement { pickingMode = PickingMode.Ignore };
                footprint.AddToClassList("build-card-footprint");
                footprint.AddToClassList("fp-w-" + Mathf.Clamp(data.footprint.x, 1, 6));
                footprint.AddToClassList("fp-d-" + Mathf.Clamp(data.footprint.y, 1, 6));
                preview.Add(footprint);

                var title = new Label(data.displayName);
                title.AddToClassList("build-card-name");
                var size = new Label(data.footprint.x + "x" + data.footprint.y);
                size.AddToClassList("build-card-size");

                item.Add(preview);
                item.Add(title);
                item.Add(size);
                list.Add(item);
                items.Add(item);
                itemData.Add(data);
            }

            emptyLabel.EnableInClassList(HiddenClass, items.Count > 0);
            list.EnableInClassList(HiddenClass, items.Count == 0);
            Refresh(placer.Current);
        }

        void Refresh(BuildingData current)
        {
            for (int i = 0; i < items.Count; i++)
                items[i].EnableInClassList(SelectedClass, itemData[i] == current);

            placingBar.EnableInClassList(HiddenClass, current == null);
            if (current != null) placingLabel.text = "Đang đặt: " + current.displayName;
        }
    }
}
