using System;
using UnityEngine;

namespace SurvivalRts
{
    // Bot điều quân cho đội của máy này: định kỳ dồn cả quân về tiểu đội địch gần nhất và huấn luyện thêm khi đủ vàng.
    // Chỉ điều đội người chơi của máy này; đội máy tính đã có AI trong mô phỏng.
    // Bật bằng tham số dòng lệnh -bot hoặc gán Active; chỉ gửi lệnh qua lockstep như người chơi thật.
    [RequireComponent(typeof(BattleSimulation))]
    public class BattleBot : MonoBehaviour
    {
        [Tooltip("Số lượt giữa hai lần bot ra lệnh (10 lượt = 1 giây)")]
        [SerializeField] int ordersEveryTurns = 20;

        BattleSimulation simulation;
        uint lastOrderTurn;

        public bool Active { get; set; }

        void Awake()
        {
            simulation = GetComponent<BattleSimulation>();
            foreach (var arg in Environment.GetCommandLineArgs())
                if (arg == "-bot") Active = true;
        }

        void Update()
        {
            var net = LockstepNetwork.Instance;
            if (!Active || net == null || !net.InMatch || !simulation.Started || simulation.LocalTeam < 0) return;
            if (simulation.IsAiTeam(simulation.LocalTeam)) return;
            if (simulation.WinnerTeam >= 0 || net.CurrentTurn < lastOrderTurn + ordersEveryTurns) return;
            lastOrderTurn = net.CurrentTurn;

            TryTrain();
            if (!TryArmyCenter(simulation.LocalTeam, out var own)) return;
            if (!TryNearestEnemy(own, out var destination)) return;

            net.SendCommand(new LockstepCommand
            {
                Type = BattleCommand.MoveArmy,
                X = Mathf.RoundToInt(destination.x * 100f),
                Z = Mathf.RoundToInt(destination.y * 100f)
            });
        }

        // Luân phiên các loại lính, chỉ gửi lệnh khi đủ vàng.
        void TryTrain()
        {
            int types = simulation.Data.unitTypes.Length;
            int type = (int)(lastOrderTurn / (uint)ordersEveryTurns) % types;
            if (simulation.TeamGold(simulation.LocalTeam) < simulation.TrainCost(type)) return;
            LockstepNetwork.Instance.SendCommand(new LockstepCommand { Type = BattleCommand.Train, Param = type });
        }

        bool TryArmyCenter(int team, out Vector2 center)
        {
            center = Vector2.zero;
            int total = 0;
            for (int s = 0; s < simulation.SquadsPerTeam; s++)
            {
                if (!simulation.TryGetSquad(team, s, out var squad, out int alive)) continue;
                center += squad * alive;
                total += alive;
            }
            if (total == 0) return false;
            center /= total;
            return true;
        }

        bool TryNearestEnemy(Vector2 from, out Vector2 nearest)
        {
            nearest = default;
            float best = float.MaxValue;
            for (int team = 0; team < BattleSimulation.TeamCount; team++)
            {
                if (team == simulation.LocalTeam) continue;
                for (int s = 0; s < simulation.SquadsPerTeam; s++)
                {
                    if (!simulation.TryGetSquad(team, s, out var squad, out _)) continue;
                    float distance = (squad - from).sqrMagnitude;
                    if (distance >= best) continue;
                    best = distance;
                    nearest = squad;
                }
            }
            return best < float.MaxValue;
        }
    }
}
