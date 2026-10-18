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
            float angle = index * 2.1f;
            var pos = homePos + new Vector3(Mathf.Cos(angle) * 1.4f, 0f, Mathf.Sin(angle) * 1.4f);
            var go = Instantiate(villagerPrefab, pos, Quaternion.identity, transform);
            go.name = "Villager";
            var unit = go.GetComponent<VillagerUnit>();
            if (unit == null) unit = go.AddComponent<VillagerUnit>();
            unit.Setup(homePos);
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
