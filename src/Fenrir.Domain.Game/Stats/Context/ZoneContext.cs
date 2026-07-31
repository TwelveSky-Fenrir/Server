namespace Fenrir.Domain.Game.Stats.Context;

public readonly record struct ZoneContext(
    short ZoneNumber = 0,
    bool OrnamentInUse = false,
    int OrnamentGoldTimeRemaining = 0,
    int OrnamentSilverTimeRemaining = 0,
    int RankBuffType = 0,
    byte TribeRole = 0,
    int RageGauge = 0,
    int DrunkStateId = 0,
    bool GuildBuffActive = false,
    int GuildId = 0);
