using Unity.Mathematics;

namespace SurvivalRts
{
    public enum SoundId : byte
    {
        None,
        EnemyHit,
        EnemyDeath,
        TowerShot,
        BuildingCollapse,
        CrowdMarch,
        Battle,
        UiClick,
        UiPlace,
        AlertHq,
        UiOpen,
        UiClose,
        MusicCalm,
        MusicBattle,
        BuilderHammer,
        SwordHit,
        BowShot
    }

    // Sự kiện âm thanh do gameplay (kể cả job Burst) phát ra; AudioDirector quyết định có phát thành tiếng hay không.
    public struct AudioEvent
    {
        public SoundId Id;
        public float3 Position;
        public float Parameter;
    }
}
