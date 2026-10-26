using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SurvivalRts.Editor
{
    // Đóng gói các module gameplay thành Prefab, chuyển toàn bộ camera gameplay sang Perspective và cập nhật hai scene prototype và battle.
    public static class BattleSceneSetup
    {
        const string PrototypeScenePath = "Assets/Scenes/prototype.unity";
        const string BattleScenePath = "Assets/Scenes/battle.unity";
        const string GameplayPrefabFolder = "Assets/Prefabs/Gameplay";
        const string UiScreensFolder = "Assets/Prefabs/UI/Screens";
        const string FontPath = "Assets/Prefabs/UI/Common/LiberationSans_VN_SDF.asset";
        const string UiObjectName = "BattleUi";

        const string MainCameraPrefabPath = GameplayPrefabFolder + "/MainCamera.prefab";
        const string AudioDirectorPrefabPath = GameplayPrefabFolder + "/AudioDirector.prefab";
        const string TracerPoolPrefabPath = GameplayPrefabFolder + "/TracerPool.prefab";

        struct SceneObjectRef
        {
            public Component component;
            public string propertyPath;
            public Object target;
        }

        [MenuItem("Tools/Battle/Setup Battle Scene")]
        public static void Setup()
        {
            EnsureFolders();
            SetupPrototypeScene();
            SetupBattleScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // Tạo thư mục chứa Prefab module gameplay và màn hình UI nếu chưa có.
        static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder(GameplayPrefabFolder))
                AssetDatabase.CreateFolder("Assets/Prefabs", "Gameplay");
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/UI"))
                AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
            if (!AssetDatabase.IsValidFolder(UiScreensFolder))
                AssetDatabase.CreateFolder("Assets/Prefabs/UI", "Screens");
        }

        // Chuyển camera sang Perspective và đóng gói toàn bộ module trong scene prototype thành Prefab.
        static void SetupPrototypeScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != PrototypeScenePath)
                scene = EditorSceneManager.OpenScene(PrototypeScenePath, OpenSceneMode.Single);

            UiPrefabGenerator.BuildAllPrefabsAndSetupScene();

            var camCtrl = Object.FindFirstObjectByType<CameraController>();
            if (camCtrl != null)
            {
                var cam = camCtrl.GetComponent<Camera>();
                if (cam != null)
                {
                    cam.orthographic = false;
                    cam.fieldOfView = 60f;
                    cam.nearClipPlane = 0.3f;
                    cam.farClipPlane = 1500f;
                    cam.orthographicSize = 12f;
                }
                camCtrl.ApplyPerspectiveMode();
                SaveAndConnectModule(camCtrl.gameObject, MainCameraPrefabPath);
            }

            var audioDirector = Object.FindFirstObjectByType<AudioDirector>();
            if (audioDirector != null)
                SaveAndConnectModule(audioDirector.gameObject, AudioDirectorPrefabPath);

            var tracerPool = Object.FindFirstObjectByType<TracerPool>();
            if (tracerPool != null)
                SaveAndConnectModule(tracerPool.gameObject, TracerPoolPrefabPath);

            var buildGrid = Object.FindFirstObjectByType<BuildGrid>();
            if (buildGrid != null)
                SaveAndConnectModule(buildGrid.gameObject, GameplayPrefabFolder + "/BuildGrid.prefab");

            var placer = Object.FindFirstObjectByType<BuildingPlacer>();
            if (placer != null)
                SaveAndConnectModule(placer.gameObject, GameplayPrefabFolder + "/BuildingPlacer.prefab");

            var selector = Object.FindFirstObjectByType<BuildingSelector>();
            if (selector != null)
                SaveAndConnectModule(selector.gameObject, GameplayPrefabFolder + "/BuildingSelector.prefab");

            var popManager = Object.FindFirstObjectByType<PopulationManager>();
            if (popManager != null)
                SaveAndConnectModule(popManager.gameObject, GameplayPrefabFolder + "/PopulationManager.prefab");

            var enemySpawner = Object.FindFirstObjectByType<EnemySpawner>();
            if (enemySpawner != null)
                SaveAndConnectModule(enemySpawner.gameObject, GameplayPrefabFolder + "/EnemySpawn.prefab");

            var enemyEcs = Object.FindFirstObjectByType<EnemyEcsConfig>();
            if (enemyEcs != null)
                SaveAndConnectModule(enemyEcs.gameObject, GameplayPrefabFolder + "/EnemyEcs.prefab");

            WirePrototypeReferences();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Đã đóng gói các module Prefab và chuyển Camera sang Perspective trong scene prototype.");
        }

        // Gán lại đầy đủ tham chiếu chéo giữa các Prefab instance trong scene prototype.
        static void WirePrototypeReferences()
        {
            var camCtrl = Object.FindFirstObjectByType<CameraController>();
            var mainCam = camCtrl != null ? camCtrl.GetComponent<Camera>() : Camera.main;
            var placer = Object.FindFirstObjectByType<BuildingPlacer>();
            var selector = Object.FindFirstObjectByType<BuildingSelector>();
            var audioDirector = Object.FindFirstObjectByType<AudioDirector>();
            var tracerPool = Object.FindFirstObjectByType<TracerPool>();
            var enemySpawner = Object.FindFirstObjectByType<EnemySpawner>();
            var buildMenu = Object.FindFirstObjectByType<BuildMenuController>();
            var healthBar = Object.FindFirstObjectByType<HealthBarController>();
            var unitCommand = Object.FindFirstObjectByType<UnitCommandController>();
            var settingsMenu = Object.FindFirstObjectByType<SettingsMenuController>();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            if (audioDirector != null)
            {
                var so = new SerializedObject(audioDirector);
                so.FindProperty("cameraController").objectReferenceValue = camCtrl;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(audioDirector);
            }

            if (tracerPool != null)
            {
                var so = new SerializedObject(tracerPool);
                so.FindProperty("cam").objectReferenceValue = mainCam;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(tracerPool);
            }

            if (placer != null)
            {
                var so = new SerializedObject(placer);
                so.FindProperty("cam").objectReferenceValue = mainCam;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(placer);
            }

            if (selector != null)
            {
                var so = new SerializedObject(selector);
                so.FindProperty("placer").objectReferenceValue = placer;
                so.FindProperty("cam").objectReferenceValue = mainCam;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(selector);
            }

            if (enemySpawner != null)
            {
                var area = enemySpawner.GetComponent<EnemySpawnArea>();
                var so = new SerializedObject(enemySpawner);
                so.FindProperty("area").objectReferenceValue = area;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(enemySpawner);
            }

            if (buildMenu != null)
            {
                var so = new SerializedObject(buildMenu);
                so.FindProperty("placer").objectReferenceValue = placer;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(buildMenu);
            }

            if (healthBar != null)
            {
                var so = new SerializedObject(healthBar);
                so.FindProperty("selector").objectReferenceValue = selector;
                so.FindProperty("cam").objectReferenceValue = mainCam;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(healthBar);
            }

            if (unitCommand != null)
            {
                var so = new SerializedObject(unitCommand);
                so.FindProperty("selector").objectReferenceValue = selector;
                so.FindProperty("cam").objectReferenceValue = mainCam;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(unitCommand);
            }

            if (settingsMenu != null)
            {
                var so = new SerializedObject(settingsMenu);
                so.FindProperty("placer").objectReferenceValue = placer;
                if (font != null) so.FindProperty("fontAsset").objectReferenceValue = font;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(settingsMenu);
            }
        }

        // Chuyển camera sang Perspective và đóng gói/kết nối toàn bộ module Prefab trong scene battle.
        static void SetupBattleScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != BattleScenePath)
                scene = EditorSceneManager.OpenScene(BattleScenePath, OpenSceneMode.Single);

            var simulation = Object.FindFirstObjectByType<BattleSimulation>();
            if (simulation == null)
            {
                Debug.LogError("Scene battle không có BattleSimulation");
                return;
            }

            int removed = 0;
            foreach (var root in simulation.gameObject.scene.GetRootGameObjects())
                foreach (var child in root.GetComponentsInChildren<Transform>(true))
                    removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);

            foreach (var net in Object.FindObjectsByType<LockstepNetwork>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(net);
            foreach (var disc in Object.FindObjectsByType<LanDiscovery>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(disc);

            var camCtrl = ReplaceComponentWithSharedPrefabInstance<CameraController>(scene, "Main Camera", MainCameraPrefabPath);
            var mainCam = camCtrl != null ? camCtrl.GetComponent<Camera>() : null;
            if (camCtrl != null)
            {
                if (mainCam != null)
                {
                    mainCam.orthographic = false;
                    mainCam.fieldOfView = 60f;
                    mainCam.nearClipPlane = 0.3f;
                    mainCam.farClipPlane = 1500f;
                    mainCam.orthographicSize = 60f;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(mainCam);
                }
                var camSo = new SerializedObject(camCtrl);
                camSo.FindProperty("moveSpeed").floatValue = 80f;
                camSo.FindProperty("boundsHalfSize").floatValue = 220f;
                camSo.FindProperty("zoomStep").floatValue = 8f;
                camSo.FindProperty("zoomRange").vector2Value = new Vector2(8f, 150f);
                camSo.ApplyModifiedPropertiesWithoutUndo();
                camCtrl.ApplyPerspectiveMode();
                PrefabUtility.RecordPrefabInstancePropertyModifications(camCtrl);
                PrefabUtility.RecordPrefabInstancePropertyModifications(camCtrl.transform);
            }

            var audioDirector = ReplaceComponentWithSharedPrefabInstance<AudioDirector>(scene, "AudioDirector", AudioDirectorPrefabPath);
            var tracerPool = ReplaceComponentWithSharedPrefabInstance<TracerPool>(scene, "TracerPool", TracerPoolPrefabPath);

            var input = simulation.GetComponent<BattleInput>();
            if (input == null) input = simulation.gameObject.AddComponent<BattleInput>();
            var director = simulation.GetComponent<BattleDirector>();

            SaveAndConnectModule(simulation.gameObject, GameplayPrefabFolder + "/Battle.prefab");

            var uiObject = GameObject.Find(UiObjectName);
            if (uiObject == null) uiObject = new GameObject(UiObjectName);

            var controller = uiObject.GetComponent<BattleUiController>();
            if (controller == null) controller = uiObject.AddComponent<BattleUiController>();

            var settingsMenu = uiObject.GetComponent<SettingsMenuController>();
            if (settingsMenu == null) settingsMenu = uiObject.AddComponent<SettingsMenuController>();

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            SaveAndConnectModule(uiObject, UiScreensFolder + "/BattleUiScreen.prefab");

            if (audioDirector != null)
            {
                var so = new SerializedObject(audioDirector);
                so.FindProperty("cameraController").objectReferenceValue = camCtrl;
                so.FindProperty("maxAudibleOrthoSize").floatValue = 30f;
                so.FindProperty("audibleGroundRadius").floatValue = 45f;
                so.FindProperty("marchFullCount").intValue = 600;
                so.FindProperty("battleFullCount").intValue = 250;
                so.FindProperty("battleMusicEngagedCount").intValue = 120;
                so.FindProperty("battleMusicMovingCount").intValue = 800;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(audioDirector);
            }

            if (tracerPool != null)
            {
                var tracerSo = new SerializedObject(tracerPool);
                tracerSo.FindProperty("cam").objectReferenceValue = mainCam;
                tracerSo.FindProperty("width").floatValue = 0.08f;
                tracerSo.FindProperty("duration").floatValue = 0.35f;
                tracerSo.FindProperty("projectileSpeed").floatValue = 24f;
                tracerSo.FindProperty("arcHeightFactor").floatValue = 0.25f;
                tracerSo.FindProperty("arrowLength").floatValue = 1.35f;
                tracerSo.FindProperty("maxTracers").intValue = 400;
                tracerSo.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(tracerPool);
            }

            if (director != null)
            {
                var dirSo = new SerializedObject(director);
                dirSo.FindProperty("arrowTracers").objectReferenceValue = tracerPool;
                dirSo.FindProperty("cameraController").objectReferenceValue = camCtrl;
                dirSo.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(director);
            }

            if (input != null)
            {
                var inSo = new SerializedObject(input);
                inSo.FindProperty("viewCamera").objectReferenceValue = mainCam;
                inSo.FindProperty("cameraController").objectReferenceValue = camCtrl;
                inSo.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(input);
            }

            var serialized = new SerializedObject(controller);
            serialized.FindProperty("simulation").objectReferenceValue = simulation;
            serialized.FindProperty("battleInput").objectReferenceValue = input;
            serialized.FindProperty("fontAsset").objectReferenceValue = font;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(controller);

            var settingsSo = new SerializedObject(settingsMenu);
            settingsSo.FindProperty("fontAsset").objectReferenceValue = font;
            settingsSo.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(settingsMenu);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"Đã đóng gói các module Prefab, chuyển Camera sang Perspective và lưu scene battle (gỡ {removed} component mất script).");
        }

        // Lưu GameObject thành Prefab Asset và kết nối lại vào Scene đồng thời bảo toàn mọi tham chiếu trong Scene.
        static void SaveAndConnectModule(GameObject go, string prefabPath)
        {
            if (go == null) return;

            var sceneRefs = new List<SceneObjectRef>();
            foreach (var comp in go.GetComponentsInChildren<Component>(true))
            {
                if (comp == null || comp is Transform) continue;
                var so = new SerializedObject(comp);
                var prop = so.GetIterator();
                while (prop.Next(true))
                {
                    if (prop.propertyType != SerializedPropertyType.ObjectReference) continue;
                    if (prop.propertyPath == "m_Script" || prop.propertyPath == "m_GameObject") continue;
                    var target = prop.objectReferenceValue;
                    if (target != null && !EditorUtility.IsPersistent(target))
                    {
                        sceneRefs.Add(new SceneObjectRef
                        {
                            component = comp,
                            propertyPath = prop.propertyPath,
                            target = target
                        });
                    }
                }
            }

            if (PrefabUtility.IsPartOfPrefabInstance(go))
            {
                var root = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }

            PrefabUtility.SaveAsPrefabAssetAndConnect(go, prefabPath, InteractionMode.AutomatedAction);

            foreach (var r in sceneRefs)
            {
                if (r.component == null || r.target == null) continue;
                var so = new SerializedObject(r.component);
                var prop = so.FindProperty(r.propertyPath);
                if (prop != null)
                {
                    prop.objectReferenceValue = r.target;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(r.component);
                }
            }
        }

        // Thay mọi đối tượng mang component T trong Scene bằng một Prefab instance dùng chung duy nhất.
        static T ReplaceComponentWithSharedPrefabInstance<T>(Scene scene, string objectName, string prefabPath) where T : Component
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var existingList = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (prefab == null)
                return existingList.Length > 0 ? existingList[0] : null;

            GameObject keptInstance = null;
            int siblingIndex = 0;
            Vector3 pos = prefab.transform.position;
            Quaternion rot = prefab.transform.rotation;
            Vector3 scale = prefab.transform.localScale;

            foreach (var comp in existingList)
            {
                if (comp == null) continue;
                var go = comp.gameObject;
                if (keptInstance == null && PrefabUtility.GetCorrespondingObjectFromSource(go) == prefab)
                {
                    keptInstance = go;
                    keptInstance.name = objectName;
                    continue;
                }
                siblingIndex = go.transform.GetSiblingIndex();
                pos = go.transform.position;
                rot = go.transform.rotation;
                scale = go.transform.localScale;
                Object.DestroyImmediate(go);
            }

            if (keptInstance != null) return keptInstance.GetComponent<T>();

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            inst.name = objectName;
            inst.transform.SetPositionAndRotation(pos, rot);
            inst.transform.localScale = scale;
            inst.transform.SetSiblingIndex(siblingIndex);
            PrefabUtility.RecordPrefabInstancePropertyModifications(inst.transform);
            return inst.GetComponent<T>();
        }
    }
}
