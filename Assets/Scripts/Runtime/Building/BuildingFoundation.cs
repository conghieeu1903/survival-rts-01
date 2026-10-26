using UnityEngine;

namespace SurvivalRts
{
    // Sinh phần móng đá màu xám bên dưới sàn phẳng của công trình để lấp kín khe hở trên sườn dốc.
    public class BuildingFoundation : MonoBehaviour
    {
        static Mesh sharedBoxMesh;
        static Material sharedStoneMaterial;

        Vector2Int baseFootprint = new Vector2Int(2, 2);
        int angle;
        float cellSize = 1f;
        float baseY;
        float lowestY;
        Transform foundationBlock;
        MeshRenderer foundationRenderer;
        HeightField subscribedField;

        public float BaseY => baseY;
        public float LowestY => lowestY;
        public float FoundationDepth { get; private set; }

        public void Setup(Vector2Int footprint, int yawAngle, float gridCellSize, float floorY, float minGroundY, Material overrideMaterial = null)
        {
            baseFootprint = footprint;
            angle = yawAngle;
            cellSize = gridCellSize;
            baseY = floorY;
            lowestY = minGroundY;

            EnsureVisual();
            if (overrideMaterial != null)
                foundationRenderer.sharedMaterial = overrideMaterial;
            else
                foundationRenderer.sharedMaterial = GetStoneMaterial();

            UpdateTransform();
            SubscribeHeightField();
        }

        public void UpdateHeights(Vector2Int footprint, int yawAngle, float gridCellSize, float floorY, float minGroundY, Material overrideMaterial = null)
        {
            baseFootprint = footprint;
            angle = yawAngle;
            cellSize = gridCellSize;
            baseY = floorY;
            lowestY = minGroundY;

            EnsureVisual();
            if (overrideMaterial != null)
                foundationRenderer.sharedMaterial = overrideMaterial;
            UpdateTransform();
        }

        void OnDestroy()
        {
            if (subscribedField != null)
                subscribedField.RegionHeightsChanged -= OnRegionHeightsChanged;
        }

        void SubscribeHeightField()
        {
            var hf = HeightField.Instance != null ? HeightField.Instance : FindFirstObjectByType<HeightField>();
            if (hf == subscribedField) return;
            if (subscribedField != null)
                subscribedField.RegionHeightsChanged -= OnRegionHeightsChanged;
            subscribedField = hf;
            if (subscribedField != null)
                subscribedField.RegionHeightsChanged += OnRegionHeightsChanged;
        }

        void OnRegionHeightsChanged(int minWorldX, int minWorldZ, int maxWorldX, int maxWorldZ)
        {
            var grid = BuildGrid.Instance;
            if (grid == null) return;
            grid.SampleFootprintHeights(transform.position, baseFootprint, angle, out _, out float newLowestY, out _, out _);
            lowestY = newLowestY;
            UpdateTransform();
        }

        void EnsureVisual()
        {
            if (foundationBlock != null) return;
            var existing = transform.Find("StoneFoundation");
            if (existing != null)
            {
                foundationBlock = existing;
                foundationRenderer = existing.GetComponent<MeshRenderer>();
                return;
            }

            var go = new GameObject("StoneFoundation");
            go.layer = gameObject.layer;
            foundationBlock = go.transform;
            foundationBlock.SetParent(transform, false);

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = GetBoxMesh();
            foundationRenderer = go.AddComponent<MeshRenderer>();
            foundationRenderer.sharedMaterial = GetStoneMaterial();
        }

        void UpdateTransform()
        {
            if (foundationBlock == null) return;
            float delta = Mathf.Max(0f, baseY - lowestY);
            float depth = Mathf.Max(0.14f, delta + 0.18f);
            FoundationDepth = depth;

            float width = baseFootprint.x * cellSize + 0.08f;
            float length = baseFootprint.y * cellSize + 0.08f;

            foundationBlock.localRotation = Quaternion.identity;
            foundationBlock.localPosition = new Vector3(0f, -depth * 0.5f + 0.02f, 0f);
            foundationBlock.localScale = new Vector3(width, depth, length);
        }

        static Mesh GetBoxMesh()
        {
            if (sharedBoxMesh != null) return sharedBoxMesh;
            var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sharedBoxMesh = temp.GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(temp);
            return sharedBoxMesh;
        }

        static Material GetStoneMaterial()
        {
            if (sharedStoneMaterial != null) return sharedStoneMaterial;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            sharedStoneMaterial = new Material(shader)
            {
                name = "StoneFoundationGraybox",
                color = new Color(0.38f, 0.38f, 0.40f, 1f)
            };
            if (sharedStoneMaterial.HasProperty("_BaseColor"))
                sharedStoneMaterial.SetColor("_BaseColor", new Color(0.38f, 0.38f, 0.40f, 1f));
            if (sharedStoneMaterial.HasProperty("_Smoothness"))
                sharedStoneMaterial.SetFloat("_Smoothness", 0.1f);
            return sharedStoneMaterial;
        }
    }
}
