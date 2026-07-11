using Fenrir.Application.Game.Stats;

namespace Fenrir.Application.Game.Domain.Combat;

public readonly record struct CombatantSnapshot(
    int CharacterId,
    byte Tribe,
    bool IsDead,
    int Life,
    int MaxLife,
    float PosX,
    float PosY,
    float PosZ,

        TimeSpan? ZoneEntryAtZoneClock,
    EffectiveStats Stats,

        int ChargeBuffPercent,

        short Level = 0,

        bool IsMovingZone = false,

        string Name = "");
