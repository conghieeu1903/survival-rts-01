using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SurvivalRts
{
    // Điều khiển giao diện xây dựng, thanh hiển thị tài nguyên và chế độ tháo dỡ bằng uGUI và TextMeshPro.
    public class BuildMenuController : MonoBehaviour
    {
        static readonly Color TabNormalBg = new Color(0.22f, 0.22f, 0.22f, 1f);
        static readonly Color TabActiveBg = new Color(0.88f, 0.88f, 0.88f, 1f);
        static readonly Color TabNormalText = new Color(0.86f, 0.86f, 0.86f, 1f);
        static readonly Color TabActiveText = new Color(0.08f, 0.08f, 0.08f, 1f);

        static readonly Color CardNormalBg = new Color(0.22f, 0.22f, 0.22f, 1f);
        static readonly Color CardSelectedBg = new Color(0.38f, 0.38f, 0.38f, 1f);
        static readonly Color CardDisabledBg = new Color(0.16f, 0.16f, 0.16f, 0.92f);
        static readonly Color BorderNormal = new Color(0.48f, 0.48f, 0.48f, 1f);
        static readonly Color BorderSelected = new Color(0.95f, 0.95f, 0.95f, 1f);
        static readonly Color CostNormalText = new Color(0.92f, 0.92f, 0.92f, 1f);
        static readonly Color CostLackText = new Color(0.62f, 0.62f, 0.62f, 1f);

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

        [Header("Prefab thành phần uGUI")]
        [SerializeField] GameObject tabButtonPrefab;
        [SerializeField] GameObject buildCardPrefab;

        [Header("Phần tử giao diện")]
        [SerializeField] RectTransform buildPanel;
        [SerializeField] RectTransform tabRow;
        [SerializeField] RectTransform buildList;
        [SerializeField] GameObject emptyLabel;
        [SerializeField] GameObject placingBar;
        [SerializeField] TMP_Text placingLabel;
        [SerializeField] Button rotateButton;
        [SerializeField] Button confirmDemolishButton;
        [SerializeField] Button cancelButton;
        [SerializeField] TMP_Text popLabel;
        [SerializeField] TMP_Text popValueLabel;
        [SerializeField] TMP_Text woodValueLabel;
        [SerializeField] TMP_Text stoneValueLabel;
        [SerializeField] TMP_Text ironValueLabel;
        [SerializeField] TMP_Text foodValueLabel;
        [SerializeField] TMP_Text goldValueLabel;

        readonly List<Button> items = new List<Button>();
        readonly List<Image> itemBackgrounds = new List<Image>();
        readonly List<Outline> itemOutlines = new List<Outline>();
        readonly List<TMP_Text> itemCosts = new List<TMP_Text>();
        readonly List<CanvasGroup> itemGroups = new List<CanvasGroup>();
        readonly List<BuildingData> itemData = new List<BuildingData>();
        readonly Dictionary<BuildingCategory, Button> tabs = new Dictionary<BuildingCategory, Button>(); // Map từng nhóm công trình với nút tab tương ứng

        Button demolishTabButton;
        TMP_Text cancelButtonText;
        PopulationManager subscribedPop;

        // Liên kết sự kiện và khởi tạo danh sách tab, thẻ công trình khi bật menu.
        void OnEnable()
        {
            if (GetComponent<MinimapController>() == null)
            {
                gameObject.AddComponent<MinimapController>();
            }

            ResolveReferences();

            if (buildPanel != null && buildPanel.GetComponent<UiPointerBlocker>() == null)
            {
                buildPanel.gameObject.AddComponent<UiPointerBlocker>();
            }

            if (rotateButton != null && placer != null) rotateButton.onClick.AddListener(placer.Rotate);
            if (confirmDemolishButton != null) confirmDemolishButton.onClick.AddListener(OnConfirmDemolishClicked);
            if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelClicked);

            if (placer != null)
            {
                BuildTabs();
                RebuildList(placer.Category);
                placer.CurrentChanged += Refresh;
                placer.CategoryChanged += RebuildList;
                placer.DemolishStateChanged += RefreshDemolishState;
            }

            RefreshPopulation();
        }

        // Hủy đăng ký sự kiện khi tắt menu.
        void OnDisable()
        {
            if (rotateButton != null && placer != null) rotateButton.onClick.RemoveListener(placer.Rotate);
            if (confirmDemolishButton != null) confirmDemolishButton.onClick.RemoveListener(OnConfirmDemolishClicked);
            if (cancelButton != null) cancelButton.onClick.RemoveListener(OnCancelClicked);

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

        // Tự động tìm các đối tượng con theo tên nếu chưa được gán trong Inspector.
        void ResolveReferences()
        {
            if (placer == null) placer = FindFirstObjectByType<BuildingPlacer>();
            if (buildPanel == null) buildPanel = FindComponent<RectTransform>("buildPanel");
            if (tabRow == null) tabRow = FindComponent<RectTransform>("tabRow");
            if (buildList == null) buildList = FindComponent<RectTransform>("buildList");
            if (emptyLabel == null)
            {
                var t = FindChildRecursive(transform, "emptyLabel");
                if (t != null) emptyLabel = t.gameObject;
            }
            if (placingBar == null)
            {
                var t = FindChildRecursive(transform, "placingBar");
                if (t != null) placingBar = t.gameObject;
            }
            if (placingLabel == null) placingLabel = FindComponent<TMP_Text>("placingLabel");
            if (rotateButton == null) rotateButton = FindComponent<Button>("rotateButton");
            if (confirmDemolishButton == null) confirmDemolishButton = FindComponent<Button>("confirmDemolishButton");
            if (cancelButton == null) cancelButton = FindComponent<Button>("cancelButton");
            if (cancelButton != null && cancelButtonText == null)
                cancelButtonText = cancelButton.GetComponentInChildren<TMP_Text>(true);

            if (popLabel == null) popLabel = FindComponent<TMP_Text>("popLabel");
            if (popValueLabel == null) popValueLabel = FindComponent<TMP_Text>("popValueLabel");
            if (woodValueLabel == null) woodValueLabel = FindComponent<TMP_Text>("woodValueLabel");
            if (stoneValueLabel == null) stoneValueLabel = FindComponent<TMP_Text>("stoneValueLabel");
            if (ironValueLabel == null) ironValueLabel = FindComponent<TMP_Text>("ironValueLabel");
            if (foodValueLabel == null) foodValueLabel = FindComponent<TMP_Text>("foodValueLabel");
            if (goldValueLabel == null) goldValueLabel = FindComponent<TMP_Text>("goldValueLabel");
        }

        // Tìm component trên đối tượng con theo tên.
        T FindComponent<T>(string childName) where T : Component
        {
            var t = FindChildRecursive(transform, childName);
            return t != null ? t.GetComponent<T>() : null;
        }

        // Đệ quy tìm Transform con theo tên.
        static Transform FindChildRecursive(Transform parent, string targetName)
        {
            if (parent.name == targetName) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                var found = FindChildRecursive(parent.GetChild(i), targetName);
                if (found != null) return found;
            }
            return null;
        }

        // Xử lý khi người dùng bấm nút xác nhận tháo dỡ công trình.
        void OnConfirmDemolishClicked()
        {
            AudioDirector.Post(SoundId.UiClick, Vector3.zero);
            if (placer != null) placer.ConfirmDemolish();
        }

        // Xử lý khi người dùng bấm nút hủy đặt hoặc thoát tháo dỡ.
        void OnCancelClicked()
        {
            AudioDirector.Post(SoundId.UiClick, Vector3.zero);
            if (placer != null) placer.Cancel();
        }

        // Đảm bảo đăng ký sự kiện với PopulationManager khi đối tượng này sẵn sàng.
        void Update()
        {
            if (subscribedPop == null && PopulationManager.Instance != null)
            {
                subscribedPop = PopulationManager.Instance;
                subscribedPop.Changed += RefreshPopulation;
                RefreshPopulation();
            }
        }

        // Cập nhật toàn bộ số liệu dân số và tài nguyên trên HUD.
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

        // Đổi trạng thái hiển thị của các thẻ công trình tùy theo đủ hay thiếu tài nguyên.
        void RefreshAffordability()
        {
            var pop = PopulationManager.Instance;
            for (int i = 0; i < items.Count; i++)
            {
                bool canAfford = pop == null || pop.CanAfford(itemData[i]);
                if (itemGroups[i] != null) itemGroups[i].alpha = canAfford ? 1f : 0.78f;
                if (itemCosts[i] != null) itemCosts[i].color = canAfford ? CostNormalText : CostLackText;
                if (itemBackgrounds[i] != null && (placer == null || itemData[i] != placer.Current))
                {
                    itemBackgrounds[i].color = canAfford ? CardNormalBg : CardDisabledBg;
                }
            }
        }

        // Tạo hàng nút tab danh mục công trình và nút chế độ tháo dỡ.
        void BuildTabs()
        {
            if (tabRow == null) return;
            for (int i = tabRow.childCount - 1; i >= 0; i--)
            {
                Destroy(tabRow.GetChild(i).gameObject);
            }
            tabs.Clear();

            foreach (BuildingCategory category in System.Enum.GetValues(typeof(BuildingCategory)))
            {
                var value = category;
                string labelText = CategoryNames.TryGetValue(value, out var name) ? name : value.ToString();
                string countText = placer != null ? placer.CountIn(value).ToString() : "0";

                var tab = CreateTabItem(labelText, countText, () =>
                {
                    AudioDirector.Post(SoundId.UiClick, Vector3.zero);
                    if (placer != null) placer.SetCategory(value);
                });
                tabs[value] = tab;
            }

            var spacer = new GameObject("TabSpacer", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(tabRow, false);
            var spacerLayout = spacer.GetComponent<LayoutElement>();
            spacerLayout.flexibleWidth = 1f;

            demolishTabButton = CreateTabItem("Tháo dỡ (X)", null, () =>
            {
                AudioDirector.Post(SoundId.UiClick, Vector3.zero);
                if (placer != null) placer.ToggleDemolishMode();
            });
            demolishTabButton.gameObject.name = "demolishModeButton";
        }

        // Khởi tạo một nút tab từ Prefab hoặc tự dựng nếu chưa gán Prefab.
        Button CreateTabItem(string title, string count, UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = tabButtonPrefab != null
                ? Instantiate(tabButtonPrefab, tabRow)
                : CreateFallbackTabObject(tabRow);

            var btn = go.GetComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(onClick);

            var titleTf = FindChildRecursive(go.transform, "tabTitle");
            if (titleTf != null && titleTf.TryGetComponent<TMP_Text>(out var titleLabel))
            {
                titleLabel.text = title;
            }

            var countBadgeTf = FindChildRecursive(go.transform, "countBadge");
            var countTf = FindChildRecursive(go.transform, "tabCount");
            if (string.IsNullOrEmpty(count))
            {
                if (countBadgeTf != null) countBadgeTf.gameObject.SetActive(false);
                else if (countTf != null) countTf.gameObject.SetActive(false);
            }
            else
            {
                if (countBadgeTf != null) countBadgeTf.gameObject.SetActive(true);
                if (countTf != null && countTf.TryGetComponent<TMP_Text>(out var countLabel))
                {
                    countLabel.text = count;
                }
            }

            return btn;
        }

        // Tạo lại danh sách thẻ công trình khi đổi nhóm danh mục.
        void RebuildList(BuildingCategory category)
        {
            foreach (var pair in tabs)
            {
                ApplyTabStyle(pair.Value, pair.Key == category);
            }

            if (buildList == null) return;
            for (int i = buildList.childCount - 1; i >= 0; i--)
            {
                Destroy(buildList.GetChild(i).gameObject);
            }

            items.Clear();
            itemBackgrounds.Clear();
            itemOutlines.Clear();
            itemCosts.Clear();
            itemGroups.Clear();
            itemData.Clear();

            var buildings = placer != null ? placer.Visible : null;
            if (buildings != null)
            {
                foreach (var data in buildings)
                {
                    CreateCardItem(data);
                }
            }

            if (emptyLabel != null) emptyLabel.SetActive(items.Count == 0);
            buildList.gameObject.SetActive(items.Count > 0);

            Refresh(placer != null ? placer.Current : null);
            RefreshAffordability();
        }

        // Tạo một thẻ công trình trong danh sách xây dựng.
        void CreateCardItem(BuildingData data)
        {
            GameObject go = buildCardPrefab != null
                ? Instantiate(buildCardPrefab, buildList)
                : CreateFallbackCardObject(buildList);

            go.name = "Card_" + data.name;
            var btn = go.GetComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                AudioDirector.Post(SoundId.UiClick, Vector3.zero);
                if (placer != null) placer.Toggle(data);
            });

            var bg = go.GetComponent<Image>();
            var outline = go.GetComponent<Outline>();
            var group = go.GetComponent<CanvasGroup>();
            if (group == null) group = go.AddComponent<CanvasGroup>();

            var fpTf = FindChildRecursive(go.transform, "cardFootprint");
            if (fpTf != null && fpTf.TryGetComponent<RectTransform>(out var fpRect))
            {
                float w = Mathf.Clamp(data.footprint.x, 1, 6) * 10f;
                float d = Mathf.Clamp(data.footprint.y, 1, 6) * 7.5f;
                fpRect.sizeDelta = new Vector2(w, d);
            }

            var nameTf = FindChildRecursive(go.transform, "cardName");
            if (nameTf != null && nameTf.TryGetComponent<TMP_Text>(out var nameText))
                nameText.text = data.displayName;

            var sizeTf = FindChildRecursive(go.transform, "cardSize");
            if (sizeTf != null && sizeTf.TryGetComponent<TMP_Text>(out var sizeText))
                sizeText.text = data.footprint.x + "x" + data.footprint.y;

            TMP_Text costText = null;
            var costTf = FindChildRecursive(go.transform, "cardCost");
            if (costTf != null) costTf.TryGetComponent(out costText);
            if (costText != null) costText.text = data.FormatCost();

            items.Add(btn);
            itemBackgrounds.Add(bg);
            itemOutlines.Add(outline);
            itemCosts.Add(costText);
            itemGroups.Add(group);
            itemData.Add(data);
        }

        // Áp dụng màu wireframe xám cho nút tab theo trạng thái đang mở hay không.
        static void ApplyTabStyle(Button tab, bool active)
        {
            if (tab == null) return;
            if (tab.TryGetComponent<Image>(out var bg))
                bg.color = active ? TabActiveBg : TabNormalBg;
            if (tab.TryGetComponent<Outline>(out var outline))
                outline.effectColor = active ? BorderSelected : BorderNormal;

            var titleTf = FindChildRecursive(tab.transform, "tabTitle");
            if (titleTf != null && titleTf.TryGetComponent<TMP_Text>(out var titleText))
            {
                titleText.color = active ? TabActiveText : TabNormalText;
                titleText.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
            }
        }

        // Làm mới giao diện khi trạng thái tháo dỡ thay đổi.
        void RefreshDemolishState()
        {
            Refresh(placer != null ? placer.Current : null);
        }

        // Cập nhật trạng thái chọn thẻ công trình và thanh hành động đặt/tháo dỡ.
        void Refresh(BuildingData current)
        {
            var pop = PopulationManager.Instance;
            for (int i = 0; i < items.Count; i++)
            {
                bool selected = itemData[i] == current;
                bool canAfford = pop == null || pop.CanAfford(itemData[i]);
                if (itemBackgrounds[i] != null)
                {
                    itemBackgrounds[i].color = selected ? CardSelectedBg : (canAfford ? CardNormalBg : CardDisabledBg);
                }
                if (itemOutlines[i] != null)
                {
                    itemOutlines[i].effectColor = selected ? BorderSelected : BorderNormal;
                }
            }

            bool isDemolishing = placer != null && placer.IsDemolishing;
            ApplyTabStyle(demolishTabButton, isDemolishing);

            if (isDemolishing)
            {
                if (placingBar != null) placingBar.SetActive(true);
                if (rotateButton != null) rotateButton.gameObject.SetActive(false);

                var pending = placer.PendingDemolishTarget;
                if (pending != null && DemolishSite.CanDemolish(pending))
                {
                    var occ = pending.GetComponent<GridOccupant>();
                    string name = occ != null ? occ.DisplayName : pending.name;
                    float time = occ != null ? occ.DemolishTime : 2.5f;
                    string refund = occ != null && occ.buildingData != null ? occ.buildingData.FormatCost() : "Miễn phí";
                    if (placingLabel != null)
                        placingLabel.text = $"Xác nhận phá: {name}  ·  Hoàn 100% ({refund})  ·  Thời gian: {time:0.#}s";
                    if (confirmDemolishButton != null) confirmDemolishButton.gameObject.SetActive(true);
                    if (cancelButtonText != null) cancelButtonText.text = "Hủy";
                }
                else
                {
                    if (placingLabel != null)
                        placingLabel.text = "Chế độ tháo dỡ: Click vào công trình cần phá (Chuột phải / Esc để thoát)";
                    if (confirmDemolishButton != null) confirmDemolishButton.gameObject.SetActive(false);
                    if (cancelButtonText != null) cancelButtonText.text = "Thoát";
                }
                if (buildPanel != null) LayoutRebuilder.ForceRebuildLayoutImmediate(buildPanel);
                return;
            }

            if (rotateButton != null) rotateButton.gameObject.SetActive(true);
            if (confirmDemolishButton != null) confirmDemolishButton.gameObject.SetActive(false);
            if (cancelButtonText != null) cancelButtonText.text = "Hủy";

            if (placingBar != null) placingBar.SetActive(current != null);
            if (current != null && placingLabel != null)
            {
                placingLabel.text = $"Đang đặt: {current.displayName} ({current.FormatCost()})";
            }
            if (buildPanel != null) LayoutRebuilder.ForceRebuildLayoutImmediate(buildPanel);
        }

        // Tạo nút tab dự phòng bằng code nếu chưa gán Prefab.
        static GameObject CreateFallbackTabObject(Transform parent)
        {
            var go = new GameObject("BuildTabButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = TabNormalBg;
            go.GetComponent<Outline>().effectColor = BorderNormal;

            var layout = go.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 4, 4);
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var le = go.GetComponent<LayoutElement>();
            le.minHeight = 28f;
            le.preferredHeight = 28f;
            le.flexibleWidth = 0f;
            le.flexibleHeight = 0f;

            var titleGo = new GameObject("tabTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(go.transform, false);
            var titleText = titleGo.GetComponent<TextMeshProUGUI>();
            titleText.fontSize = 13f;
            titleText.color = TabNormalText;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.raycastTarget = false;

            var badgeGo = new GameObject("countBadge", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
            badgeGo.transform.SetParent(go.transform, false);
            badgeGo.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.12f, 1f);
            var badgeLayout = badgeGo.GetComponent<HorizontalLayoutGroup>();
            badgeLayout.padding = new RectOffset(5, 5, 1, 1);
            badgeLayout.childControlWidth = true;
            badgeLayout.childControlHeight = true;

            var countGo = new GameObject("tabCount", typeof(RectTransform), typeof(TextMeshProUGUI));
            countGo.transform.SetParent(badgeGo.transform, false);
            var countText = countGo.GetComponent<TextMeshProUGUI>();
            countText.fontSize = 12f;
            countText.fontStyle = FontStyles.Bold;
            countText.color = CostNormalText;
            countText.alignment = TextAlignmentOptions.Center;
            countText.raycastTarget = false;

            return go;
        }

        // Tạo thẻ công trình dự phòng bằng code nếu chưa gán Prefab.
        static GameObject CreateFallbackCardObject(Transform parent)
        {
            var go = new GameObject("BuildCardButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline), typeof(VerticalLayoutGroup), typeof(LayoutElement), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = CardNormalBg;
            go.GetComponent<Outline>().effectColor = BorderNormal;

            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(5, 5, 5, 5);
            layout.spacing = 2f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var le = go.GetComponent<LayoutElement>();
            le.minWidth = 108f;
            le.preferredWidth = 108f;
            le.minHeight = 122f;
            le.preferredHeight = 122f;
            le.flexibleWidth = 0f;
            le.flexibleHeight = 0f;

            var previewGo = new GameObject("cardPreview", typeof(RectTransform), typeof(Image));
            previewGo.transform.SetParent(go.transform, false);
            previewGo.GetComponent<RectTransform>().sizeDelta = new Vector2(96f, 48f);
            previewGo.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 1f);
            previewGo.GetComponent<Image>().raycastTarget = false;

            var fpGo = new GameObject("cardFootprint", typeof(RectTransform), typeof(Image), typeof(Outline));
            fpGo.transform.SetParent(previewGo.transform, false);
            fpGo.GetComponent<RectTransform>().sizeDelta = new Vector2(24f, 24f);
            fpGo.GetComponent<Image>().color = new Color(0.55f, 0.55f, 0.55f, 1f);
            fpGo.GetComponent<Image>().raycastTarget = false;
            fpGo.GetComponent<Outline>().effectColor = new Color(0.88f, 0.88f, 0.88f, 1f);

            MakeCardLabel(go.transform, "cardName", 13f, FontStyles.Bold, new Color(0.95f, 0.95f, 0.95f, 1f), 19f);
            MakeCardLabel(go.transform, "cardSize", 11f, FontStyles.Normal, new Color(0.78f, 0.78f, 0.78f, 1f), 16f);
            MakeCardLabel(go.transform, "cardCost", 12f, FontStyles.Bold, CostNormalText, 19f);

            return go;
        }

        // Tạo một dòng chữ TextMeshPro con bên trong thẻ công trình.
        static void MakeCardLabel(Transform parent, string name, float size, FontStyles style, Color color, float height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(98f, height);
            var txt = go.GetComponent<TextMeshProUGUI>();
            txt.fontSize = size;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = TextAlignmentOptions.Center;
            txt.textWrappingMode = TextWrappingModes.NoWrap;
            txt.raycastTarget = false;
        }
    }
}
