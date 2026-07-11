using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

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
    /// <remarks>
    ///     FlushSequence bumped by one: <c>usp_Character_PersistBatch</c> only applies rows with a strictly
    ///     greater sequence, so this ensures the MapId change actually gets flushed.
    /// </remarks>
    public static PlayerEnterData CreateEnterData(PlayerRuntimeState state, short targetMapId,
        (float X, float Y, float Z)? position = null)
    {
        var (posX, posY, posZ) = position ?? (state.PosX, state.PosY, state.PosZ);

        // In-process handoffs skip SQL entirely, so items/stats must travel with the snapshot or the target
        // zone would seed an empty inventory despite nothing having actually changed.
        List<CharacterItemSlotDto> items = [];
        foreach (var container in AllContainers)
        foreach (var (slot, stack) in state.Inventory.GetContainer(container))
            items.Add(stack.ToRow(container, slot));

        // Same rationale as Items above.
        List<CharacterSkillDto> skills = [];
        foreach (var (slot, learned) in state.LearnedSkills)
            skills.Add(new CharacterSkillDto(slot, learned.SkillId, learned.Grade));

        // Same rationale as Items/Skills above -- see PlayerRuntimeState.Hotkeys' own remarks for the
        // Sort<->Value1<->Value2 field-position mapping this reverses (HotkeySlot.Value1 is the bound id, so
        // it becomes the DB-shaped "Sort" column; HotkeySlot.Value2 is the secondary value ("Value1"); Kind is
        // the actual discriminator ("Value2")).
        List<CharacterHotkeyDto> hotkeys = [];
        foreach (var ((page, index), slot) in state.Hotkeys)
            hotkeys.Add(new CharacterHotkeyDto(page, index, slot.Value1, slot.Value2, (int)slot.Kind));

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
            // The real persisted origin tribe (0-2) must travel here too, or a tribe-3 (Fujin) character's
            // PlayerRuntimeState.PreviousTribe would silently revert to mirroring Tribe (wrong by design for
            // that one case) on every in-process zone transfer -- see that field's own remarks.
            PreviousTribe: state.PreviousTribe,
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
            Exp2: state.Exp2,
            // aZone241Time must travel here too, or it would silently reset to 0 on every in-process zone
            // transfer -- see PlayerRuntimeState.Zone241Time's own remarks.
            Zone241Time: state.Zone241Time,
            KnownCashCatalogVersion: state.KnownCashCatalogVersion,
            TicksSinceDeath: state.TicksSinceDeath,
            ReviveHackFlag: state.ReviveHackFlag,
            CanUseConsumables: state.CanUseConsumables,
            DeathSubCounter: state.DeathSubCounter,
            DungeonInstanceRoundsRemaining: state.DungeonInstanceRoundsRemaining,
            // Carries the live in-memory total through a same-shard hop instead of re-querying it -- without
            // this, a character who earned hero-rank points this session would see the counter silently reset
            // to its last-persisted (stale) value on every zone transfer. See PlayerRuntimeState.HeroRankPoints's
            // own remarks.
            HeroRankPoints: state.HeroRankPoints,
            // Stat/elixir-potion lifetime counters must travel here too, or they silently reset to 0 on every
            // in-process zone transfer -- see PlayerRuntimeState.EatLifePotion's own remarks.
            EatLifePotion: state.EatLifePotion,
            EatManaPotion: state.EatManaPotion,
            EatStrPotion: state.EatStrPotion,
            EatDexPotion: state.EatDexPotion,
            EatElePotion: state.EatElePotion,
            // Lucky Drop/"Acquisition" Scroll minutes counter must travel here too, or it silently resets to 0
            // on every in-process zone transfer -- see PlayerRuntimeState.DropItemTime's own remarks.
            DropItemTime: state.DropItemTime,
            // War Point currency balance must travel here too, or it silently resets to 0 on every in-process
            // zone transfer -- see PlayerRuntimeState.WarPoint's own remarks.
            WarPoint: state.WarPoint,
            // Quest step/kill-counter tracking, the daily-mission counters, auto-hunt config, both auto-potion
            // ratios, and pet growth/activity must travel here too, or they silently reset to their zero/
            // disabled default on every in-process zone transfer -- same "must travel or it resets" posture as
            // every other field above. See PlayerEnterData's own remarks on these parameters and
            // EnterWorldService's world-entry hydration, which is the only other place that populates them.
            QuestProgress: new QuestProgress(state.QuestStepPermanent, state.QuestActiveFlag, state.QuestSort,
                state.QuestTargetPhase, state.QuestKillCounter),
            MissionJoinWar: state.MissionJoinWar,
            MissionKillOtherTribe: state.MissionKillOtherTribe,
            MissionKillMonster: state.MissionKillMonster,
            MissionPlayTime: state.MissionPlayTime,
            AutoHuntEnabled: state.AutoHuntEnabled,
            AutoHuntConfig: state.AutoHuntConfig,
            AutoLifeRatio: state.AutoLifeRatio,
            AutoManaRatio: state.AutoManaRatio,
            PetGrowth: state.PetGrowth,
            PetActivity: state.PetActivity,
            // mSupportSkillTimeUpRatio's two source fields must travel here too, or a premium account's/
            // active-buff-extension's duration multiplier would silently reset to 1x on every in-process zone
            // transfer -- see PlayerEnterData's own remarks on these two parameters.
            PremiumExpireUtc: state.PremiumExpireUtc,
            BuffX2Time: state.BuffX2Time,
            // Carries the live BUFF_INFO through the handoff -- see ZoneTransferBuffRules' own remarks for the
            // one destination-zone exception (124, Pungun Hall) that wipes it instead.
            Buffs: ZoneTransferBuffRules.Resolve(state.Buffs, targetMapId),
            // Pet EXP boost pill running duration counter -- see PlayerEnterData's own remarks on this
            // parameter for why it must travel through an in-process handoff.
            PetExpX2Time: state.PetExpX2Time,
            // Live hotkey table -- must travel here too, or a character's bound hotkeys would silently reset
            // to empty on every in-process zone transfer. See PlayerEnterData.Hotkeys' own remarks.
            Hotkeys: hotkeys,
            // Store/coffre money pool + second-page expiry dates must travel here too, or they would silently
            // reset to 0 on every in-process zone transfer -- see PlayerRuntimeState.Vault.cs's own remarks.
            StoreMoney: state.StoreMoney,
            InventoryDate: state.InventoryDate,
            StoreDate: state.StoreDate,
            // aPetBagDate must travel here too, or it would silently reset to 0 on every in-process zone
            // transfer -- see PlayerRuntimeState.PetBagDate's own remarks.
            PetBagDate: state.PetBagDate,
            // The M15 Pet Lucky Box (8111) pity counter must travel here too, or it would silently reset to 0
            // on every in-process zone transfer -- see PlayerRuntimeState.M15PetLuckyBoxPity's own remarks.
            M15PetLuckyBoxPity: state.M15PetLuckyBoxPity,
            // Carries the live source-IP through the handoff too, or the PvP same-origin kill-credit guard
            // would silently go blind (null vs null never proves same-origin) for every character past their
            // first in-process zone transfer -- see PlayerRuntimeState.SourceIp's own remarks.
            SourceIp: state.SourceIp,
            // Live rune-socket arrays must travel here too, or a character's socketed runes would silently
            // reset to empty on every in-process zone transfer -- see PlayerEnterData.RuneSystem's own remarks.
            RuneSystem: state.RuneSystem,
            RuneSystemStat: state.RuneSystemStat);
    }
}

/// <summary>
///     The zone-transfer buff-carry rule (Server/ts25zone/S04_MyWork02.cpp:2017-2186's
///     <c>DEMAND_ZONE_SERVER_INFO_2</c>, zone-124 zero-fill at :2169-2173): every destination zone carries the
///     transferring character's live buff table through unmodified, except zone 124 (Pungun Hall,
///     ServerDocs/19_Header_Lib/11_ZoneMoveInfo.md:87's <c>mDATA[123]</c>), which always wipes it. Shared by
///     both the in-process handoff payload (<see cref="ZoneTransfer.CreateEnterData" />) and the client-facing
///     entry snapshot built on the same connection for a same-shard hop (<c>ZoneMoveService.HandleAsync</c>) --
///     both must agree on the same zeroed-or-live outcome, or the client's own buff display would disagree with
///     what the destination zone's simulation actually applies.
/// </summary>
public static class ZoneTransferBuffRules
{
    /// <summary>
    ///     The sole destination that wipes the carried buff table instead of carrying it through -- Pungun
    ///     Hall (Server/ts25zone/S04_MyWork02.cpp:2169-2173).
    /// </summary>
    public const short BuffClearDestinationZoneId = 124;

    /// <summary>
    ///     Returns the <see cref="BuffInfo" /> that must ride along in the handoff/entry-snapshot for a
    ///     transfer into <paramref name="targetMapId" />: a defensive clone of <paramref name="liveBuffs" /> for
    ///     every destination except <see cref="BuffClearDestinationZoneId" />, which always yields a fresh
    ///     all-zero table regardless of what was live at the moment of transfer. Never returns the same backing
    ///     array as <paramref name="liveBuffs" /> -- see <see cref="PlayerRuntimeState.Buffs" />'s own remarks on
    ///     why a per-instance array is required.
    /// </summary>
    public static BuffInfo Resolve(BuffInfo liveBuffs, short targetMapId)
    {
        return targetMapId == BuffClearDestinationZoneId
            ? new BuffInfo { Buff = new int[70] }
            : new BuffInfo { Buff = (int[])liveBuffs.Buff.Clone() };
    }
}
