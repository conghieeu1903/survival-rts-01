using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace SurvivalRts
{
    // Lệnh điều quân gửi qua lockstep.
    public static class BattleCommand
    {
        public const ushort MoveSquad = 1; // Param = tiểu đội trong đội, (X, Z) = đích
        public const ushort MoveArmy = 2;  // cả đội dồn về (X, Z)
        public const ushort Train = 3;     // Param = chỉ số loại lính trong BattleData.unitTypes; trừ vàng, sau thời gian huấn luyện có tiểu đội mới
        public const ushort PlayerLeft = 100; // host chèn khi người chơi của Team rời trận; đội đó chuyển cho máy tính điều khiển
    }

    // Mô phỏng trận nhiều đội theo lockstep: toàn bộ lính nằm trong NativeArray, mỗi bước chạy job Burst
    // chỉ dùng số nguyên cố định nên mọi máy ra cùng kết quả. Hiển thị tách riêng ở BattleUnitPresentation.
    public class BattleSimulation : MonoBehaviour, ILockstepSimulation
    {
        public const int TeamCount = 4;
        public const int StepsPerTurn = 2;
        public const float StepSeconds = LockstepNetwork.TurnInterval / StepsPerTurn;

        const int CellMeters = 4;
        const int RetargetSteps = 8;
        const int MaxEffectsPerStep = 96;
        const int AiIntervalTurns = 20; // AI ra lệnh mỗi chừng này lượt, lệch pha giữa các đội
        const int AiPhaseTurns = 5;
        const int AiTrainPerOrder = 2;

        // trạng thái chỗ tiểu đội trong mảng squadStatus
        const byte SquadFree = 0;   // chỗ dự trữ trống
        const byte SquadQueued = 1; // đang huấn luyện
        const byte SquadActive = 2; // đã xuất hiện

        public static BattleSimulation Current { get; private set; }

        [Header("Dữ liệu")]
        [Tooltip("Cấu hình quân số, chỉ số lính và hiển thị của trận")]
        [SerializeField] BattleData data;

        BattleStats stats;
        int unitCount;
        int unitsPerTeam;
        int squadsPerTeam;  // tổng chỗ tiểu đội mỗi đội, gồm cả dự trữ
        int initialSquads;  // số tiểu đội có sẵn lúc bắt đầu, nằm đầu mỗi đội
        int reservePerType; // chỗ dự trữ cho mỗi loại lính
        int turnsPerSecond;
        int stepIndex;
        int turnIndex;
        bool ended;
        int localTeam = -1;
        double turnAppliedTime;
        bool started;

        // trạng thái hiện tại, hoán đổi với bộ Next sau mỗi bước
        NativeArray<int> posX, posZ, nextX, nextZ;
        NativeArray<int> health, target, timer, damageTarget;
        NativeArray<byte> state;
        NativeArray<byte> unitType;
        NativeArray<BattleUnitStats> typeStats;
        byte[] squadType;
        NativeArray<int> slotX, slotZ;
        NativeArray<int> squadDestX, squadDestZ;
        // vị trí trước lượt vừa chạy, dùng nội suy hiển thị
        NativeArray<int> prevX, prevZ;
        NativeArray<int> cellStart, sorted, fill;
        NativeArray<long> squadSumX, squadSumZ;
        NativeArray<int> squadAlive;
        NativeArray<uint> checksum;
        // kinh tế và thống kê, mỗi đội một phần tử
        NativeArray<int> gold, goldFrac, kills, losses;
        NativeArray<byte> aiTeam;
        // mỗi chỗ tiểu đội: trạng thái và lượt hoàn tất huấn luyện
        NativeArray<byte> squadStatus;
        NativeArray<int> squadReady;
        int[] squadHomeX, squadHomeZ;
        int[] trainCost, trainTurns, trainSize;

        public BattleData Data => data;
        public bool Started => started;
        public int UnitCount => unitCount;
        public int UnitsPerTeam => unitsPerTeam;
        public int SquadsPerTeam => squadsPerTeam;
        public int LocalTeam => localTeam;
        public BattleStats Stats => stats;
        public double TurnAppliedTime => turnAppliedTime;

        // Đội thắng khi chỉ còn một đội có lính; -1 nếu trận chưa phân thắng bại.
        public int WinnerTeam { get; private set; } = -1;

        public NativeArray<int> PosX => posX;
        public NativeArray<int> PosZ => posZ;
        public NativeArray<int> PrevX => prevX;
        public NativeArray<int> PrevZ => prevZ;
        public NativeArray<int> Health => health;
        public NativeArray<int> Target => target;
        public NativeArray<byte> State => state;
        public NativeArray<byte> UnitType => unitType;

        // Vùng mặt đất (x, z tính bằng mét) mà hình ảnh và âm thanh đòn đánh được báo; ngoài vùng này bị bỏ để khỏi tốn.
        public Rect EffectArea { get; set; } = new Rect(-100000f, -100000f, 200000f, 200000f);

        public event Action MatchStarted;

        // Bắn một lần khi trận có đội thắng.
        public event Action<int> MatchEnded;

        // Một phát bắn tầm xa vừa diễn ra, vị trí tính bằng mét, dùng để vẽ mũi tên.
        public event Action<Vector3, Vector3> ShotFired;

        // Một đòn cận chiến trúng mục tiêu (vị trí mục tiêu) và một lính vừa bị hạ.
        public event Action<Vector3> MeleeHit;
        public event Action<Vector3> UnitKilled;

        void Awake()
        {
            Current = this;
        }

        void OnEnable()
        {
            Register();
        }

        // Thứ tự Awake giữa các component không cố định nên đăng ký lại ở Start.
        void Start()
        {
            Register();
        }

        void Register()
        {
            var net = LockstepNetwork.Instance;
            if (net != null) net.Simulation = this;
        }

        void OnDisable()
        {
            var net = LockstepNetwork.Instance;
            if (net != null && ReferenceEquals(net.Simulation, this)) net.Simulation = null;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
            Release();
        }

        public void StartMatch(int seed, int teamCount, int team, int aiMask)
        {
            if (data == null)
            {
                Debug.LogError("BattleSimulation chưa gán BattleData");
                return;
            }

            Release();
            localTeam = team;
            stepIndex = 0;
            turnIndex = 0;
            ended = false;
            WinnerTeam = -1;
            turnsPerSecond = Mathf.Max(1, Mathf.RoundToInt(1f / LockstepNetwork.TurnInterval));
            initialSquads = data.SquadsPerTeam;
            reservePerType = Mathf.Max(0, data.reserveSquadsPerTeam);
            squadsPerTeam = data.SquadCapacity;
            unitsPerTeam = squadsPerTeam * data.squadSize;
            unitCount = unitsPerTeam * TeamCount;
            BuildStats();
            Allocate();
            Spawn();
            for (int t = 0; t < TeamCount; t++)
            {
                gold[t] = Mathf.Max(0, data.startGold);
                // đội không có người chơi (hoặc nằm ngoài số đội của trận) do máy tính điều khiển, trừ đội của máy này
                bool ai = ((aiMask >> t) & 1) != 0 || t >= teamCount;
                aiTeam[t] = (byte)(ai && t != team ? 1 : 0);
            }
            started = true;
            turnAppliedTime = Time.timeAsDouble;
            RefreshSquads();
            MatchStarted?.Invoke();
        }

        void BuildStats()
        {
            int cell = CellMeters * BattleFixed.One;
            int mapHalf = BattleFixed.FromMeters(data.mapHalfSize);
            stats = new BattleStats
            {
                MapHalf = mapHalf,
                CellSize = cell,
                GridWidth = (2 * mapHalf + cell - 1) / cell + 1,
                RetargetInterval = RetargetSteps,
                ArriveRadius = BattleFixed.FromMeters(0.3f)
            };

            typeStats = new NativeArray<BattleUnitStats>(data.unitTypes.Length, Allocator.Persistent);
            for (int t = 0; t < data.unitTypes.Length; t++)
            {
                var type = data.unitTypes[t];
                int strikeSteps = Mathf.Max(1, Mathf.RoundToInt(type.strikeDelay / StepSeconds));
                int cooldownSteps = Mathf.Max(strikeSteps + 1, Mathf.RoundToInt(type.attackInterval / StepSeconds));
                typeStats[t] = new BattleUnitStats
                {
                    MaxHealth = type.maxHealth,
                    Damage = type.damage,
                    CooldownSteps = cooldownSteps,
                    HitMark = cooldownSteps - strikeSteps,
                    AttackRange = BattleFixed.FromMeters(type.attackRange),
                    DetectRange = BattleFixed.FromMeters(type.detectRange),
                    Speed = Mathf.Max(1, BattleFixed.FromMeters(type.moveSpeed * StepSeconds)),
                    SeparationRadius = BattleFixed.FromMeters(type.radius * 2f)
                };
            }

            trainCost = new int[data.unitTypes.Length];
            trainTurns = new int[data.unitTypes.Length];
            trainSize = new int[data.unitTypes.Length];
            for (int t = 0; t < data.unitTypes.Length; t++)
            {
                var type = data.unitTypes[t];
                trainCost[t] = Mathf.Max(0, type.cost);
                trainTurns[t] = Mathf.Max(1, Mathf.RoundToInt(type.trainSeconds * turnsPerSecond));
                trainSize[t] = type.trainSquadSize > 0 && type.trainSquadSize < data.squadSize ? type.trainSquadSize : data.squadSize;
            }
        }

        void Allocate()
        {
            int cells = stats.GridWidth * stats.GridWidth;
            int squads = squadsPerTeam * TeamCount;
            posX = new NativeArray<int>(unitCount, Allocator.Persistent);
            posZ = new NativeArray<int>(unitCount, Allocator.Persistent);
            nextX = new NativeArray<int>(unitCount, Allocator.Persistent);
            nextZ = new NativeArray<int>(unitCount, Allocator.Persistent);
            prevX = new NativeArray<int>(unitCount, Allocator.Persistent);
            prevZ = new NativeArray<int>(unitCount, Allocator.Persistent);
            health = new NativeArray<int>(unitCount, Allocator.Persistent);
            target = new NativeArray<int>(unitCount, Allocator.Persistent);
            timer = new NativeArray<int>(unitCount, Allocator.Persistent);
            damageTarget = new NativeArray<int>(unitCount, Allocator.Persistent);
            state = new NativeArray<byte>(unitCount, Allocator.Persistent);
            unitType = new NativeArray<byte>(unitCount, Allocator.Persistent);
            slotX = new NativeArray<int>(unitCount, Allocator.Persistent);
            slotZ = new NativeArray<int>(unitCount, Allocator.Persistent);
            squadDestX = new NativeArray<int>(squads, Allocator.Persistent);
            squadDestZ = new NativeArray<int>(squads, Allocator.Persistent);
            squadSumX = new NativeArray<long>(squads, Allocator.Persistent);
            squadSumZ = new NativeArray<long>(squads, Allocator.Persistent);
            squadAlive = new NativeArray<int>(squads, Allocator.Persistent);
            cellStart = new NativeArray<int>(cells + 1, Allocator.Persistent);
            fill = new NativeArray<int>(cells, Allocator.Persistent);
            sorted = new NativeArray<int>(unitCount, Allocator.Persistent);
            checksum = new NativeArray<uint>(1, Allocator.Persistent);
            gold = new NativeArray<int>(TeamCount, Allocator.Persistent);
            goldFrac = new NativeArray<int>(TeamCount, Allocator.Persistent);
            kills = new NativeArray<int>(TeamCount, Allocator.Persistent);
            losses = new NativeArray<int>(TeamCount, Allocator.Persistent);
            aiTeam = new NativeArray<byte>(TeamCount, Allocator.Persistent);
            squadStatus = new NativeArray<byte>(squads, Allocator.Persistent);
            squadReady = new NativeArray<int>(squads, Allocator.Persistent);
            squadHomeX = new int[squads];
            squadHomeZ = new int[squads];
        }

        // Mỗi đội chiếm một góc bản đồ, tiểu đội xếp thành khối nhìn vào tâm; loại lính đầu danh sách đứng hàng trước.
        // Tiểu đội dự trữ xếp phía sau khối, lính của chúng ở trạng thái Inactive tới khi huấn luyện xong.
        void Spawn()
        {
            int spacing = BattleFixed.FromMeters(data.formationSpacing);
            int width = Mathf.CeilToInt(Mathf.Sqrt(data.squadSize));
            int squadExtent = width * spacing;
            int pitch = squadExtent + BattleFixed.FromMeters(4f);
            int cols = Mathf.CeilToInt(Mathf.Sqrt(initialSquads * 1.6f));
            int rows = (initialSquads + cols - 1) / cols;
            int cornerX = stats.MapHalf * 11 / 20;

            // loại lính theo hạng từ hàng trước ra sau
            var typeByRank = new byte[initialSquads];
            int rank = 0;
            for (int t = 0; t < data.unitTypes.Length; t++)
                for (int k = 0; k < data.unitTypes[t].squadCount; k++) typeByRank[rank++] = (byte)t;
            squadType = new byte[squadsPerTeam * TeamCount];

            for (int team = 0; team < TeamCount; team++)
            {
                int signX = team == 1 || team == 2 ? 1 : -1;
                int signZ = team >= 2 ? 1 : -1;
                int centerX = signX * cornerX;
                int centerZ = signZ * cornerX;

                for (int squad = 0; squad < squadsPerTeam; squad++)
                {
                    bool initial = squad < initialSquads;
                    int reserveIndex = squad - initialSquads;
                    int col = initial ? squad % cols : reserveIndex % cols;
                    // dự trữ nằm sau khối: phía xa tâm bản đồ theo trục z
                    int row = initial ? squad / cols : signZ < 0 ? -1 - reserveIndex / cols : rows + reserveIndex / cols;
                    int sx = centerX + (col - (cols - 1) / 2) * pitch;
                    int sz = centerZ + (row - (rows - 1) / 2) * pitch;
                    int global = team * squadsPerTeam + squad;
                    squadDestX[global] = sx;
                    squadDestZ[global] = sz;
                    squadHomeX[global] = sx;
                    squadHomeZ[global] = sz;
                    squadStatus[global] = initial ? SquadActive : SquadFree;

                    // hàng gần tâm bản đồ nhất (theo trục z) có hạng thấp nhất
                    int frontRow = signZ < 0 ? rows - 1 - row : row;
                    byte type = initial
                        ? typeByRank[Mathf.Min(frontRow * cols + col, initialSquads - 1)]
                        : (byte)(reserveIndex / reservePerType);
                    squadType[global] = type;

                    for (int k = 0; k < data.squadSize; k++)
                    {
                        int i = team * unitsPerTeam + squad * data.squadSize + k;
                        int ox = (k % width - (width - 1) / 2) * spacing;
                        int oz = (k / width - (width - 1) / 2) * spacing;
                        slotX[i] = ox;
                        slotZ[i] = oz;
                        posX[i] = sx + ox;
                        posZ[i] = sz + oz;
                        unitType[i] = type;
                        health[i] = initial ? typeStats[type].MaxHealth : 0;
                        state[i] = initial ? BattleState.Idle : BattleState.Inactive;
                        target[i] = -1;
                        damageTarget[i] = -1;
                    }
                }
            }
            prevX.CopyFrom(posX);
            prevZ.CopyFrom(posZ);
        }

        public void ApplyTurn(uint turn, LockstepCommand[] commands)
        {
            if (!started) return;

            CompletePresentation();
            foreach (var command in commands) Execute(command);
            RunAi();
            RunEconomy();

            prevX.CopyFrom(posX);
            prevZ.CopyFrom(posZ);
            for (int s = 0; s < StepsPerTurn; s++) Step();
            RefreshSquads();
            turnIndex++;
            turnAppliedTime = Time.timeAsDouble;
        }

        void Execute(LockstepCommand command)
        {
            int team = command.Team;
            if (team < 0 || team >= TeamCount) return;

            if (command.Type == BattleCommand.PlayerLeft)
            {
                aiTeam[team] = 1;
                return;
            }

            int x = math.clamp(command.X * BattleFixed.One / 100, -stats.MapHalf, stats.MapHalf);
            int z = math.clamp(command.Z * BattleFixed.One / 100, -stats.MapHalf, stats.MapHalf);
            if (command.Type == BattleCommand.MoveSquad)
            {
                if (command.Param < 0 || command.Param >= squadsPerTeam) return;
                int squad = team * squadsPerTeam + command.Param;
                squadDestX[squad] = x;
                squadDestZ[squad] = z;
            }
            else if (command.Type == BattleCommand.MoveArmy)
            {
                for (int s = 0; s < squadsPerTeam; s++)
                {
                    squadDestX[team * squadsPerTeam + s] = x;
                    squadDestZ[team * squadsPerTeam + s] = z;
                }
            }
            else if (command.Type == BattleCommand.Train)
            {
                TryTrain(team, command.Param);
            }
        }

        // Xếp một tiểu đội vào hàng huấn luyện: trừ vàng và giữ một chỗ dự trữ; sai điều kiện thì bỏ qua.
        bool TryTrain(int team, int type)
        {
            if (WinnerTeam >= 0 || type < 0 || type >= trainCost.Length) return false;
            if (gold[team] < trainCost[type]) return false;

            int first = team * squadsPerTeam + initialSquads + type * reservePerType;
            for (int id = first; id < first + reservePerType; id++)
            {
                if (squadStatus[id] != SquadFree) continue;
                gold[team] -= trainCost[type];
                squadStatus[id] = SquadQueued;
                squadReady[id] = turnIndex + trainTurns[type];
                return true;
            }
            return false;
        }

        // Tiểu đội huấn luyện xong xuất hiện tại khu vực căn cứ của đội.
        void ActivateSquad(int team, int squad)
        {
            int id = team * squadsPerTeam + squad;
            int size = trainSize[squadType[id]];
            int maxHealth = typeStats[squadType[id]].MaxHealth;
            for (int k = 0; k < size; k++)
            {
                int i = team * unitsPerTeam + squad * data.squadSize + k;
                posX[i] = squadHomeX[id] + slotX[i];
                posZ[i] = squadHomeZ[id] + slotZ[i];
                prevX[i] = posX[i];
                prevZ[i] = posZ[i];
                health[i] = maxHealth;
                state[i] = BattleState.Idle;
                target[i] = -1;
                timer[i] = 0;
                damageTarget[i] = -1;
            }
            squadDestX[id] = squadHomeX[id];
            squadDestZ[id] = squadHomeZ[id];
            squadStatus[id] = SquadActive;
        }

        // Thu nhập vàng theo lượt (số nguyên, phần dư dồn lại) và hoàn tất các tiểu đội huấn luyện xong.
        void RunEconomy()
        {
            int perSecond = Mathf.Max(0, data.goldPerSecond);
            for (int team = 0; team < TeamCount; team++)
            {
                int total = goldFrac[team] + perSecond;
                gold[team] += total / turnsPerSecond;
                goldFrac[team] = total % turnsPerSecond;

                for (int squad = initialSquads; squad < squadsPerTeam; squad++)
                {
                    int id = team * squadsPerTeam + squad;
                    if (squadStatus[id] == SquadQueued && turnIndex >= squadReady[id]) ActivateSquad(team, squad);
                }
            }
        }

        // AI cho đội máy tính: định kỳ đưa từng tiểu đội về tiểu đội địch gần nhất, cung thủ giữ khoảng cách, và huấn luyện thêm khi đủ vàng.
        // Chỉ dùng số nguyên và dữ liệu mô phỏng nên mọi máy ra cùng lệnh.
        void RunAi()
        {
            if (WinnerTeam >= 0) return;
            for (int team = 0; team < TeamCount; team++)
            {
                if (aiTeam[team] == 0 || (turnIndex + team * AiPhaseTurns) % AiIntervalTurns != 0) continue;
                if (AliveInTeam(team) <= 0) continue;
                AiOrders(team);
                AiTrain(team);
            }
        }

        void AiOrders(int team)
        {
            for (int squad = 0; squad < squadsPerTeam; squad++)
            {
                int id = team * squadsPerTeam + squad;
                int alive = squadAlive[id];
                if (alive <= 0) continue;
                int cx = (int)(squadSumX[id] / alive);
                int cz = (int)(squadSumZ[id] / alive);

                long bestSq = long.MaxValue;
                int ex = 0;
                int ez = 0;
                for (int other = 0; other < TeamCount; other++)
                {
                    if (other == team) continue;
                    for (int s = 0; s < squadsPerTeam; s++)
                    {
                        int oid = other * squadsPerTeam + s;
                        int oAlive = squadAlive[oid];
                        if (oAlive <= 0) continue;
                        int ox = (int)(squadSumX[oid] / oAlive);
                        int oz = (int)(squadSumZ[oid] / oAlive);
                        long d = BattleFixed.DistSq(cx, cz, ox, oz);
                        if (d >= bestSq) continue;
                        bestSq = d;
                        ex = ox;
                        ez = oz;
                    }
                }
                if (bestSq == long.MaxValue) continue;

                int destX = ex;
                int destZ = ez;
                int type = squadType[id];
                if (data.unitTypes[type].ranged)
                {
                    // cung đứng cách đích 3/4 tầm bắn về phía quân mình, kiếm lao thẳng vào nên cung ở sau
                    int hold = typeStats[type].AttackRange * 3 / 4;
                    long dist = BattleFixed.Isqrt(bestSq);
                    if (dist > hold)
                    {
                        destX = ex + (int)((cx - ex) * (long)hold / dist);
                        destZ = ez + (int)((cz - ez) * (long)hold / dist);
                    }
                    else
                    {
                        destX = cx;
                        destZ = cz;
                    }
                }
                squadDestX[id] = math.clamp(destX, -stats.MapHalf, stats.MapHalf);
                squadDestZ[id] = math.clamp(destZ, -stats.MapHalf, stats.MapHalf);
            }
        }

        void AiTrain(int team)
        {
            int types = trainCost.Length;
            for (int n = 0; n < AiTrainPerOrder; n++)
            {
                bool trained = false;
                for (int k = 0; k < types && !trained; k++)
                {
                    int type = (turnIndex / AiIntervalTurns + team + n + k) % types;
                    trained = TryTrain(team, type);
                }
                if (!trained) break;
            }
        }

        void Step()
        {
            var grid = new BattleGridJob
            {
                Stats = stats,
                UnitCount = unitCount,
                PosX = posX,
                PosZ = posZ,
                Health = health,
                CellStart = cellStart,
                Sorted = sorted,
                Fill = fill
            }.Schedule();

            var move = new BattleStepJob
            {
                Stats = stats,
                StepIndex = stepIndex,
                UnitsPerTeam = unitsPerTeam,
                SquadSize = data.squadSize,
                SquadsPerTeam = squadsPerTeam,
                Types = typeStats,
                UnitType = unitType,
                PosX = posX,
                PosZ = posZ,
                Health = health,
                SlotX = slotX,
                SlotZ = slotZ,
                SquadDestX = squadDestX,
                SquadDestZ = squadDestZ,
                CellStart = cellStart,
                Sorted = sorted,
                NextX = nextX,
                NextZ = nextZ,
                Target = target,
                Timer = timer,
                DamageTarget = damageTarget,
                State = state
            }.Schedule(unitCount, 64, grid);

            new BattleDamageJob
            {
                UnitCount = unitCount,
                UnitsPerTeam = unitsPerTeam,
                Types = typeStats,
                UnitType = unitType,
                DamageTarget = damageTarget,
                Health = health,
                Kills = kills,
                Losses = losses
            }.Schedule(move).Complete();

            (posX, nextX) = (nextX, posX);
            (posZ, nextZ) = (nextZ, posZ);
            stepIndex++;
            if (ShotFired != null || MeleeHit != null || UnitKilled != null) EmitEffects();
        }

        // Báo các đòn đánh của bước vừa rồi cho hình ảnh và âm thanh; chỉ để trình diễn nên giới hạn số lượng.
        void EmitEffects()
        {
            const float Scale = 1f / BattleFixed.One;
            int emitted = 0;
            for (int i = 0; i < unitCount && emitted < MaxEffectsPerStep; i++)
            {
                int t = damageTarget[i];
                if (t < 0) continue;

                var from = new Vector3(posX[i] * Scale, 1.1f, posZ[i] * Scale);
                var to = new Vector3(posX[t] * Scale, 0.9f, posZ[t] * Scale);
                if (!EffectArea.Contains(new Vector2(from.x, from.z)) && !EffectArea.Contains(new Vector2(to.x, to.z))) continue;
                if (data.unitTypes[unitType[i]].ranged) ShotFired?.Invoke(from, to);
                else MeleeHit?.Invoke(to);

                // đòn vừa làm máu của mục tiêu về 0 hoặc thấp hơn
                if (health[t] <= 0 && health[t] + typeStats[unitType[i]].Damage > 0) UnitKilled?.Invoke(to);
                emitted++;
            }
        }

        void RefreshSquads()
        {
            new BattleSquadJob
            {
                UnitCount = unitCount,
                UnitsPerTeam = unitsPerTeam,
                SquadSize = data.squadSize,
                SquadsPerTeam = squadsPerTeam,
                PosX = posX,
                PosZ = posZ,
                Health = health,
                SumX = squadSumX,
                SumZ = squadSumZ,
                Alive = squadAlive
            }.Run();

            int teamsLeft = 0;
            int last = -1;
            for (int t = 0; t < TeamCount; t++)
            {
                if (AliveInTeam(t) <= 0) continue;
                teamsLeft++;
                last = t;
            }
            WinnerTeam = teamsLeft == 1 ? last : -1;

            // chỗ dự trữ có tiểu đội đã chết hết thì trả lại để huấn luyện tiếp
            for (int team = 0; team < TeamCount; team++)
                for (int squad = initialSquads; squad < squadsPerTeam; squad++)
                {
                    int id = team * squadsPerTeam + squad;
                    if (squadStatus[id] == SquadActive && squadAlive[id] <= 0) squadStatus[id] = SquadFree;
                }

            if (WinnerTeam >= 0 && !ended)
            {
                ended = true;
                MatchEnded?.Invoke(WinnerTeam);
            }
        }

        public uint ComputeChecksum()
        {
            if (!started) return 0;
            new BattleChecksumJob
            {
                UnitCount = unitCount,
                PosX = posX,
                PosZ = posZ,
                Health = health,
                Target = target,
                Timer = timer,
                State = state,
                SquadDestX = squadDestX,
                SquadDestZ = squadDestZ,
                SquadStatus = squadStatus,
                SquadReady = squadReady,
                Gold = gold,
                GoldFrac = goldFrac,
                Kills = kills,
                Losses = losses,
                AiTeam = aiTeam,
                Result = checksum
            }.Run();
            return checksum[0];
        }

        // Loại lính của tiểu đội (chỉ số trong BattleData.unitTypes).
        public int SquadType(int team, int squad)
        {
            int id = team * squadsPerTeam + squad;
            return started && squad >= 0 && squad < squadsPerTeam && id < squadType.Length ? squadType[id] : 0;
        }

        // Tâm tiểu đội (m) và số lính còn sống; false nếu tiểu đội đã hết.
        public bool TryGetSquad(int team, int squad, out Vector2 center, out int alive)
        {
            center = default;
            alive = 0;
            if (!started || team < 0 || team >= TeamCount || squad < 0 || squad >= squadsPerTeam) return false;
            int id = team * squadsPerTeam + squad;
            if (squadStatus[id] != SquadActive) return false;
            alive = squadAlive[id];
            if (alive <= 0) return false;
            center = new Vector2(
                BattleFixed.ToMeters((int)(squadSumX[id] / alive)),
                BattleFixed.ToMeters((int)(squadSumZ[id] / alive)));
            return true;
        }

        public int AliveInTeam(int team)
        {
            if (!started || team < 0 || team >= TeamCount) return 0;
            int total = 0;
            for (int s = 0; s < squadsPerTeam; s++) total += squadAlive[team * squadsPerTeam + s];
            return total;
        }

        public int TeamAlive(int team) => AliveInTeam(team);
        public int TeamKills(int team) => started && team >= 0 && team < TeamCount ? kills[team] : 0;
        public int TeamLosses(int team) => started && team >= 0 && team < TeamCount ? losses[team] : 0;
        public int TeamGold(int team) => started && team >= 0 && team < TeamCount ? gold[team] : 0;
        public bool IsAiTeam(int team) => started && team >= 0 && team < TeamCount && aiTeam[team] != 0;

        // Số tiểu đội của đội đang huấn luyện.
        public int TeamTraining(int team)
        {
            if (!started || team < 0 || team >= TeamCount) return 0;
            int total = 0;
            for (int s = initialSquads; s < squadsPerTeam; s++)
                if (squadStatus[team * squadsPerTeam + s] == SquadQueued) total++;
            return total;
        }

        // Giá vàng và thời gian huấn luyện (giây) của một loại lính, 0 nếu loại không hợp lệ.
        public int TrainCost(int type) => started && type >= 0 && type < trainCost.Length ? trainCost[type] : 0;
        public float TrainSeconds(int type) => started && type >= 0 && type < trainTurns.Length ? trainTurns[type] / (float)turnsPerSecond : 0f;

        // Job hiển thị đọc các mảng của mô phỏng, phải xong trước khi mô phỏng ghi hoặc giải phóng chúng.
        static void CompletePresentation()
        {
            var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;
            world.GetExistingSystemManaged<BattleUnitPresentation>()?.CompleteJobs();
        }

        void Release()
        {
            CompletePresentation();
            started = false;
            Dispose(ref posX); Dispose(ref posZ); Dispose(ref nextX); Dispose(ref nextZ);
            Dispose(ref prevX); Dispose(ref prevZ);
            Dispose(ref health); Dispose(ref target); Dispose(ref timer); Dispose(ref damageTarget);
            Dispose(ref state); Dispose(ref unitType); Dispose(ref typeStats);
            Dispose(ref slotX); Dispose(ref slotZ);
            Dispose(ref squadDestX); Dispose(ref squadDestZ);
            Dispose(ref squadSumX); Dispose(ref squadSumZ); Dispose(ref squadAlive);
            Dispose(ref cellStart); Dispose(ref fill); Dispose(ref sorted);
            Dispose(ref checksum);
            Dispose(ref gold); Dispose(ref goldFrac); Dispose(ref kills); Dispose(ref losses);
            Dispose(ref aiTeam); Dispose(ref squadStatus); Dispose(ref squadReady);
        }

        static void Dispose<T>(ref NativeArray<T> array) where T : struct
        {
            if (array.IsCreated) array.Dispose();
        }
    }
}
