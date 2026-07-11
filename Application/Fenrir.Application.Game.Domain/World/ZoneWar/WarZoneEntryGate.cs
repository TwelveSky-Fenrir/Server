namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>What <see cref="WarZoneEntryGate.Evaluate" /> decided for one registration/transfer attempt.</summary>
public enum WarZoneEntryOutcome
{
    /// <summary>Either the zone is not one <see cref="WarZoneEntryCatalog" /> restricts, or the avatar qualifies.</summary>
    Allowed,

    /// <summary>Combined level and/or rebirth count falls outside the zone's rule -- a hard eviction, never a soft redirect.</summary>
    RejectedOutOfRange
}

/// <summary>
///     Server-authoritative registration/transfer eligibility check for the war zones enumerated in
///     <see cref="WarZoneEntryCatalog" /> -- the C# analog of the live <c>CheckRegisterAvatar</c> gate
///     (Server/ts25zone/S04_MyWork02.cpp:902, S07_MyGame03.cpp:5946). Evaluated at every point an avatar
///     registers into a zone process: initial world entry
///     (<c>Fenrir.Application.Game.Services.ZoneLifecycle.EnterWorldService</c>) and zone-to-zone transfer,
///     same-shard or cross-shard (<c>Fenrir.Application.Game.Services.ZoneLifecycle.ZoneMoveService</c>).
/// </summary>
/// <remarks>
///     A denial is a hard disconnect ("out-of-range eviction"), never a soft redirect -- matching legacy's own
///     posture (Server/ts25zone/S04_MyWork02.cpp:902-905, "session is disconnected... a hard drop, not a
///     redirect to another zone") and this codebase's existing "any violation ends the socket" pattern for
///     anti-cheat/trust gates in the same handler family (<see cref="TribeGuardCorridorGate" />'s
///     RejectedHardDisconnect, <see cref="TribeSymbolBattleZoneLockout" />).
/// </remarks>
public static class WarZoneEntryGate
{
    /// <summary>
    ///     <paramref name="combinedLevel" /> is grade portion plus base-level portion added together
    ///     (<see cref="PlayerRuntimeState.CombinedLevel" /> for a live in-zone player, or
    ///     <c>character.Level + character.Level2</c> for a not-yet-registered persisted record); rebirth count
    ///     is read directly off the avatar record either way (contract Inputs).
    /// </summary>
    public static WarZoneEntryOutcome Evaluate(short zoneNumber, int combinedLevel, int rebirthCount)
    {
        if (!WarZoneEntryCatalog.TryGetRule(zoneNumber, out var rule))
            return WarZoneEntryOutcome.Allowed; // not one of the enumerated war zones -- unrestricted by this gate

        var levelOk = combinedLevel >= rule.MinCombinedLevel && combinedLevel <= rule.MaxCombinedLevel;
        var rebirthOk = rebirthCount >= rule.MinRebirthCount && rebirthCount <= rule.MaxRebirthCount;

        return levelOk && rebirthOk ? WarZoneEntryOutcome.Allowed : WarZoneEntryOutcome.RejectedOutOfRange;
    }
}
