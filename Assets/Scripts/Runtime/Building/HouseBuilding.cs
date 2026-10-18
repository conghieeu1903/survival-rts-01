using System.Collections.Generic;
using UnityEngine;

namespace SurvivalRts
{
    // Gắn lên Nhà dân (House) sau khi xây xong: cộng thêm dân và sinh nhân vật Villager quanh nhà.
    public class HouseBuilding : MonoBehaviour
    {
        int provided;
        List<GameObject> spawnedVillagers;

        public int Provided => provided;

        public void Setup(int populationCount)
        {
            provided = populationCount;
            if (PopulationManager.Instance != null)
            {
                var frontPos = transform.position + transform.forward * 1.6f;
                spawnedVillagers = PopulationManager.Instance.AddPopulation(provided, frontPos);
            }
        }

        void OnDestroy()
        {
            if (PopulationManager.Instance != null && provided > 0)
            {
                PopulationManager.Instance.RemovePopulation(provided, spawnedVillagers);
            }
        }
    }
}
