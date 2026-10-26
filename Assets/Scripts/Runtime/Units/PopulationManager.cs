using System;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalRts
{
    // Quản lý tổng dân số, số dân tự do, phân bổ công nhân cho các nhà nghề, kho tài nguyên và sinh model nhân vật.
    public class PopulationManager : MonoBehaviour
    {
        public static PopulationManager Instance { get; private set; }

        [Header("Prefab nhân vật")]
        [Tooltip("Prefab người dân tự do (Villager)")]
        [SerializeField] GameObject villagerPrefab;

        [Tooltip("Prefab thợ xây (Builder)")]
        [SerializeField] GameObject builderPrefab;

        [Tooltip("Prefab tiều phu chặt gỗ (Lumberjack)")]
        [SerializeField] GameObject lumberjackPrefab;

        [Tooltip("Prefab thợ mỏ khai thác đá (Miner)")]
        [SerializeField] GameObject minerPrefab;

        [Tooltip("Prefab nông dân canh tác (Farmer)")]
        [SerializeField] GameObject farmerPrefab;

        [Header("Khởi đầu")]
        [Tooltip("Số dân tự do có sẵn ở Nhà chính khi bắt đầu ván")]
        [SerializeField] int initialFreePopulation = 4;

        [Tooltip("Có sẵn 1 thợ xây khởi điểm ở Nhà chính để xây công trình đầu tiên")]
        [SerializeField] int initialBuilders = 1;

        [Tooltip("Số gỗ khởi điểm khi bắt đầu ván")]
        [SerializeField] int initialWood = 10;

        [Tooltip("Số đá khởi điểm khi bắt đầu ván")]
        [SerializeField] int initialStone = 4;

        [Tooltip("Số lúa khởi điểm khi bắt đầu ván")]
        [SerializeField] int initialFood = 6;

        [Tooltip("Số sắt khởi điểm khi bắt đầu ván")]
        [SerializeField] int initialIron;

        [Tooltip("Số tiền vàng khởi điểm khi bắt đầu ván")]
        [SerializeField] int initialGold;

        readonly List<GameObject> freeVillagerVisuals = new List<GameObject>();
        readonly List<BuilderUnit> activeBuilders = new List<BuilderUnit>();
        readonly List<HarvesterUnit> activeHarvesters = new List<HarvesterUnit>();
        int nextSlot = 1;

        public int TotalPopulation { get; private set; }
        public int AssignedWorkers { get; private set; }
        public int FreePopulation => Mathf.Max(0, TotalPopulation - AssignedWorkers);
        public int BuilderCount => activeBuilders.Count;

        public int Gold { get; private set; }
        public int Food { get; private set; }
        public int Wood { get; private set; }
        public int Stone { get; private set; }
        public int Iron { get; private set; }

        public event Action Changed;

        public void ConfigurePrefabs(GameObject villager, GameObject builder,
            GameObject lumberjack = null, GameObject miner = null, GameObject farmer = null)
        {
            villagerPrefab = villager;
            builderPrefab = builder;
            if (lumberjack != null) lumberjackPrefab = lumberjack;
            if (miner != null) minerPrefab = miner;
            if (farmer != null) farmerPrefab = farmer;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Wood = Mathf.Max(0, initialWood);
            Stone = Mathf.Max(0, initialStone);
            Food = Mathf.Max(0, initialFood);
            Iron = Mathf.Max(0, initialIron);
            Gold = Mathf.Max(0, initialGold);
            ResourceNode.EnsureSceneNodesRegistered();
        }

        void Start()
        {
            var hq = FindAnyObjectByType<Headquarters>();
            var homeTransform = hq != null ? hq.transform : transform;
            var spawnPos = homeTransform.position + new Vector3(0f, 0f, 3f);

            AddPopulation(initialFreePopulation, spawnPos);
            for (int i = 0; i < initialBuilders; i++)
            {
                SpawnBuilderUnit(homeTransform);
            }
            Changed?.Invoke();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void AddResource(ResourceType type, int amount)
        {
            if (amount <= 0) return;
            if (type == ResourceType.Wood) Wood += amount;
            else if (type == ResourceType.Stone) Stone += amount;
            else if (type == ResourceType.Iron) Iron += amount;
            else if (type == ResourceType.Food) Food += amount;
            Changed?.Invoke();
        }

        // Kiểm tra kho tài nguyên có đủ để xây công trình này không.
        public bool CanAfford(BuildingData data)
        {
            if (data == null) return false;
            return Wood >= data.woodCost
                && Stone >= data.stoneCost
                && Iron >= data.ironCost
                && Food >= data.foodCost
                && Gold >= data.goldCost;
        }

        // Trừ tài nguyên khi đặt móng công trình; trả về false nếu không đủ.
        public bool TrySpendResources(BuildingData data)
        {
            if (!CanAfford(data)) return false;
            if (!data.HasAnyCost) return true;

            Wood -= data.woodCost;
            Stone -= data.stoneCost;
            Iron -= data.ironCost;
            Food -= data.foodCost;
            Gold -= data.goldCost;
            Changed?.Invoke();
            return true;
        }

        // Hoàn lại 100% chi phí xây dựng khi người chơi tháo dỡ công trình.
        public void RefundResources(BuildingData data)
        {
            if (data == null || !data.HasAnyCost) return;

            Wood += data.woodCost;
            Stone += data.stoneCost;
            Iron += data.ironCost;
            Food += data.foodCost;
            Gold += data.goldCost;
            Changed?.Invoke();
        }

        // Kiểm tra / trừ / hoàn tài nguyên theo số lượng cụ thể (dùng cho chi phí đào tạo lính).
        public bool CanAfford(int wood, int stone, int iron, int food, int gold)
        {
            return Wood >= wood && Stone >= stone && Iron >= iron && Food >= food && Gold >= gold;
        }

        public bool TrySpend(int wood, int stone, int iron, int food, int gold)
        {
            if (!CanAfford(wood, stone, iron, food, gold)) return false;
            Wood -= wood;
            Stone -= stone;
            Iron -= iron;
            Food -= food;
            Gold -= gold;
            Changed?.Invoke();
            return true;
        }

        public void Refund(int wood, int stone, int iron, int food, int gold)
        {
            Wood += Mathf.Max(0, wood);
            Stone += Mathf.Max(0, stone);
            Iron += Mathf.Max(0, iron);
            Food += Mathf.Max(0, food);
            Gold += Mathf.Max(0, gold);
            Changed?.Invoke();
        }

        // Giữ chỗ một dân tự do cho nghề khác (ví dụ đang đào tạo lính); công trình tự sinh đơn vị của nó.
        public bool TryReserveWorker()
        {
            if (FreePopulation <= 0) return false;
            AssignedWorkers++;
            UpdateVillagerVisibility();
            Changed?.Invoke();
            return true;
        }

        // Trả lại một dân đã giữ chỗ. died = true khi đơn vị đó chết, dân số giảm đi một.
        public void ReleaseWorker(bool died)
        {
            if (AssignedWorkers > 0) AssignedWorkers--;
            if (died) TotalPopulation = Mathf.Max(0, TotalPopulation - 1);
            UpdateVillagerVisibility();
            Changed?.Invoke();
        }

        // Cha chung của các đơn vị do công trình sinh ra, để chúng không bị xoá theo công trình.
        public Transform UnitRoot => transform;

        public List<GameObject> AddPopulation(int amount, Vector3 homePosition)
        {
            var spawned = new List<GameObject>();
            if (amount <= 0) return spawned;

            TotalPopulation += amount;
            for (int i = 0; i < amount; i++)
            {
                var v = SpawnVillagerVisual(homePosition, i);
                if (v != null)
                {
                    freeVillagerVisuals.Add(v);
                    spawned.Add(v);
                }
            }
            UpdateVillagerVisibility();
            Changed?.Invoke();
            return spawned;
        }

        public void RemovePopulation(int amount, List<GameObject> houseVisuals)
        {
            if (amount <= 0) return;
            TotalPopulation = Mathf.Max(0, TotalPopulation - amount);

            if (houseVisuals != null)
            {
                foreach (var v in houseVisuals)
                {
                    if (v == null) continue;
                    freeVillagerVisuals.Remove(v);
                    Destroy(v);
                }
                houseVisuals.Clear();
            }

            UpdateVillagerVisibility();
            Changed?.Invoke();
        }

        public bool TryAssignBuilder(Transform hutTransform, out BuilderUnit builder)
        {
            builder = null;
            if (FreePopulation <= 0) return false;

            AssignedWorkers++;
            builder = SpawnBuilderUnit(hutTransform);
            UpdateVillagerVisibility();
            Changed?.Invoke();
            return true;
        }

        public void UnassignBuilder(BuilderUnit builder)
        {
            if (AssignedWorkers > 0) AssignedWorkers--;
            if (builder != null)
            {
                activeBuilders.Remove(builder);
                Destroy(builder.gameObject);
            }
            UpdateVillagerVisibility();
            Changed?.Invoke();
        }

        public bool TryAssignHarvester(ResourceCamp camp, int slotIndex, out HarvesterUnit harvester)
        {
            harvester = null;
            if (FreePopulation <= 0 || camp == null) return false;

            GameObject prefab = lumberjackPrefab;
            string unitName = "Lumberjack";
            if (camp.ResourceType == ResourceType.Stone || camp.ResourceType == ResourceType.Iron)
            {
                prefab = minerPrefab;
                unitName = camp.ResourceType == ResourceType.Iron ? "IronMiner" : "Miner";
            }
            else if (camp.ResourceType == ResourceType.Food)
            {
                prefab = farmerPrefab;
                unitName = "Farmer";
            }
            if (prefab == null) prefab = builderPrefab;
            if (prefab == null) return false;

            AssignedWorkers++;
            var go = Instantiate(prefab, camp.GetDropOffPosition(slotIndex), Quaternion.identity, transform);
            go.name = unitName;
            harvester = go.GetComponent<HarvesterUnit>();
            if (harvester == null) harvester = go.AddComponent<HarvesterUnit>();
            harvester.Setup(camp, slotIndex);
            activeHarvesters.Add(harvester);

            UpdateVillagerVisibility();
            Changed?.Invoke();
            return true;
        }

        public void UnassignHarvester(HarvesterUnit harvester)
        {
            if (AssignedWorkers > 0) AssignedWorkers--;
            if (harvester != null)
            {
                activeHarvesters.Remove(harvester);
                Destroy(harvester.gameObject);
            }
            UpdateVillagerVisibility();
            Changed?.Invoke();
        }

        GameObject SpawnVillagerVisual(Vector3 homePos, int index)
        {
            if (villagerPrefab == null) return null;
            var grid = BuildGrid.Instance;
            Vector3 pos = homePos;
            float baseAngle = index * 2.1f;

            for (int attempt = 0; attempt < 8; attempt++)
            {
                float angle = baseAngle + attempt * (Mathf.PI * 0.25f);
                var candidate = homePos + new Vector3(Mathf.Cos(angle) * 1.4f, 0f, Mathf.Sin(angle) * 1.4f);
                if (grid == null)
                {
                    pos = candidate;
                    break;
                }
                candidate.y = grid.SampleWorldHeight(candidate);
                var cell = grid.MinCell(candidate, Vector2Int.one);
                if (grid.InBounds(cell)
                    && grid.GetOccupancy(cell) == CellOccupancy.None
                    && grid.Terrain[grid.Index(cell)] == CellState.Free
                    && Mathf.Abs(candidate.y - homePos.y) <= 0.65f)
                {
                    pos = candidate;
                    break;
                }
                if (attempt == 0) pos = candidate;
            }

            var go = Instantiate(villagerPrefab, pos, Quaternion.identity, transform);
            go.name = "Villager";
            var unit = go.GetComponent<VillagerUnit>();
            if (unit == null) unit = go.AddComponent<VillagerUnit>();
            unit.Setup(pos);
            return go;
        }

        BuilderUnit SpawnBuilderUnit(Transform homeTransform)
        {
            if (builderPrefab == null) return null;
            var go = Instantiate(builderPrefab, homeTransform.position, Quaternion.identity, transform);
            go.name = "Builder";
            var unit = go.GetComponent<BuilderUnit>();
            if (unit == null) unit = go.AddComponent<BuilderUnit>();
            unit.Setup(homeTransform, nextSlot++);
            activeBuilders.Add(unit);
            return unit;
        }

        void UpdateVillagerVisibility()
        {
            int free = FreePopulation;
            for (int i = 0; i < freeVillagerVisuals.Count; i++)
            {
                if (freeVillagerVisuals[i] != null)
                {
                    freeVillagerVisuals[i].SetActive(i < free);
                }
            }
        }
    }
}
