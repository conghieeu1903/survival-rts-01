using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SurvivalRts.Editor
{
    // Gắn giao diện trận đại chiến (BattleUiController, LanDiscovery) vào scene đang mở và gỡ bảng phòng chờ cũ.
    public static class BattleSceneSetup
    {
        const string FontPath = "Assets/Prefabs/UI/Common/LiberationSans_VN_SDF.asset";
        const string UiObjectName = "BattleUi";

        [MenuItem("Tools/Battle/Setup Battle Scene")]
        public static void Setup()
        {
            var simulation = Object.FindFirstObjectByType<BattleSimulation>();
            if (simulation == null)
            {
                Debug.LogError("Scene đang mở không có BattleSimulation, hãy mở scene battle trước");
                return;
            }

            // gỡ component mất script (bảng phòng chờ cũ đã bị xóa) trên toàn scene
            int removed = 0;
            foreach (var root in simulation.gameObject.scene.GetRootGameObjects())
                foreach (var child in root.GetComponentsInChildren<Transform>(true))
                    removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);

            var input = simulation.GetComponent<BattleInput>();
            if (input == null) input = simulation.gameObject.AddComponent<BattleInput>();

            var uiObject = GameObject.Find(UiObjectName);
            if (uiObject == null) uiObject = new GameObject(UiObjectName);

            var controller = uiObject.GetComponent<BattleUiController>();
            if (controller == null) controller = uiObject.AddComponent<BattleUiController>();
            var discovery = Object.FindFirstObjectByType<LanDiscovery>();
            if (discovery == null) discovery = uiObject.AddComponent<LanDiscovery>();

            var serialized = new SerializedObject(controller);
            serialized.FindProperty("simulation").objectReferenceValue = simulation;
            serialized.FindProperty("battleInput").objectReferenceValue = input;
            serialized.FindProperty("lanDiscovery").objectReferenceValue = discovery;
            serialized.FindProperty("fontAsset").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(uiObject);
            EditorSceneManager.MarkSceneDirty(simulation.gameObject.scene);
            Debug.Log($"Đã thiết lập giao diện trận đại chiến (gỡ {removed} component mất script)");
        }
    }
}
