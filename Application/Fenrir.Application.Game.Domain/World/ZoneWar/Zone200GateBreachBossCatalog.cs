using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public static class Zone200GateBreachBossCatalog
{

        public static readonly ImmutableArray<short> EligibleServerNumbers = [200, 297, 298, 299];

        public const int BossMonsterId = 756;

        public const int KillQuotaPerTribe = 170;

        public const int GateBreachToKillRaceClockTicks = 3600;

        public const float SummonX = 16f;

        public const float SummonY = 264f;

        public const float SummonZ = 7650f;

        public const bool ExistenceCheckActive = true;

        public const int PostSummonBossDeathPollTimeoutTicks = 3600;

        public static readonly ImmutableArray<int> BattleWinBonusFixedItemIds = [1072, 1103, 1449, 1422, 1145, 2249, 602];
}
