using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace SurvivalRts.Editor
{
    // Công cụ dựng các Prefab giao diện uGUI và TextMeshPro wireframe xám và cập nhật vào Scene.
    [InitializeOnLoad]
    public static class UiPrefabGenerator
    {
        static UiPrefabGenerator()
        {
            EditorApplication.delayCall += EnsureUpdatedOnce;
        }

        // Tự động cập nhật lại Prefab nếu bản đồ vẫn còn cấu trúc khung cũ hoặc có Missing Prefab.
        static void EnsureUpdatedOnce()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            var bmPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Screens/BuildMenuScreen.prefab");
            bool needsUpdate = bmPrefab != null && bmPrefab.transform.Find("minimapPanel") != null;
            var activeScene = EditorSceneManager.GetActiveScene();
            if (!needsUpdate && activeScene.IsValid() && activeScene.path == "Assets/Scenes/prototype.unity")
            {
                foreach (var rootGo in activeScene.GetRootGameObjects())
                {
                    if (rootGo != null && PrefabUtility.IsPrefabAssetMissing(rootGo))
                    {
                        needsUpdate = true;
                        break;
                    }
                }
            }
            if (needsUpdate)
            {
                BuildAllPrefabsAndSetupScene();
            }
        }
        static readonly Color PanelBg = new Color(0.14f, 0.14f, 0.14f, 0.95f);
        static readonly Color CardBg = new Color(0.22f, 0.22f, 0.22f, 1f);
        static readonly Color TrackBg = new Color(0.10f, 0.10f, 0.10f, 1f);
        static readonly Color BorderCol = new Color(0.48f, 0.48f, 0.48f, 1f);
        static readonly Color TextMain = new Color(0.96f, 0.96f, 0.96f, 1f);
        static readonly Color TextDim = new Color(0.78f, 0.78f, 0.78f, 1f);
        static readonly Color AccentGray = new Color(0.90f, 0.90f, 0.90f, 1f);
        static readonly Color DarkText = new Color(0.08f, 0.08f, 0.08f, 1f);

        // Tạo toàn bộ Prefab uGUI dùng TextMeshPro trong Assets/Prefabs/UI và cập nhật Scene hiện tại.
        public static void BuildAllPrefabsAndSetupScene()
        {
            var font = EnsureVietnameseTmpFont();

            var badgePrefab = BuildResourceBadgePrefab(font);
            var tabPrefab = BuildTabButtonPrefab(font);
            var cardPrefab = BuildCardButtonPrefab(font);

            var buildMenuPrefab = BuildBuildMenuScreenPrefab(font, badgePrefab, tabPrefab, cardPrefab);
            var healthBarPrefab = BuildHealthBarScreenPrefab(font);
            var unitCommandPrefab = BuildUnitCommandScreenPrefab(font);
            var settingsMenuPrefab = BuildSettingsMenuScreenPrefab(font);

            SetupActiveScene(buildMenuPrefab, healthBarPrefab, unitCommandPrefab, settingsMenuPrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // Tạo hoặc lấy TMP_FontAsset hỗ trợ đầy đủ tiếng Việt có dấu ở chế độ Dynamic SDF.
        static TMP_FontAsset EnsureVietnameseTmpFont()
        {
            const string fontAssetPath = "Assets/Prefabs/UI/Common/LiberationSans_VN_SDF.asset";
            var vnFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontAssetPath);
            var libFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");

            if (vnFont == null && libFont != null)
            {
                vnFont = TMP_FontAsset.CreateFontAsset(
                    libFont,
                    44,
                    5,
                    GlyphRenderMode.SDFAA,
                    1024,
                    1024,
                    AtlasPopulationMode.Dynamic,
                    true
                );
                vnFont.name = "LiberationSans_VN_SDF";
                AssetDatabase.CreateAsset(vnFont, fontAssetPath);
                if (vnFont.material != null)
                {
                    vnFont.material.name = "LiberationSans_VN_SDF Material";
                    AssetDatabase.AddObjectToAsset(vnFont.material, fontAssetPath);
                }
                if (vnFont.atlasTextures != null)
                {
                    for (int i = 0; i < vnFont.atlasTextures.Length; i++)
                    {
                        if (vnFont.atlasTextures[i] != null)
                        {
                            vnFont.atlasTextures[i].name = "LiberationSans_VN_SDF Atlas " + i;
                            AssetDatabase.AddObjectToAsset(vnFont.atlasTextures[i], fontAssetPath);
                        }
                    }
                }
            }

            if (vnFont != null)
            {
                const string vnChars = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~°·"
                    + "ÀÁÂÃÈÉÊÌÍÒÓÔÕÙÚÝàáâãèéêìíòóôõùúýĂăĐđĨĩŨũƠơƯưẠạẢảẤấẦầẨẩẪẫẬậẮắẰằẲẳẴẵẶặẸẹẺẻẼẽẾếỀềỂểỄễỆệỈỉỊịỌọỎỏỐốỒồỔổỖỗỘộỚớỜờỞởỠỡỢợỤụỦủỨứỪừỬửỮữỰựỲỳỴỵỶỷỸỹ";
                vnFont.TryAddCharacters(vnChars, out _);
                EditorUtility.SetDirty(vnFont);

                var defaultTmp = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
                if (defaultTmp != null)
                {
                    if (defaultTmp.fallbackFontAssetTable == null)
                        defaultTmp.fallbackFontAssetTable = new List<TMP_FontAsset>();
                    if (!defaultTmp.fallbackFontAssetTable.Contains(vnFont))
                    {
                        defaultTmp.fallbackFontAssetTable.Add(vnFont);
                        EditorUtility.SetDirty(defaultTmp);
                    }
                }
            }

            return vnFont;
        }

        // Tạo Prefab ô hiển thị tài nguyên dùng chung trên thanh HUD.
        static GameObject BuildResourceBadgePrefab(TMP_FontAsset font)
        {
            var root = new GameObject("ResourceBadge", typeof(RectTransform), typeof(Image), typeof(Outline), typeof(HorizontalLayoutGroup), typeof(LayoutElement), typeof(ContentSizeFitter));
            root.GetComponent<Image>().color = PanelBg;
            root.GetComponent<Outline>().effectColor = BorderCol;

            var hlg = root.GetComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(8, 12, 6, 6);
            hlg.spacing = 8f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            var le = root.GetComponent<LayoutElement>();
            le.minWidth = 108f;
            le.minHeight = 42f;
            le.preferredHeight = 42f;
            le.flexibleWidth = 0f;
            le.flexibleHeight = 0f;

            var csf = root.GetComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var iconGo = new GameObject("icon", typeof(RectTransform), typeof(Image), typeof(Outline), typeof(LayoutElement));
            iconGo.transform.SetParent(root.transform, false);
            var iconImg = iconGo.GetComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.color = CardBg;
            iconImg.raycastTarget = false;
            iconGo.GetComponent<Outline>().effectColor = BorderCol;
            var iconLe = iconGo.GetComponent<LayoutElement>();
            iconLe.minWidth = 26f;
            iconLe.preferredWidth = 26f;
            iconLe.minHeight = 26f;
            iconLe.preferredHeight = 26f;
            iconLe.flexibleWidth = 0f;
            iconLe.flexibleHeight = 0f;

            var infoGo = new GameObject("info", typeof(RectTransform), typeof(VerticalLayoutGroup));
            infoGo.transform.SetParent(root.transform, false);
            var vlg = infoGo.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 1f;
            vlg.childAlignment = TextAnchor.MiddleLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;

            CreateText(infoGo.transform, "resourceLabel", font, "TÀI NGUYÊN", 10.5f, FontStyles.Bold, TextDim, TextAlignmentOptions.MidlineLeft);
            CreateText(infoGo.transform, "valueLabel", font, "0", 15f, FontStyles.Bold, TextMain, TextAlignmentOptions.MidlineLeft);

            string path = "Assets/Prefabs/UI/Common/ResourceBadge.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // Tạo Prefab nút tab danh mục công trình.
        static GameObject BuildTabButtonPrefab(TMP_FontAsset font)
        {
            var root = new GameObject("BuildTabButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(UiButtonFeedback), typeof(Outline), typeof(HorizontalLayoutGroup), typeof(LayoutElement), typeof(ContentSizeFitter));
            root.GetComponent<Image>().color = CardBg;
            root.GetComponent<Outline>().effectColor = BorderCol;

            var hlg = root.GetComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(10, 10, 4, 4);
            hlg.spacing = 6f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            var le = root.GetComponent<LayoutElement>();
            le.minHeight = 28f;
            le.preferredHeight = 28f;
            le.flexibleWidth = 0f;
            le.flexibleHeight = 0f;

            var csf = root.GetComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            CreateText(root.transform, "tabTitle", font, "Kinh tế", 12.5f, FontStyles.Normal, TextMain, TextAlignmentOptions.Center);

            var badgeGo = new GameObject("countBadge", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
            badgeGo.transform.SetParent(root.transform, false);
            badgeGo.GetComponent<Image>().color = TrackBg;
            badgeGo.GetComponent<Image>().raycastTarget = false;
            var cbHlg = badgeGo.GetComponent<HorizontalLayoutGroup>();
            cbHlg.padding = new RectOffset(5, 5, 1, 1);
            cbHlg.childAlignment = TextAnchor.MiddleCenter;
            cbHlg.childControlWidth = true;
            cbHlg.childControlHeight = true;

            CreateText(badgeGo.transform, "tabCount", font, "0", 11.5f, FontStyles.Bold, AccentGray, TextAlignmentOptions.Center);

            string path = "Assets/Prefabs/UI/Components/BuildTabButton.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // Tạo Prefab thẻ công trình trong danh sách xây dựng.
        static GameObject BuildCardButtonPrefab(TMP_FontAsset font)
        {
            var root = new GameObject("BuildCardButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(UiButtonFeedback), typeof(Outline), typeof(VerticalLayoutGroup), typeof(LayoutElement), typeof(CanvasGroup));
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(106f, 120f);
            root.GetComponent<Image>().color = CardBg;
            root.GetComponent<Outline>().effectColor = BorderCol;

            var vlg = root.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(5, 5, 5, 5);
            vlg.spacing = 2f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var le = root.GetComponent<LayoutElement>();
            le.minWidth = 106f;
            le.preferredWidth = 106f;
            le.minHeight = 120f;
            le.preferredHeight = 120f;
            le.flexibleWidth = 0f;
            le.flexibleHeight = 0f;

            var previewGo = new GameObject("cardPreview", typeof(RectTransform), typeof(Image));
            previewGo.transform.SetParent(root.transform, false);
            previewGo.GetComponent<RectTransform>().sizeDelta = new Vector2(94f, 46f);
            previewGo.GetComponent<Image>().color = TrackBg;
            previewGo.GetComponent<Image>().raycastTarget = false;

            var fpGo = new GameObject("cardFootprint", typeof(RectTransform), typeof(Image), typeof(Outline));
            fpGo.transform.SetParent(previewGo.transform, false);
            var fpRect = fpGo.GetComponent<RectTransform>();
            fpRect.anchorMin = new Vector2(0.5f, 0.5f);
            fpRect.anchorMax = new Vector2(0.5f, 0.5f);
            fpRect.sizeDelta = new Vector2(24f, 24f);
            fpGo.GetComponent<Image>().color = new Color(0.58f, 0.58f, 0.58f, 1f);
            fpGo.GetComponent<Image>().raycastTarget = false;
            fpGo.GetComponent<Outline>().effectColor = TextMain;

            var nameTxt = CreateText(root.transform, "cardName", font, "Công trình", 12.5f, FontStyles.Bold, TextMain, TextAlignmentOptions.Center);
            nameTxt.rectTransform.sizeDelta = new Vector2(96f, 19f);

            var sizeTxt = CreateText(root.transform, "cardSize", font, "2x2", 11f, FontStyles.Normal, TextDim, TextAlignmentOptions.Center);
            sizeTxt.rectTransform.sizeDelta = new Vector2(96f, 16f);

            var costTxt = CreateText(root.transform, "cardCost", font, "Miễn phí", 11.5f, FontStyles.Bold, AccentGray, TextAlignmentOptions.Center);
            costTxt.rectTransform.sizeDelta = new Vector2(96f, 18f);

            string path = "Assets/Prefabs/UI/Components/BuildCardButton.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // Tạo Prefab màn hình BuildMenuScreen (gồm Resource HUD, Minimap và Build Panel).
        static GameObject BuildBuildMenuScreenPrefab(TMP_FontAsset font, GameObject badgePrefab, GameObject tabPrefab, GameObject cardPrefab)
        {
            var root = CreateScreenCanvas("BuildMenu", 10);
            var bmCtrl = root.AddComponent<BuildMenuController>();
            var mmCtrl = root.AddComponent<MinimapController>();

            var hudGo = new GameObject("resourceHud", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            hudGo.transform.SetParent(root.transform, false);
            var hudRect = hudGo.GetComponent<RectTransform>();
            hudRect.anchorMin = new Vector2(1f, 1f);
            hudRect.anchorMax = new Vector2(1f, 1f);
            hudRect.pivot = new Vector2(1f, 1f);
            hudRect.anchoredPosition = new Vector2(-16f, -14f);
            var hudHlg = hudGo.GetComponent<HorizontalLayoutGroup>();
            hudHlg.spacing = 8f;
            hudHlg.childAlignment = TextAnchor.MiddleRight;
            hudHlg.childControlWidth = true;
            hudHlg.childControlHeight = true;
            hudHlg.childForceExpandWidth = false;
            hudHlg.childForceExpandHeight = false;
            var hudCsf = hudGo.GetComponent<ContentSizeFitter>();
            hudCsf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            hudCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var popValueLabel = InstantiateResourceBadge(hudGo.transform, badgePrefab, "popBadge", "DÂN SỐ", "popValueLabel", "0 / 0");
            var goldValueLabel = InstantiateResourceBadge(hudGo.transform, badgePrefab, "goldBadge", "TIỀN", "goldValueLabel", "0");
            var foodValueLabel = InstantiateResourceBadge(hudGo.transform, badgePrefab, "foodBadge", "LÚA", "foodValueLabel", "0");
            var woodValueLabel = InstantiateResourceBadge(hudGo.transform, badgePrefab, "woodBadge", "GỖ", "woodValueLabel", "0");
            var stoneValueLabel = InstantiateResourceBadge(hudGo.transform, badgePrefab, "stoneBadge", "ĐÁ", "stoneValueLabel", "0");
            var ironValueLabel = InstantiateResourceBadge(hudGo.transform, badgePrefab, "ironBadge", "SẮT", "ironValueLabel", "0");

            var mmCanvasGo = new GameObject("minimapCanvas", typeof(RectTransform), typeof(RawImage), typeof(Outline), typeof(UiPointerBlocker));
            mmCanvasGo.transform.SetParent(root.transform, false);
            var mmRect = mmCanvasGo.GetComponent<RectTransform>();
            mmRect.anchorMin = new Vector2(0f, 0f);
            mmRect.anchorMax = new Vector2(0f, 0f);
            mmRect.pivot = new Vector2(0f, 0f);
            mmRect.anchoredPosition = new Vector2(16f, 16f);
            mmRect.sizeDelta = new Vector2(170f, 170f);
            mmCanvasGo.GetComponent<Outline>().effectColor = BorderCol;

            var fullMapGo = new GameObject("fullMapCanvas", typeof(RectTransform), typeof(RawImage), typeof(Outline), typeof(UiPointerBlocker));
            fullMapGo.transform.SetParent(root.transform, false);
            var fmRect = fullMapGo.GetComponent<RectTransform>();
            fmRect.anchorMin = new Vector2(0.5f, 0.5f);
            fmRect.anchorMax = new Vector2(0.5f, 0.5f);
            fmRect.pivot = new Vector2(0.5f, 0.5f);
            fmRect.anchoredPosition = new Vector2(0f, 36f);
            fmRect.sizeDelta = new Vector2(460f, 460f);
            fullMapGo.GetComponent<Outline>().effectColor = BorderCol;
            fullMapGo.SetActive(false);

            var buildPanel = new GameObject("buildPanel", typeof(RectTransform), typeof(Image), typeof(Outline), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(UiPointerBlocker));
            buildPanel.transform.SetParent(root.transform, false);
            var bpRect = buildPanel.GetComponent<RectTransform>();
            bpRect.anchorMin = new Vector2(0.5f, 0f);
            bpRect.anchorMax = new Vector2(0.5f, 0f);
            bpRect.pivot = new Vector2(0.5f, 0f);
            bpRect.anchoredPosition = new Vector2(0f, 16f);
            buildPanel.GetComponent<Image>().color = PanelBg;
            buildPanel.GetComponent<Outline>().effectColor = BorderCol;
            var bpVlg = buildPanel.GetComponent<VerticalLayoutGroup>();
            bpVlg.padding = new RectOffset(12, 12, 8, 12);
            bpVlg.spacing = 8f;
            bpVlg.childAlignment = TextAnchor.UpperLeft;
            bpVlg.childControlWidth = true;
            bpVlg.childControlHeight = true;
            bpVlg.childForceExpandWidth = true;
            bpVlg.childForceExpandHeight = false;
            var bpCsf = buildPanel.GetComponent<ContentSizeFitter>();
            bpCsf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            bpCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var headerRow = new GameObject("headerRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            headerRow.transform.SetParent(buildPanel.transform, false);
            var hdrHlg = headerRow.GetComponent<HorizontalLayoutGroup>();
            hdrHlg.spacing = 18f;
            hdrHlg.childAlignment = TextAnchor.MiddleLeft;
            hdrHlg.childControlWidth = true;
            hdrHlg.childControlHeight = true;
            hdrHlg.childForceExpandWidth = false;
            hdrHlg.childForceExpandHeight = false;
            var hdrLe = headerRow.GetComponent<LayoutElement>();
            hdrLe.minWidth = 480f;
            hdrLe.minHeight = 20f;
            hdrLe.preferredHeight = 20f;
            hdrLe.flexibleHeight = 0f;

            CreateText(headerRow.transform, "titleLabel", font, "XÂY DỰNG", 13.5f, FontStyles.Bold, TextMain, TextAlignmentOptions.MidlineLeft);
            var popLabel = CreateText(headerRow.transform, "popLabel", font, "Dân rỗi: 0/0  ·  Thợ xây: 0", 12.5f, FontStyles.Normal, TextDim, TextAlignmentOptions.MidlineLeft);
            popLabel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            CreateText(headerRow.transform, "hintLabel", font, "Chuột trái đặt  ·  Chuột phải hủy", 12f, FontStyles.Normal, TextDim, TextAlignmentOptions.MidlineRight);

            var tabRow = new GameObject("tabRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            tabRow.transform.SetParent(buildPanel.transform, false);
            var tabHlg = tabRow.GetComponent<HorizontalLayoutGroup>();
            tabHlg.spacing = 6f;
            tabHlg.childAlignment = TextAnchor.MiddleLeft;
            tabHlg.childControlWidth = true;
            tabHlg.childControlHeight = true;
            tabHlg.childForceExpandWidth = false;
            tabHlg.childForceExpandHeight = false;
            var tabRowLe = tabRow.GetComponent<LayoutElement>();
            tabRowLe.minWidth = 480f;
            tabRowLe.minHeight = 28f;
            tabRowLe.preferredHeight = 28f;
            tabRowLe.flexibleHeight = 0f;

            var buildList = new GameObject("buildList", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            buildList.transform.SetParent(buildPanel.transform, false);
            var listHlg = buildList.GetComponent<HorizontalLayoutGroup>();
            listHlg.spacing = 8f;
            listHlg.childAlignment = TextAnchor.MiddleLeft;
            listHlg.childControlWidth = true;
            listHlg.childControlHeight = true;
            listHlg.childForceExpandWidth = false;
            listHlg.childForceExpandHeight = false;
            var listLe = buildList.GetComponent<LayoutElement>();
            listLe.minWidth = 480f;
            listLe.minHeight = 120f;
            listLe.preferredHeight = 120f;
            listLe.flexibleHeight = 0f;

            var emptyLabel = CreateText(buildPanel.transform, "emptyLabel", font, "Chưa có công trình nào trong nhóm này", 13f, FontStyles.Normal, TextDim, TextAlignmentOptions.Center);
            var emptyLe = emptyLabel.gameObject.AddComponent<LayoutElement>();
            emptyLe.minWidth = 480f;
            emptyLe.minHeight = 110f;
            emptyLe.preferredHeight = 110f;
            emptyLe.flexibleHeight = 0f;
            emptyLabel.gameObject.SetActive(false);

            var placingBar = new GameObject("placingBar", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            placingBar.transform.SetParent(buildPanel.transform, false);
            var pbHlg = placingBar.GetComponent<HorizontalLayoutGroup>();
            pbHlg.spacing = 6f;
            pbHlg.padding = new RectOffset(0, 0, 4, 0);
            pbHlg.childAlignment = TextAnchor.MiddleLeft;
            pbHlg.childControlWidth = true;
            pbHlg.childControlHeight = true;
            pbHlg.childForceExpandWidth = false;
            pbHlg.childForceExpandHeight = false;
            var pbLe = placingBar.GetComponent<LayoutElement>();
            pbLe.minWidth = 480f;
            pbLe.minHeight = 32f;
            pbLe.preferredHeight = 32f;
            pbLe.flexibleHeight = 0f;

            var placingLabel = CreateText(placingBar.transform, "placingLabel", font, "Đang đặt", 12.5f, FontStyles.Normal, TextMain, TextAlignmentOptions.MidlineLeft);
            placingLabel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            var rotateBtn = CreateButton(placingBar.transform, "rotateButton", font, "Xoay 90°", 86f, 26f, CardBg, TextMain);
            var confirmDemolishBtn = CreateButton(placingBar.transform, "confirmDemolishButton", font, "Xác nhận phá", 112f, 26f, AccentGray, DarkText);
            confirmDemolishBtn.gameObject.SetActive(false);
            var cancelBtn = CreateButton(placingBar.transform, "cancelButton", font, "Hủy", 68f, 26f, CardBg, TextMain);
            placingBar.SetActive(false);

            var bmSo = new SerializedObject(bmCtrl);
            bmSo.FindProperty("tabButtonPrefab").objectReferenceValue = tabPrefab;
            bmSo.FindProperty("buildCardPrefab").objectReferenceValue = cardPrefab;
            bmSo.FindProperty("buildPanel").objectReferenceValue = bpRect;
            bmSo.FindProperty("tabRow").objectReferenceValue = tabRow.GetComponent<RectTransform>();
            bmSo.FindProperty("buildList").objectReferenceValue = buildList.GetComponent<RectTransform>();
            bmSo.FindProperty("emptyLabel").objectReferenceValue = emptyLabel.gameObject;
            bmSo.FindProperty("placingBar").objectReferenceValue = placingBar;
            bmSo.FindProperty("placingLabel").objectReferenceValue = placingLabel;
            bmSo.FindProperty("rotateButton").objectReferenceValue = rotateBtn;
            bmSo.FindProperty("confirmDemolishButton").objectReferenceValue = confirmDemolishBtn;
            bmSo.FindProperty("cancelButton").objectReferenceValue = cancelBtn;
            bmSo.FindProperty("popLabel").objectReferenceValue = popLabel;
            bmSo.FindProperty("popValueLabel").objectReferenceValue = popValueLabel;
            bmSo.FindProperty("woodValueLabel").objectReferenceValue = woodValueLabel;
            bmSo.FindProperty("stoneValueLabel").objectReferenceValue = stoneValueLabel;
            bmSo.FindProperty("ironValueLabel").objectReferenceValue = ironValueLabel;
            bmSo.FindProperty("foodValueLabel").objectReferenceValue = foodValueLabel;
            bmSo.FindProperty("goldValueLabel").objectReferenceValue = goldValueLabel;
            bmSo.ApplyModifiedPropertiesWithoutUndo();

            var mmSo = new SerializedObject(mmCtrl);
            mmSo.FindProperty("minimapCanvas").objectReferenceValue = mmCanvasGo.GetComponent<RawImage>();
            mmSo.FindProperty("fullMapCanvas").objectReferenceValue = fullMapGo.GetComponent<RawImage>();
            mmSo.ApplyModifiedPropertiesWithoutUndo();

            string path = "Assets/Prefabs/UI/Screens/BuildMenuScreen.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // Tạo một ô tài nguyên từ Prefab và trả về nhãn giá trị TMP_Text của nó.
        static TMP_Text InstantiateResourceBadge(Transform parent, GameObject badgePrefab, string badgeName, string labelText, string valueObjName, string defaultVal)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(badgePrefab, parent);
            go.name = badgeName;
            var labelTf = go.transform.Find("info/resourceLabel");
            if (labelTf != null && labelTf.TryGetComponent<TMP_Text>(out var lbl))
            {
                lbl.text = labelText;
            }
            var valTf = go.transform.Find("info/valueLabel");
            if (valTf != null && valTf.TryGetComponent<TMP_Text>(out var valTxt))
            {
                valTf.name = valueObjName;
                valTxt.text = defaultVal;
                return valTxt;
            }
            return null;
        }

        // Tạo Prefab màn hình HealthBarScreen hiển thị thanh máu và bảng thợ.
        static GameObject BuildHealthBarScreenPrefab(TMP_FontAsset font)
        {
            var root = CreateScreenCanvas("HealthBar", 5);
            var hbCtrl = root.AddComponent<HealthBarController>();

            var barGo = new GameObject("healthBar", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            barGo.transform.SetParent(root.transform, false);
            var barRect = barGo.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0.5f, 0.5f);
            barRect.anchorMax = new Vector2(0.5f, 0.5f);
            barRect.pivot = new Vector2(0.5f, 0f);
            var barVlg = barGo.GetComponent<VerticalLayoutGroup>();
            barVlg.spacing = 4f;
            barVlg.childAlignment = TextAnchor.LowerCenter;
            barVlg.childControlWidth = true;
            barVlg.childControlHeight = true;
            barVlg.childForceExpandWidth = false;
            barVlg.childForceExpandHeight = false;
            var barCsf = barGo.GetComponent<ContentSizeFitter>();
            barCsf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            barCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var statusBox = new GameObject("statusBox", typeof(RectTransform), typeof(Image), typeof(Outline), typeof(HorizontalLayoutGroup));
            statusBox.transform.SetParent(barGo.transform, false);
            statusBox.GetComponent<Image>().color = PanelBg;
            statusBox.GetComponent<Image>().raycastTarget = false;
            statusBox.GetComponent<Outline>().effectColor = BorderCol;
            var sbHlg = statusBox.GetComponent<HorizontalLayoutGroup>();
            sbHlg.padding = new RectOffset(8, 8, 2, 2);
            sbHlg.childAlignment = TextAnchor.MiddleCenter;
            sbHlg.childControlWidth = true;
            sbHlg.childControlHeight = true;
            var statusLabel = CreateText(statusBox.transform, "statusLabel", font, "", 11.5f, FontStyles.Bold, TextMain, TextAlignmentOptions.Center);
            statusBox.SetActive(false);

            var barTrack = new GameObject("barTrack", typeof(RectTransform), typeof(Image), typeof(Outline), typeof(LayoutElement));
            barTrack.transform.SetParent(barGo.transform, false);
            barTrack.GetComponent<Image>().color = TrackBg;
            barTrack.GetComponent<Image>().raycastTarget = false;
            barTrack.GetComponent<Outline>().effectColor = BorderCol;
            var btLe = barTrack.GetComponent<LayoutElement>();
            btLe.minWidth = 144f;
            btLe.preferredWidth = 144f;
            btLe.minHeight = 18f;
            btLe.preferredHeight = 18f;
            btLe.flexibleWidth = 0f;
            btLe.flexibleHeight = 0f;

            var fillGo = new GameObject("fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(barTrack.transform, false);
            var fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImg = fillGo.GetComponent<Image>();
            fillImg.color = new Color(0.75f, 0.75f, 0.75f, 1f);
            fillImg.raycastTarget = false;

            var valueLabel = CreateText(barTrack.transform, "valueLabel", font, "0 / 0", 11.5f, FontStyles.Bold, TextMain, TextAlignmentOptions.Center);
            var valRect = valueLabel.rectTransform;
            valRect.anchorMin = Vector2.zero;
            valRect.anchorMax = Vector2.one;
            valRect.offsetMin = Vector2.zero;
            valRect.offsetMax = Vector2.zero;

            var workerPanel = new GameObject("workerPanel", typeof(RectTransform), typeof(Image), typeof(Outline), typeof(HorizontalLayoutGroup), typeof(UiPointerBlocker));
            workerPanel.transform.SetParent(barGo.transform, false);
            workerPanel.GetComponent<Image>().color = PanelBg;
            workerPanel.GetComponent<Outline>().effectColor = BorderCol;
            var wpHlg = workerPanel.GetComponent<HorizontalLayoutGroup>();
            wpHlg.padding = new RectOffset(6, 6, 3, 3);
            wpHlg.spacing = 6f;
            wpHlg.childAlignment = TextAnchor.MiddleCenter;
            wpHlg.childControlWidth = true;
            wpHlg.childControlHeight = true;
            wpHlg.childForceExpandWidth = false;
            wpHlg.childForceExpandHeight = false;

            var removeWorkerBtn = CreateButton(workerPanel.transform, "removeWorkerButton", font, "-", 26f, 22f, CardBg, TextMain);
            var workerLabel = CreateText(workerPanel.transform, "workerLabel", font, "Thợ: 0/3", 11.5f, FontStyles.Bold, TextMain, TextAlignmentOptions.Center);
            var addWorkerBtn = CreateButton(workerPanel.transform, "addWorkerButton", font, "+ Thợ", 60f, 22f, CardBg, TextMain);
            workerPanel.SetActive(false);

            var demolishPanel = new GameObject("demolishPanel", typeof(RectTransform), typeof(Image), typeof(Outline), typeof(HorizontalLayoutGroup), typeof(UiPointerBlocker));
            demolishPanel.transform.SetParent(barGo.transform, false);
            demolishPanel.GetComponent<Image>().color = PanelBg;
            demolishPanel.GetComponent<Outline>().effectColor = BorderCol;
            var dpHlg = demolishPanel.GetComponent<HorizontalLayoutGroup>();
            dpHlg.padding = new RectOffset(6, 6, 3, 3);
            dpHlg.spacing = 6f;
            dpHlg.childAlignment = TextAnchor.MiddleCenter;
            dpHlg.childControlWidth = true;
            dpHlg.childControlHeight = true;
            dpHlg.childForceExpandWidth = false;
            dpHlg.childForceExpandHeight = false;

            var demolishBtn = CreateButton(demolishPanel.transform, "demolishButton", font, "Tháo dỡ", 70f, 22f, CardBg, TextMain);
            var demolishConfirmLbl = CreateText(demolishPanel.transform, "demolishConfirmLabel", font, "Phá (2.5s)?", 11.5f, FontStyles.Bold, TextMain, TextAlignmentOptions.Center);
            demolishConfirmLbl.gameObject.SetActive(false);
            var confirmDemolishBtn = CreateButton(demolishPanel.transform, "confirmDemolishBtn", font, "Xác nhận phá", 96f, 22f, AccentGray, DarkText);
            confirmDemolishBtn.gameObject.SetActive(false);
            var cancelDemolishBtn = CreateButton(demolishPanel.transform, "cancelDemolishBtn", font, "Hủy", 50f, 22f, CardBg, TextMain);
            cancelDemolishBtn.gameObject.SetActive(false);
            demolishPanel.SetActive(false);
            barGo.SetActive(false);

            var hbSo = new SerializedObject(hbCtrl);
            hbSo.FindProperty("canvasRect").objectReferenceValue = root.GetComponent<RectTransform>();
            hbSo.FindProperty("bar").objectReferenceValue = barRect;
            hbSo.FindProperty("fillRect").objectReferenceValue = fillRect;
            hbSo.FindProperty("fillImage").objectReferenceValue = fillImg;
            hbSo.FindProperty("valueLabel").objectReferenceValue = valueLabel;
            hbSo.FindProperty("statusBox").objectReferenceValue = statusBox;
            hbSo.FindProperty("statusLabel").objectReferenceValue = statusLabel;
            hbSo.FindProperty("workerPanel").objectReferenceValue = workerPanel;
            hbSo.FindProperty("workerLabel").objectReferenceValue = workerLabel;
            hbSo.FindProperty("addWorkerButton").objectReferenceValue = addWorkerBtn;
            hbSo.FindProperty("removeWorkerButton").objectReferenceValue = removeWorkerBtn;
            hbSo.FindProperty("demolishPanel").objectReferenceValue = demolishPanel;
            hbSo.FindProperty("demolishButton").objectReferenceValue = demolishBtn;
            hbSo.FindProperty("demolishConfirmLabel").objectReferenceValue = demolishConfirmLbl;
            hbSo.FindProperty("confirmDemolishBtn").objectReferenceValue = confirmDemolishBtn;
            hbSo.FindProperty("cancelDemolishBtn").objectReferenceValue = cancelDemolishBtn;
            hbSo.ApplyModifiedPropertiesWithoutUndo();

            string path = "Assets/Prefabs/UI/Screens/HealthBarScreen.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // Tạo Prefab màn hình UnitCommandScreen điều khiển lính.
        static GameObject BuildUnitCommandScreenPrefab(TMP_FontAsset font)
        {
            var root = CreateScreenCanvas("UnitCommand", 12);
            var ucCtrl = root.AddComponent<UnitCommandController>();

            var cmdRoot = new GameObject("commandRoot", typeof(RectTransform));
            cmdRoot.transform.SetParent(root.transform, false);
            var crRect = cmdRoot.GetComponent<RectTransform>();
            crRect.anchorMin = new Vector2(0.5f, 0f);
            crRect.anchorMax = new Vector2(0.5f, 0f);
            crRect.pivot = new Vector2(0.5f, 0f);
            crRect.anchoredPosition = new Vector2(0f, 245f);

            var cmdPanel = new GameObject("commandPanel", typeof(RectTransform), typeof(Image), typeof(Outline), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter), typeof(UiPointerBlocker));
            cmdPanel.transform.SetParent(cmdRoot.transform, false);
            var cpRect = cmdPanel.GetComponent<RectTransform>();
            cpRect.anchorMin = new Vector2(0.5f, 0f);
            cpRect.anchorMax = new Vector2(0.5f, 0f);
            cpRect.pivot = new Vector2(0.5f, 0f);
            cpRect.anchoredPosition = Vector2.zero;
            cmdPanel.GetComponent<Image>().color = PanelBg;
            cmdPanel.GetComponent<Outline>().effectColor = BorderCol;

            var cpHlg = cmdPanel.GetComponent<HorizontalLayoutGroup>();
            cpHlg.padding = new RectOffset(12, 12, 8, 8);
            cpHlg.spacing = 10f;
            cpHlg.childAlignment = TextAnchor.MiddleCenter;
            cpHlg.childControlWidth = true;
            cpHlg.childControlHeight = true;
            cpHlg.childForceExpandWidth = false;
            cpHlg.childForceExpandHeight = false;

            var cpCsf = cmdPanel.GetComponent<ContentSizeFitter>();
            cpCsf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            cpCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var title = CreateText(cmdPanel.transform, "commandTitle", font, "Lính kiếm", 14f, FontStyles.Bold, TextMain, TextAlignmentOptions.MidlineLeft);
            var status = CreateText(cmdPanel.transform, "commandStatus", font, "Đang canh gác trước trại", 12.5f, FontStyles.Normal, TextDim, TextAlignmentOptions.MidlineLeft);
            var moveBtn = CreateButton(cmdPanel.transform, "moveButton", font, "Di chuyển", 88f, 30f, CardBg, TextMain);
            var returnBtn = CreateButton(cmdPanel.transform, "returnButton", font, "Về trại", 78f, 30f, CardBg, TextMain);
            var cancelBtn = CreateButton(cmdPanel.transform, "cancelButton", font, "Hủy", 62f, 30f, CardBg, TextMain);
            cancelBtn.gameObject.SetActive(false);
            cmdRoot.SetActive(false);

            var ucSo = new SerializedObject(ucCtrl);
            ucSo.FindProperty("root").objectReferenceValue = cmdRoot;
            ucSo.FindProperty("commandPanel").objectReferenceValue = cpRect;
            ucSo.FindProperty("title").objectReferenceValue = title;
            ucSo.FindProperty("status").objectReferenceValue = status;
            ucSo.FindProperty("moveButton").objectReferenceValue = moveBtn;
            ucSo.FindProperty("returnButton").objectReferenceValue = returnBtn;
            ucSo.FindProperty("cancelButton").objectReferenceValue = cancelBtn;
            ucSo.ApplyModifiedPropertiesWithoutUndo();

            string path = "Assets/Prefabs/UI/Screens/UnitCommandScreen.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // Tạo Prefab màn hình SettingsMenuScreen cho menu tạm dừng Esc trong trận.
        static GameObject BuildSettingsMenuScreenPrefab(TMP_FontAsset font)
        {
            var root = new GameObject("SettingsMenu", typeof(SettingsMenuController));
            var ctrl = root.GetComponent<SettingsMenuController>();
            var so = new SerializedObject(ctrl);
            so.FindProperty("fontAsset").objectReferenceValue = font;
            so.ApplyModifiedPropertiesWithoutUndo();

            string path = "Assets/Prefabs/UI/Screens/SettingsMenuScreen.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // Tạo một đối tượng Canvas chuẩn cho mỗi màn hình UI.
        static GameObject CreateScreenCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            canvas.pixelPerfect = false;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440f, 810f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            return go;
        }

        // Tạo phần tử TextMeshProUGUI sắc nét với các thuộc tính cơ bản.
        static TextMeshProUGUI CreateText(Transform parent, string name, TMP_FontAsset font, string content, float fontSize, FontStyles style, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var txt = go.GetComponent<TextMeshProUGUI>();
            if (font != null) txt.font = font;
            txt.text = content;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = align;
            txt.textWrappingMode = TextWrappingModes.NoWrap;
            txt.overflowMode = TextOverflowModes.Overflow;
            txt.raycastTarget = false;
            return txt;
        }

        // Tạo nút bấm uGUI dùng nhãn TextMeshProUGUI có kích thước cố định.
        static Button CreateButton(Transform parent, string name, TMP_FontAsset font, string label, float width, float height, Color bgCol, Color textCol)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(UiButtonFeedback), typeof(Outline), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(width, height);
            go.GetComponent<Image>().color = bgCol;
            go.GetComponent<Outline>().effectColor = BorderCol;

            var le = go.GetComponent<LayoutElement>();
            le.minWidth = width;
            le.preferredWidth = width;
            le.minHeight = height;
            le.preferredHeight = height;
            le.flexibleWidth = 0f;
            le.flexibleHeight = 0f;

            var txt = CreateText(go.transform, "Text", font, label, 12.5f, FontStyles.Bold, textCol, TextAlignmentOptions.Center);
            var tr = txt.rectTransform;
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;

            return go.GetComponent<Button>();
        }

        // Thay thế các đối tượng UI trong Scene hiện tại bằng Prefab uGUI TextMeshPro và lưu Scene.
        static void SetupActiveScene(GameObject buildMenuPrefab, GameObject healthBarPrefab, GameObject unitCommandPrefab, GameObject settingsMenuPrefab)
        {
            var placer = Object.FindFirstObjectByType<BuildingPlacer>();
            var selector = Object.FindFirstObjectByType<BuildingSelector>();
            var mainCam = Camera.main;

            DestroyAllByName("BuildMenu");
            DestroyAllByName("HealthBar");
            DestroyAllByName("UnitCommand");
            DestroyAllByName("SettingsMenu");
            foreach (var rootGo in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (rootGo != null && (PrefabUtility.IsPrefabAssetMissing(rootGo) || rootGo.name.StartsWith("SettingsMenu")))
                    Object.DestroyImmediate(rootGo);
            }
            foreach (var c in Object.FindObjectsByType<BuildMenuController>(FindObjectsSortMode.None))
                if (c != null) Object.DestroyImmediate(c.gameObject);
            foreach (var c in Object.FindObjectsByType<MinimapController>(FindObjectsSortMode.None))
                if (c != null) Object.DestroyImmediate(c.gameObject);

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var esGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                Undo.RegisterCreatedObjectUndo(esGo, "Create EventSystem");
            }

            var bmInst = (GameObject)PrefabUtility.InstantiatePrefab(buildMenuPrefab);
            bmInst.name = "BuildMenu";
            var bmSo = new SerializedObject(bmInst.GetComponent<BuildMenuController>());
            bmSo.FindProperty("placer").objectReferenceValue = placer;
            bmSo.ApplyModifiedPropertiesWithoutUndo();

            var hbInst = (GameObject)PrefabUtility.InstantiatePrefab(healthBarPrefab);
            hbInst.name = "HealthBar";
            var hbSo = new SerializedObject(hbInst.GetComponent<HealthBarController>());
            hbSo.FindProperty("selector").objectReferenceValue = selector;
            hbSo.FindProperty("cam").objectReferenceValue = mainCam;
            hbSo.ApplyModifiedPropertiesWithoutUndo();

            var ucInst = (GameObject)PrefabUtility.InstantiatePrefab(unitCommandPrefab);
            ucInst.name = "UnitCommand";
            var ucSo = new SerializedObject(ucInst.GetComponent<UnitCommandController>());
            ucSo.FindProperty("selector").objectReferenceValue = selector;
            ucSo.FindProperty("cam").objectReferenceValue = mainCam;
            ucSo.ApplyModifiedPropertiesWithoutUndo();

            var smInst = (GameObject)PrefabUtility.InstantiatePrefab(settingsMenuPrefab);
            smInst.name = "SettingsMenu";
            var smSo = new SerializedObject(smInst.GetComponent<SettingsMenuController>());
            smSo.FindProperty("placer").objectReferenceValue = placer;
            smSo.FindProperty("fontAsset").objectReferenceValue = EnsureVietnameseTmpFont();
            smSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }

        // Xóa sạch mọi đối tượng trùng tên trong Scene hiện tại.
        static void DestroyAllByName(string objName)
        {
            while (true)
            {
                var go = GameObject.Find(objName);
                if (go == null) break;
                Object.DestroyImmediate(go);
            }
        }
    }
}
