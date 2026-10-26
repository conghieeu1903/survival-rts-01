using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SurvivalRts.Editor
{
    // Dựng scene init thành màn loading, scene menu thành menu chính (kèm LockstepNetwork + LanDiscovery), cập nhật scene battle và xếp thứ tự Build Settings.
    public static class LoadingSceneSetup
    {
        const string InitScene = "Assets/Scenes/init.unity";
        const string MenuScene = "Assets/Scenes/menu.unity";
        const string MultiplayerScene = "Assets/Scenes/battle.unity";
        const string OfflineScene = "Assets/Scenes/prototype.unity";
        const string FontPath = "Assets/Prefabs/UI/Common/LiberationSans_VN_SDF.asset";
        const string SoundLibraryPath = "Assets/ScriptableObjects/Audio/SoundLibrary.asset";
        const string BattleDataPath = "Assets/ScriptableObjects/Battle/BattleData.asset";

        [MenuItem("Tools/Loading/Setup Init And Menu Scenes")]
        public static void SetupScenes()
        {
            SetupInitScene();
            BattleSceneSetup.Setup();
            SetupMenuScene();
            SetBuildScenes();
        }

        static void SetupInitScene()
        {
            var scene = EditorSceneManager.OpenScene(InitScene, OpenSceneMode.Single);

            var loading = GameObject.Find("Loading");
            if (loading == null) loading = new GameObject("Loading");
            var screen = loading.GetComponent<LoadingScreen>();
            if (screen == null) screen = loading.AddComponent<LoadingScreen>();

            var so = new SerializedObject(screen);
            so.FindProperty("nextScene").stringValue = GameFlow.MenuScene;
            so.FindProperty("soundLibrary").objectReferenceValue = AssetDatabase.LoadAssetAtPath<SoundLibrary>(SoundLibraryPath);
            so.FindProperty("battleData").objectReferenceValue = AssetDatabase.LoadAssetAtPath<BattleData>(BattleDataPath);
            so.FindProperty("fontAsset").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void SetupMenuScene()
        {
            var scene = EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Single);

            var menu = GameObject.Find("MainMenu");
            if (menu == null) menu = new GameObject("MainMenu");
            var component = menu.GetComponent<MainMenu>();
            if (component == null) component = menu.AddComponent<MainMenu>();

            var so = new SerializedObject(component);
            so.FindProperty("fontAsset").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            so.ApplyModifiedPropertiesWithoutUndo();

            var audioGo = GameObject.Find("AudioDirector");
            if (audioGo == null) audioGo = new GameObject("AudioDirector");
            var director = audioGo.GetComponent<AudioDirector>();
            if (director == null) director = audioGo.AddComponent<AudioDirector>();
            var audioSo = new SerializedObject(director);
            audioSo.FindProperty("library").objectReferenceValue = AssetDatabase.LoadAssetAtPath<SoundLibrary>(SoundLibraryPath);
            audioSo.ApplyModifiedPropertiesWithoutUndo();

            var netGo = GameObject.Find("LockstepNetwork");
            if (netGo == null) netGo = new GameObject("LockstepNetwork");
            if (netGo.GetComponent<LockstepNetwork>() == null) netGo.AddComponent<LockstepNetwork>();
            if (netGo.GetComponent<LanDiscovery>() == null) netGo.AddComponent<LanDiscovery>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // init đứng đầu để game luôn bắt đầu bằng màn loading.
        static void SetBuildScenes()
        {
            var scenes = new List<EditorBuildSettingsScene>();
            foreach (var path in new[] { InitScene, MenuScene, MultiplayerScene, OfflineScene })
                scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
