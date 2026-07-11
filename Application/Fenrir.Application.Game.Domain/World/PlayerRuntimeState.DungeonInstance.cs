namespace Fenrir.Application.Game.Domain.World;

public enum DungeonInstanceLifecycle
{

        Idle = 0,

        Waiting = 1,

        Summoning = 2,

        BattleInProgress = 3,

        Success = 4,

        Failure = 5,

        WaitBeforeReturn = 6,

        ReturnToTownNotice = 7,

        DisconnectPending = 8
}

public partial class PlayerRuntimeState
{

        public int? DungeonInstanceId { get; set; }

        public DungeonInstanceLifecycle DungeonInstanceLifecycleState { get; set; } = DungeonInstanceLifecycle.Idle;

        public int DungeonInstanceTick { get; set; }

        public int DungeonInstanceRoundsRemaining { get; set; }
}
