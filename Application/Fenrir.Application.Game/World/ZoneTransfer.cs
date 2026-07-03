namespace Fenrir.Application.Game.World;

/// <summary>
///     In-process map transfer (ADR-0012, report 06 §6.2 step 4): a <c>Leave</c>-with-handoff is posted to the
///     SOURCE zone, whose own tick removes the player, snapshots the live state into a
///     <see cref="PlayerEnterData" /> and posts the matching <c>Enter</c> to the target — the state travels
///     INSIDE the command, is never referenced by two zones, and the character never exists in both at once.
///     The client keeps its TCP connection throughout (the whole legacy LP/ZP_MOVING_ZONE re-handshake
///     machinery has no equivalent here); the client-facing packet flow (DEMAND_ZONE_SERVER_INFO_2 tSort
///     routing, portal triggers) plugs onto <see cref="Request" /> in Phase C/V1 — this type is the mechanism
///     only.
/// </summary>
public static class ZoneTransfer
{
    /// <summary>
    ///     Requests the transfer of <paramref name="characterId" /> from <paramref name="source" /> to
    ///     <paramref name="target" />. Fire-and-forget like every zone command: false means the source inbox
    ///     dropped the write (overload), in which case nothing happened at all — the player simply stays where
    ///     they are, and the caller may retry or abort. Posting a character the source does not track is a
    ///     harmless no-op (the Leave finds nothing to remove).
    /// </summary>
    public static bool Request(Zone source, Zone target, int characterId)
    {
        return source.Post(ZoneCommand.Leave(characterId, target));
    }

    /// <summary>
    ///     Snapshots a live <see cref="PlayerRuntimeState" /> into the immutable payload the target zone's
    ///     Enter will seed its own state from. Called by the SOURCE zone's tick only (the sole thread allowed
    ///     to read this state), right after the player was removed from it. Position/heading travel as the
    ///     player's CURRENT ones unless <paramref name="position" /> overrides them — the Phase C/V1 portal flow
    ///     (<c>CzDemandZoneServerInfo2SendHandler</c>) and death-respawn (<see cref="Zone.ApplyDeath" />) both
    ///     pass the resolved arrival point here via <see cref="ZoneCommand.HandoffPosition" /> rather than
    ///     mutating <paramref name="state" /> directly (single-writer invariant, architecture reference §10.1).
    /// </summary>
    /// <remarks>
    ///     FlushSequence is bumped by one: the map change is a real state mutation, and
    ///     <c>usp_Character_PersistBatch</c> only applies rows whose sequence is STRICTLY greater than the
    ///     stored one — without the bump, a player who transfers and then never moves would keep their old
    ///     MapId in SQL forever (the target zone's Enter marks them dirty precisely so this row gets flushed).
    /// </remarks>
    public static PlayerEnterData CreateEnterData(PlayerRuntimeState state, short targetMapId,
        (float X, float Y, float Z)? position = null)
    {
        var (posX, posY, posZ) = position ?? (state.PosX, state.PosY, state.PosZ);

        return new PlayerEnterData(
            state.Session,
            state.Name,
            state.Tribe,
            state.Gender,
            state.HeadType,
            state.FaceType,
            state.Level,
            targetMapId,
            posX,
            posY,
            posZ,
            state.Heading,
            state.Life,
            state.MaxLife,
            state.Mana,
            state.MaxMana,
            state.FlushSequence + 1);
    }
}
