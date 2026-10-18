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

        [Header("Tham chiếu")]
        [Tooltip("Placer cung cấp danh sách công trình và nhận lệnh chọn từ menu")]
        [SerializeField] BuildingPlacer placer;

        readonly List<Button> items = new List<Button>();

        void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            var list = root.Q<VisualElement>("buildList");
            var panel = root.Q<VisualElement>("buildPanel");

            panel.RegisterCallback<PointerEnterEvent>(_ => UiPointerState.IsOverUI = true);
            panel.RegisterCallback<PointerLeaveEvent>(_ => UiPointerState.IsOverUI = false);

            list.Clear();
            items.Clear();
            var buildings = placer.Buildings;
            for (int i = 0; i < buildings.Count; i++)
            {
                var data = buildings[i];
                var item = new Button(() => placer.Toggle(data));
                item.AddToClassList(ItemClass);

                var key = new Label((i + 1).ToString());
                key.AddToClassList("build-card-key");

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
                item.Add(key);
                item.Add(title);
                item.Add(size);
                list.Add(item);
                items.Add(item);
            }

            placer.CurrentChanged += Refresh;
            Refresh(placer.Current);
        }

        void OnDisable()
        {
            if (placer != null) placer.CurrentChanged -= Refresh;
            UiPointerState.IsOverUI = false;
        }

        void Refresh(BuildingData current)
        {
            var buildings = placer.Buildings;
            for (int i = 0; i < items.Count; i++)
                items[i].EnableInClassList(SelectedClass, buildings[i] == current);
        }
    }
}
