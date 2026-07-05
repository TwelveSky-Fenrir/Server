using Fenrir.Application.Game.Domain.Inventory;

namespace Fenrir.Application.Game.Domain.World;

/// <summary>
///     In-process map transfer: a <c>Leave</c>-with-handoff is posted to the source zone, whose tick removes
///     the player, snapshots state into <see cref="PlayerEnterData" />, and posts <c>Enter</c> to the target --
///     state travels inside the command, never referenced by two zones at once. Client keeps its TCP connection
///     throughout (no legacy LP/ZP_MOVING_ZONE re-handshake here).
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
    ///     Fire-and-forget: false means the source inbox dropped the write (overload) and nothing happened --
    ///     the player stays put. Posting an untracked character is a harmless no-op.
    /// </summary>
    public static bool Request(Zone source, Zone target, int characterId)
    {
        return source.Post(ZoneCommand.Leave(characterId, target));
    }

    /// <summary>
    ///     Snapshots a live <see cref="PlayerRuntimeState" /> into the payload the target zone's Enter seeds
    ///     from. Called by the SOURCE zone's tick only, right after the player is removed. Position/heading are
    ///     the player's current ones unless <paramref name="position" /> overrides them.
    /// </summary>
    /// <param name="sourceClock">
    ///     Source zone's clock at snapshot time, used only to convert a pending
    ///     <see cref="PlayerRuntimeState.ReviveAtZoneClock" /> into a remaining duration -- source/target clocks
    ///     tick independently and are not otherwise comparable.
    /// </param>
    /// <remarks>
    ///     FlushSequence bumped by one: <c>usp_Character_PersistBatch</c> only applies rows with a strictly
    ///     greater sequence, so this ensures the MapId change actually gets flushed.
    /// </remarks>
    public static PlayerEnterData CreateEnterData(PlayerRuntimeState state, short targetMapId, TimeSpan sourceClock,
        (float X, float Y, float Z)? position = null)
    {
        var (posX, posY, posZ) = position ?? (state.PosX, state.PosY, state.PosZ);

        // In-process handoffs skip SQL entirely, so items/stats must travel with the snapshot or the target
        // zone would seed an empty inventory despite nothing having actually changed.
        List<CharacterItemSlotDto> items = [];
        foreach (var container in AllContainers)
        foreach (var (slot, stack) in state.Inventory.GetContainer(container))
            items.Add(stack.ToRow(container, slot));

        // Clamped at zero: a revive already overdue when this handoff drains must not go negative.
        TimeSpan? reviveRemaining = state.IsDead
            ? state.ReviveAtZoneClock - sourceClock is var remaining && remaining > TimeSpan.Zero
                ? remaining
                : TimeSpan.Zero
            : null;

        // Same rationale as Items above.
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
            TeacherPoint: state.TeacherPoint,
            Level2: state.Level2,
            Exp2: state.Exp2);
    }
}
