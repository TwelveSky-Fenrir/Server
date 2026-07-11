using Fenrir.Application.Game.Domain.AntiCheat;

namespace Fenrir.Application.Game.Domain.World;

public partial class PlayerRuntimeState
{

        public PlayerCadenceTimers Cadences;

        public string? SourceIp { get; init; }


        public int DarkAttackUseTick { get; set; }

        public int DarkAttackActiveTick { get; set; }

        public int DarkAttackKind { get; set; }

        public int HitRateTick { get; set; }

        public int HitRateKind { get; set; }

        public int DodgeRateTick { get; set; }

        public int DodgeRateKind { get; set; }

        public int LastUsedSkillIndex { get; set; }

        public int LastUsedSkillTick { get; set; }

        public int LastUsedSkillWarning { get; set; }

        public int LastUsedSkillTotalWarning { get; set; }

        public int SameSpotCount { get; set; }

        public int BottleTick { get; set; }

        public bool AntiAhsHackFlag { get; set; }

        public int AutoCheckState { get; set; }

        public int AutoCheckAnswer1 { get; set; }

        public int AutoCheckAnswer2 { get; set; }

        public int AutoCheckTime { get; set; }

        public int AttackHitTime1 { get; set; }

        public int AttackHitTime2 { get; set; }

        public TimeSpan CpExchangeTick { get; set; }

        public TimeSpan CpRfcTick { get; set; }

        public void ResetVolatileAntiCheatCountersOnEntry(TimeSpan zoneClockNow)
    {
        DarkAttackUseTick = 0;
        DarkAttackActiveTick = 0;
        DarkAttackKind = 0;
        HitRateTick = 0;
        HitRateKind = 0;
        DodgeRateTick = 0;
        DodgeRateKind = 0;
        LastUsedSkillIndex = 0;
        LastUsedSkillTick = 0;
        LastUsedSkillWarning = 0;
        LastUsedSkillTotalWarning = 0;
        SameSpotCount = 0;
        BottleTick = 0;
        AntiAhsHackFlag = false;
        AutoCheckState = 0;
        AutoCheckAnswer1 = 0;
        AutoCheckAnswer2 = 0;
        AutoCheckTime = 0;
        AttackHitTime1 = 0;
        AttackHitTime2 = 0;

        CpExchangeTick = zoneClockNow;
        CpRfcTick = zoneClockNow;
        Cadences.ResetAll(zoneClockNow);
    }
}
