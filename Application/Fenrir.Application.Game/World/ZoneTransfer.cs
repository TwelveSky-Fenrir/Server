using Fenrir.Application.Game.Inventory;
using Fenrir.Data.Characters;

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
    /// <summary>Every container kind an in-process handoff must carry over -- see <see cref="CreateEnterData" />.</summary>
    private static readonly byte[] AllContainers =
    [
        ContainerMatrix.InventoryPage0, ContainerMatrix.InventoryPage1, ContainerMatrix.Equipment,
        ContainerMatrix.StorePage0, ContainerMatrix.StorePage1
    ];

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
    ///     (<c>ZoneMoveHandler</c>) and death-respawn (<see cref="Zone.ApplyDeath" />) both
    ///     pass the resolved arrival point here via <see cref="ZoneCommand.HandoffPosition" /> rather than
    ///     mutating <paramref name="state" /> directly (single-writer invariant, architecture reference §10.1).
    /// </summary>
    /// <param name="sourceClock">
    ///     The SOURCE zone's own simulated clock at the moment of this snapshot (<see cref="Zone.Tick" />'s
    ///     private accumulator) — used ONLY to translate a pending <see cref="PlayerRuntimeState.ReviveAtZoneClock" />
    ///     into a REMAINING duration, since the source and target zones tick independently and their absolute
    ///     clock values are not comparable. A player handed off mid-death (e.g. an explicit
    ///     <c>CZ_DEMAND_ZONE_SERVER_INFO_2</c> request sent before the auto-revive timer elapses) must keep the
    ///     rest of that timer on arrival rather than either losing it (reviving instantly) or restarting a full
    ///     new delay.
    /// </param>
    /// <remarks>
    ///     FlushSequence is bumped by one: the map change is a real state mutation, and
    ///     <c>usp_Character_PersistBatch</c> only applies rows whose sequence is STRICTLY greater than the
    ///     stored one — without the bump, a player who transfers and then never moves would keep their old
    ///     MapId in SQL forever (the target zone's Enter marks them dirty precisely so this row gets flushed).
    /// </remarks>
    public static PlayerEnterData CreateEnterData(PlayerRuntimeState state, short targetMapId, TimeSpan sourceClock,
        (float X, float Y, float Z)? position = null)
    {
        var (posX, posY, posZ) = position ?? (state.PosX, state.PosY, state.PosZ);

        // In-process handoffs never touch SQL (a fresh usp_Character_GetForWorldEntry round trip would be
        // wasted work for a map change that already carries the live state) -- Items/Stats must therefore
        // travel with the rest of the snapshot here, or the target zone would seed a brand-new
        // PlayerRuntimeState with an EMPTY inventory and NULL Stats despite nothing having actually changed
        // about the character's equipment. Re-derives the flat item-row list from the CURRENT in-memory
        // containers (round-tripping ItemStack -> CharacterItemSlotDto) since that -- not a second SQL read --
        // is this zone's own source of truth for "what does this player have right now".
        List<CharacterItemSlotDto> items = [];
        foreach (var container in AllContainers)
        foreach (var (slot, stack) in state.Inventory.GetContainer(container))
            items.Add(stack.ToRow(container, slot));

        // Clamped at zero: a revive already due by the time this handoff drains (e.g. several ticks elapsed
        // between ApplyDeath scheduling it and the Leave actually running) must not go NEGATIVE -- the target
        // zone would misread a negative remaining as "already overdue by that much", which is harmless in
        // effect (ProcessPendingRevives only compares clock >= due) but not the intent.
        TimeSpan? reviveRemaining = state.IsDead
            ? (state.ReviveAtZoneClock - sourceClock) is var remaining && remaining > TimeSpan.Zero
                ? remaining
                : TimeSpan.Zero
            : null;

        // Same "no second SQL read" rationale as Items above -- round-trips the CURRENT in-memory learned-skill
        // slots (Skills.LearnedSkill) back into the flat DTO shape PlayerEnterData/Zone.HandleEnter already share.
        List<CharacterSkillDto> skills = [];
        foreach (var (slot, learned) in state.LearnedSkills)
            skills.Add(new CharacterSkillDto(slot, learned.SkillId, learned.Grade));

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
            state.FlushSequence + 1,
            state.IsDead,
            reviveRemaining,
            items,
            state.Stats,
            state.IsMuted,
            state.GuildId,
            state.GuildName,
            state.GuildRoleDb,
            state.TribeRole,
            state.Friends,
            skills,
            state.TeacherCharacterId,
            state.StudentCharacterId,
            GuildCallName: state.GuildCallName,
            // CORRECTION (review finding, Phase C/V7): these live values must travel across an in-process
            // handoff too -- see PlayerEnterData's own remarks on the "resets to 0" bug this closes.
            StatVit: state.StatVit,
            StatStr: state.StatStr,
            StatInt: state.StatInt,
            StatDex: state.StatDex,
            StatPoints: state.StatPoints,
            Title: state.Title,
            Halo: state.Halo,
            RebirthCount: state.RebirthCount,
            Experience: state.Experience,
            ContributionPoints: state.ContributionPoints,
            TeacherPoint: state.TeacherPoint);
    }
}
