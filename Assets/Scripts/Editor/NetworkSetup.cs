using System.Reflection;
using FishNet.Managing;
using FishNet.Managing.Object;
using UnityEditor;
using UnityEngine;

namespace SurvivalRts.Editor
{
    // Tạo prefab NetworkManager (Tugboat) để LockstepNetwork nạp lúc chạy.
    public static class NetworkSetup
    {
        const string PrefabPath = "Assets/Resources/NetworkManager.prefab";

        [MenuItem("Tools/Network/Create NetworkManager Prefab")]
        public static void CreateNetworkManagerPrefab()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");

            var root = new GameObject("NetworkManager");
            var manager = root.AddComponent<NetworkManager>();

            // Generator của FishNet là internal nên gọi qua reflection
            var generator = typeof(NetworkManager).Assembly.GetType("FishNet.Editing.PrefabCollectionGenerator.Generator");
            var method = generator?.GetMethod("GetDefaultPrefabObjects", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            manager.SpawnablePrefabs = method?.Invoke(null, new object[] { null }) as PrefabObjects;

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log("Đã tạo " + PrefabPath);
        }
    }
}
