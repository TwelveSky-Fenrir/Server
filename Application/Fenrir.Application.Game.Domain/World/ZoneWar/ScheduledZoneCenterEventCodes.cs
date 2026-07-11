using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public static class ScheduledZoneCenterEventCodes
{

        public const int Zone038WinEventCode = 38;

        public const int HsbRewardFlagResetPingEventCode = 1601;

        public const int DailyResetEventCode = 1600;

        public const short Zone039MonsterSummonResetMapId = 74;

        public static readonly FrozenSet<short> Zone039ArmingGatedMapIds =
        new short[] { 39, 144, 145, 313, 74 }.ToFrozenSet();
}
