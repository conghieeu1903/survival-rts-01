---
trigger: always_on
---

quy tắc đóng gói các module và thành phần trong scene thành Prefab:

- mọi module hệ thống trong scene (như `MainCamera`, `AudioDirector`, `TracerPool`, `BuildGrid`, `BuildingPlacer`, `BuildingSelector`, `PopulationManager`, `EnemySpawn`, `EnemyEcs`, `Battle`...) bắt buộc phải đóng gói thành Prefab trong `Assets/Prefabs/Gameplay/`, không để GameObject rời trực tiếp trong scene.

- mọi màn hình giao diện trong scene (`BuildMenu`, `HealthBar`, `UnitCommand`, `SettingsMenu`, `BattleUi`...) bắt buộc đóng gói thành Prefab trong `Assets/Prefabs/UI/Screens/`.

- các module dùng chung giữa nhiều scene (như `MainCamera.prefab`, `AudioDirector.prefab`, `TracerPool.prefab`) phải tái sử dụng cùng một Prefab gốc dưới dạng Prefab instance trong từng scene, chỉ ghi đè (override) các thông số đặc thù của scene đó (như `boundsHalfSize`, `zoomRange`, vị trí đặt camera...).

- toàn bộ camera trong các scene gameplay bắt buộc dùng chế độ `Perspective` (`orthographic = false`).

- khi thêm module mới hoặc cập nhật Prefab/script của module, bắt buộc cập nhật và lưu lại trên tất cả các scene gameplay liên quan (`Assets/Scenes/prototype.unity` và `Assets/Scenes/battle.unity`), đồng thời kiểm tra không để xảy ra lỗi `Missing Prefab` hoặc `Missing Reference` giữa các Prefab instance trong scene.
