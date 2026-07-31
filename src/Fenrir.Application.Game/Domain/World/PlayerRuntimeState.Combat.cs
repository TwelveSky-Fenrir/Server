using Fenrir.Core.Packets.Shared;

namespace Fenrir.Application.Game.Domain.World;

public partial class PlayerRuntimeState
{
    public (int X, int Z) CurrentCell { get; set; }

    public DateTime LastMoveUtc { get; set; }

    public TimeSpan LastAvatarRebroadcastAt { get; set; }

    public uint UniqueNumber => unchecked((uint)CharacterId);

    public bool IsDead { get; set; }

    public int TicksSinceDeath { get; set; }

    public bool ReviveHackFlag { get; set; }

    public bool IsMovingZone { get; set; }

    public DateTime ZoneTransferRegisteredAtUtc { get; set; }

    public int DeathSubCounter { get; set; }

    public int ActionSort { get; set; }

    public int ActionType { get; set; }

    public int ActionSkillNumber { get; set; }

    public int ActionSkillGradeNum1 { get; set; }
    public int ActionSkillGradeNum2 { get; set; }

    public bool AttackBudgetEnforced { get; set; } = true;

    public int AttackFamilyTag { get; set; }

    public int AttackSubPacketCeiling { get; set; }

    public int AttackSubPacketsUsed { get; set; }

    public BuffInfo Buffs { get; } = new() { Buff = new int[70] };

    public int[] BuffChangeScratch { get; } = new int[35];

    public TimeSpan? ZoneEntryAtZoneClock { get; set; }

    public DateTime LastHolyShieldAppliedUtc { get; set; } = DateTime.MinValue;

    public bool IsStunned { get; set; }

    public int StunDurationSeconds { get; set; }

    public long LastOneSecondGateTick { get; set; }

    public int OneSecondGateOpenCount { get; set; }

    public int RepeatedStunCount { get; set; }

    public bool CanUseConsumables { get; set; } = true;

    public bool IsUnderDarkAttackPotionDebuff { get; set; }

    public DateTime DarkAttackDebuffActivatedAtUtc { get; set; }
}
