using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Avatars;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.Social;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Stats;
using Fenrir.Data.Security;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Serialization.Wire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Services.ZoneLifecycle;

public sealed class EnterWorldService(
    ICharacterRepository characters,
    WorldDataCache worldData,
    ZoneRegistry zones,
    IMuteRepository mutes,
    IBanRepository bans,
    ApplicationFirewall firewall,
    IGuildRepository guilds,
    GuildRankingCache guildRanking,
    ITribeRepository tribes,
    IFriendRepository friends,
    IMentorRepository mentors,
    IHeroRankingRepository heroRankings,
    ICharacterShardLocationRepository characterShardLocations,
    WorldStateService worldState,
    TowerWarState towerWar,
    ZoneCenterSiegeState zoneCenterSiegeState,
    TribeGuardCorridorState tribeGuardCorridorState,
    IOptions<GameServerOptions> options,
    ILogger<EnterWorldService> logger) : IEnterWorldService
{
    public async ValueTask HandleAsync(EnterWorldRequest packet, ZoneClientSession zoneSession,
        CancellationToken cancellationToken)
    {
        var accountId = zoneSession.AccountId!.Value;
        var characterId = zoneSession.CharacterId!.Value;

        // Re-checked here (not just at Login): Login and Zone are separate TCP listeners (ADR-0012), so an IP
        // blocked after that account's Login session already happened would otherwise never be re-evaluated.
        if (!await firewall.IsAllowedAsync(zoneSession.RemoteEndPoint, cancellationToken))
        {
            logger.LogWarning(
                "Enter-world rejected for account {AccountId} character {CharacterId}: remote IP is firewalled",
                accountId, characterId);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        // A GM-banned character (admin.Bans, ZONE_BLOCK_USER_FOR_PLAYUSER's Fenrir equivalent) must never reach
        // the world, unlike a mute -- checked before the bundle fetch below to not waste it on a rejected entry.
        if (await bans.IsActiveForCharacterAsync(characterId, cancellationToken))
        {
            logger.LogWarning("Enter-world rejected for character {CharacterId}: character is GM-banned",
                characterId);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        // tID must still name the account this socket was ticketed for.
        if (!ObfuscatedUidCodec.TryDecodeAccountId(packet.Id, out var decodedAccountId) ||
            decodedAccountId != accountId)
        {
            logger.LogWarning(
                "Enter-world rejected for account {AccountId} character {CharacterId}: obfuscated id mismatch",
                accountId, characterId);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        // A client cannot enter the world already mid-action (move/skill/etc) -- Server/ts25zone/
        // S04_MyWork02.cpp:773-782 rejects unless action type is 0 and action sort is 0 or 1, for every
        // player, unconditionally.
        if (packet.Action.Type != 0 || packet.Action.Sort is not (0 or 1))
        {
            logger.LogWarning(
                "Enter-world rejected for character {CharacterId}: disallowed action type {ActionType}/sort {ActionSort}",
                characterId, packet.Action.Type, packet.Action.Sort);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var bundle = await characters.GetWorldEntryBundleAsync(characterId, cancellationToken);
        if (bundle is null)
        {
            logger.LogWarning("Enter-world rejected for character {CharacterId}: world-entry bundle fetch failed",
                characterId);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var character = bundle.Character;

        // Resolve against the ticket-committed CharacterId; AvatarName only re-confirms it, never picks it.
        if (packet.AvatarName != character.Name)
        {
            logger.LogWarning(
                "Enter-world rejected for character {CharacterId}: avatar name {SentName} does not match {ExpectedName}",
                characterId, packet.AvatarName, character.Name);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        // Tribe/PreviousTribe self-consistency (Server/ts25zone/S04_MyWork02.cpp:880-901): a main-faction
        // tribe (0-2) must carry a PreviousTribe exactly equal to itself; the fourth faction (3) must carry a
        // PreviousTribe in {0,1,2} (the one legitimate case where the two fields differ -- "transferred in
        // from an original tribe"); any other Tribe value is never valid. This checks the just-loaded
        // record's own internal consistency, never anything the client asserts, and -- matching legacy -- ends
        // the session outright with no response on any mismatch rather than a structured failure code.
        if (!IsTribeAndPreviousTribeConsistent(character.Tribe, character.PreviousTribe))
        {
            logger.LogWarning(
                "Enter-world rejected for character {CharacterId}: tribe {Tribe}/previousTribe {PreviousTribe} are internally inconsistent",
                characterId, character.Tribe, character.PreviousTribe);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        // The character's persisted map must be one this shard hosts (ADR-0012).
        if (!zones.TryGet(character.MapId, out var zone))
        {
            logger.LogWarning(
                "Character {CharacterId} is on map {MapId}, which this shard does not host -- aborting registration",
                characterId, character.MapId);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        // Fenrir-only failure boundary -- no Server/ citation applies (there is no legacy counterpart to
        // mirror here; this segment's shape is purely a Fenrir C# call-chain composition concern). Before
        // this try/catch, any exception raised anywhere from here through zone.Post() below -- equipment/
        // stat computation, the six-way concurrent repository batch, either response send, or the Post
        // itself -- propagated uncaught up through ZoneFrameDispatcher/SessionLoop/GameConnectionHost with
        // no account/character context anywhere in the trail. CompleteWorldEntryAsync is kept as a local
        // function (rather than reindenting this whole segment in place) purely so the try/catch below reads
        // as this segment's single failure boundary without an oversized reindentation diff.
        async ValueTask CompleteWorldEntryAsync()
        {
            var equipmentContainer = BuildEquipmentContainer(bundle.Items);
            var attributes = new CharacterBaseAttributes(character.StatVit, character.StatStr, character.StatInt,
                character.StatDex, character.Level, character.Tribe, character.PreviousTribe, character.Title,
                character.Halo, character.RebirthCount);

            var petItemId = PetSlots.ResolveEquippedPetItemId(bundle.Items);
            var petContribution = PetGrowthCalculator.Compute(petItemId, character.PetGrowth, character.PetActivity,
                worldData.ItemsById);

            var stats = EquipmentService.RecomputeStats(attributes, equipmentContainer, worldData,
                pet: petContribution);

            // Seeds PlayerRuntimeState.IsMuted for this world entry; MuteRefreshPollHost (Application/
            // Fenrir.Application.Game.Hosting) keeps it fresh on a fixed interval afterwards -- see that
            // host's own remarks for why a per-chat-message requery is not the right shape here.
            var isMutedTask = mutes.IsActiveForCharacterAsync(characterId, cancellationToken);
            var guildTask = guilds.GetByCharacterAsync(characterId, cancellationToken);
            var tribeRoleTask = tribes.GetRoleForCharacterAsync(characterId, cancellationToken);
            var friendsTask = friends.GetByCharacterAsync(characterId, cancellationToken);
            var mentorTask = mentors.GetForCharacterAsync(characterId, cancellationToken);

            // World-entry hydration for PlayerRuntimeState.HeroRankPoints (legacy MyDB::GetHeroPoint,
            // Server/ts25login/S08_MyDB.cpp:1178-1188 -- read here, Zone-side, rather than threaded through
            // the Login-side session ticket; see Migrations/030_hero_rank_points_world_entry_hydration.sql's
            // own header for why that deviation from the legacy trigger point is accepted). Null (no row for
            // this character/period yet) legitimately means zero, matching legacy's own collapsed
            // no-row/query-failure semantics -- see IHeroRankingRepository.GetPointsAsync's own remarks.
            var heroRankPointsTask = heroRankings.GetPointsAsync(characterId,
                HeroRankPointAccumulator.CurrentPeriodKind,
                cancellationToken);

            // Cross-shard character-location directory (runtime.CharacterShardLocation): a same-shard-miss
            // fallback for whisper/friend-locate/guild-find. Per-connection, once-per-world-entry cost, not a
            // tick or per-packet hot path, so an extra awaited stored-procedure call here alongside the others is
            // unremarkable. Intra-shard zone-to-zone handoffs never call this again -- ShardId never changes on a
            // same-shard hop, and MapId staleness for a character who has since wandered to another map on the
            // SAME shard is an accepted bound (this directory is a same-shard-miss fallback only).
            var shardLocationUpsertTask = characterShardLocations.UpsertAsync(characterId, options.Value.ShardId,
                character.MapId, character.Name, character.Tribe, cancellationToken);

            await Task.WhenAll(isMutedTask.AsTask(), guildTask.AsTask(), tribeRoleTask.AsTask(),
                friendsTask.AsTask(), mentorTask.AsTask(), heroRankPointsTask.AsTask(),
                shardLocationUpsertTask.AsTask());

            var isMuted = isMutedTask.Result;
            var guildMembership = guildTask.Result;
            var tribeRole = tribeRoleTask.Result;
            var friendRows = friendsTask.Result;
            var mentorBond = mentorTask.Result;
            var heroRankPoints = heroRankPointsTask.Result ?? 0;

            var guildRoleWire = guildMembership is { } gm ? GuildRoleCodec.DbRoleToWire(gm.Role) : 0;
            var friendNameBySlot = friendRows.ToDictionary(f => f.Slot, f => f.FriendName);
            var friendIdBySlot = friendRows.ToDictionary(f => f.Slot, f => f.FriendCharacterId);
            var socialSnapshot = new AvatarSocialSnapshot(
                friendNameBySlot,
                mentorBond?.TeacherName ?? "",
                mentorBond?.StudentName ?? "",
                guildMembership?.GuildName ?? "",
                guildRoleWire,
                guildMembership?.CallName ?? "");

            // Legacy parity note (do not gate this pair behind ZoneReadyRequest/op13): Server/ts25zone/
            // S04_MyWork02.cpp:979-980 sends ZCP_REGISTER_AVATAR_RECV (op12 response) and
            // ZCP_BROADCAST_WORLD_INFO (op13-numbered response) back to back, unconditionally, inside the SAME
            // op12 (P_REGISTER_AVATAR_SEND) handler -- neither waits on the client's own op13
            // (CZ_CLIENT_OK_FOR_ZONE_SEND), which per ZoneReadyHandler/ZoneReadyService never sends a response
            // packet under any branch. ServerDocs/19_Header_Lib/03_Protocol_Liaisons_Client_Zone.md's sequence
            // diagram groups these by opcode number, not causal order -- Server/ wins that disagreement. Both
            // sends below must stay unconditional and synchronous with this handler's own success path.
            var registerRecv = new EnterWorldResponse
            {
                // A brand-new character legitimately has no buff rows yet (creation never writes any); a
                // returning character's own persisted snapshot must ride along here instead of a flat zero.
                AvatarInfo = AvatarInfoFactory.CreateForCharacter(character, bundle.Items, socialSnapshot,
                    bundle.Skills, bundle.Hotkeys),
                BuffInfo = BuildBuffInfo(bundle.Buffs)
            };
            zoneSession.SendRaw(ZoneMessageFactory.Encode(in registerRecv));

            var broadcastWorldInfo = new WorldSnapshotResponse
            {
                // Three overlays stacked onto the zeroed template, each covering a disjoint field slice: guild
                // ranking board, live RvR tribe-symbol/points snapshot, and (the newest) the numbered zone-siege
                // state machines + tribe-guard corridor passability that already have a real in-process backing
                // model (ZoneCenterSiegeState/TribeGuardCorridorState) but previously never reached this packet
                // -- see ZoneCenterSiegeProjection's own remarks.
                WorldInfo = ZoneCenterSiegeProjection.Apply(
                    WorldStateProjection.Apply(
                        GuildRankingProjection.Apply(WorldStateTemplates.ZeroedWorldInfo, guildRanking.Top),
                        worldState),
                    zoneCenterSiegeState, tribeGuardCorridorState),
                // No repository currently projects tribe master/sub-master NAMES or the vote/honor-rank rosters
                // (ITribeRepository only ever returns CharacterIds) -- the zeroed template is the correct
                // placeholder until that surface exists, not a regression introduced here.
                TribeInfo = WorldStateTemplates.ZeroedTribeInfo
            };
            zoneSession.SendRaw(ZoneMessageFactory.Encode(in broadcastWorldInfo));

            // Legacy pairs the full 12-tower ownership/status snapshot with the RvR world-info broadcast
            // immediately above, unconditionally, inside this same registration-completion sequence
            // (Server/ts25zone/S04_MyWork02.cpp:1203-1204, B_BROADCAST_CHUGSOUNG_INFO) -- for every zone
            // entry, not just tower zones, and regardless of any tower's current siege phase. Previously
            // Fenrir's only equivalent send fired solely from Zone.Combat.cs's ApplyTowerGuardianHitSideEffects
            // (a guardian's own first landed hit), so a player who never witnessed that event received zero
            // tower-ownership information for their whole session. TowerStatusResponse is not Compressed, so
            // (unlike the two SendRaw/Encode calls above) it goes out via the plain Send<T> path -- the same
            // call shape Zone.Combat.cs's own BroadcastTowerStatus already uses for this exact packet type.
            zoneSession.Send(towerWar.BuildStatusSnapshot());

            // Self-spawn: Zone doesn't know this player exists yet (ZoneCommand.Enter below is only posted, not ticked).
            zoneSession.Send(new AvatarActionResponse
            {
                ServerIndex = characterId,
                UniqueNumber = unchecked((uint)characterId),
                Data = new ObjectForAvatar
                {
                    // Legacy sources this from the character's own persisted record at this exact moment
                    // (Server/ts25zone/S04_MyWork02.cpp:994-999), not a fixed value -- but no VisibleState column
                    // exists yet anywhere in game.Characters to source it from (a schema gap, not just a
                    // wrong-constant one). Until that column -- and a GM hide/show command
                    // (Server/ts25zone/S04_MyWork04.cpp:933-958) -- both exist on the Fenrir side, every
                    // character's real value IS 1: creation sets it unconditionally
                    // (Server/ts25login/S04_MyWork02.cpp:739-741), the DB column default agrees
                    // (Server/BuildEU33/DB/nxtserver.sql:28), and no write site ever sets it to 0 without a GM
                    // action Fenrir doesn't implement yet. 0 is the exact value legacy reserves for "this avatar
                    // is GM-hidden" (Server/ts25zone/H07_MyGame.h:971's IsHiding) -- hardcoding it here made every
                    // character appear self-invisible on every zone entry (the "own character not visible" bug).
                    // 1 is legacy-accurate for every character Fenrir can currently represent, not a safer guess.
                    VisibleState = 1,
                    // Out of scope for the VisibleState fix above -- SpecialState's own creation-time default and
                    // write-site semantics were not independently verified, so it stays untouched pending its own
                    // legacy-behavior-translator contract.
                    SpecialState = 0,
                    KillOtherTribe = 0,
                    GoodFellow = 0,
                    GuildName = socialSnapshot.GuildName,
                    GuildRole = socialSnapshot.GuildRoleWire,
                    CallName = socialSnapshot.CallName,
                    // Legacy zeroes this only for a guild-less character; a real guild member's mark-effect
                    // value has no source anywhere in the current guild repository surface yet (no MarkEffect
                    // column/DTO field exists) -- flagged, not silently assumed correct.
                    GuildMarkEffect = 0,
                    Name = character.Name,
                    Tribe = character.Tribe,
                    // The Noble Dragon/Royal Serpent/Grand Tiger starter-kit template (0-2), genuinely independent
                    // of Tribe -- now a real persisted column (Migrations/018), no longer synthesized as 0.
                    PreviousTribe = character.PreviousTribe,
                    Gender = character.Gender,
                    HeadType = character.HeadType,
                    FaceType = character.FaceType,
                    Level1 = character.Level,
                    Level2 = character.Level2,
                    EquipForView = EquipmentViewCodec.BuildEquipForView(bundle.Items),
                    // character.MountItemId/MountSlotIndex are readable since Migrations/018, but the legacy's
                    // exact "currently mounted" condition (S04_MyWork02.cpp:935-940) isn't confirmed yet -- needs
                    // a legacy-behavior-translator contract before this stops being a flat 0.
                    AnimalNumber = 0,
                    Title = character.Title,
                    Halo = character.Halo,
                    RebirthNum = character.RebirthCount,
                    BattleTeam = 0,
                    Action = new ActionInfo
                    {
                        Type = 0,
                        Sort = 0,
                        Frame = 0,
                        Location = [character.PosX, character.PosY, character.PosZ],
                        TargetLocation = [character.PosX, character.PosY, character.PosZ],
                        Front = character.Heading,
                        TargetFront = character.Heading,
                        // Companion-pet follow sub-fields (PlayerRuntimeState.PetActionSort and its five
                        // siblings, see that field's own remarks): correctly zero here, not a remaining
                        // instance of the companion-pet-follow-rebroadcast gap -- this self-spawn packet is
                        // sent before HandleEnter ever creates this character's PlayerRuntimeState, so no
                        // CZ_UPDATE_PET_ACTION_SEND (op156) value has ever been recorded for this session yet.
                        // Every subsequent avatar-action broadcast (built via Zone.BuildAvatarActionRecv /
                        // Zone.PetActionFieldsOf once the character is tracked) reads the live stored value.
                        PetLocation = new float[3],
                        PetTargetLocation = new float[3],
                        PetFront = 0,
                        PetSort = 0,
                        TargetObjectSort = 0,
                        TargetObjectIndex = 0,
                        TargetObjectUniqueNumber = 0,
                        SkillNumber = 0,
                        SkillGradeNum1 = 0,
                        SkillGradeNum2 = 0,
                        SkillValue = 0
                    },
                    MaxLifeValue = character.MaxLife,
                    LifeValue = character.Life,
                    MaxManaValue = character.MaxMana,
                    ManaValue = character.Mana,
                    EffectValueForView = BuildEffectValueForView(bundle.Buffs),
                    // Legacy copies the character's own persisted party name here (S04_MyWork02.cpp:1036), not
                    // blank -- no party-name column/DTO field is persisted anywhere on the Fenrir side yet
                    // (PartyRegistry is in-memory-only and does not survive a reconnect), so this stays "" pending
                    // a database-engineer schema addition, not a silent regression.
                    PartyName = "",
                    DuelState = new int[3],
                    PShopState = 0,
                    PShopName = "",
                    CostumeNumber = 0,
                    BufEffectTimeState = 0,
                    BufSort = 0,
                    AutoState = 0,
                    FishingState = 0,
                    FishingStep = 0,
                    FishingPoint = new float[3],
                    RankPoint = 0,
                    TargetState = 0,
                    AnimalAbsorbState = 0,
                    PetValid = 0,
                    Unk1 = 0,
                    // Top-level PetLocation (distinct from Action.PetLocation above): legacy sets this to the
                    // avatar's own current position, not zero (S04_MyWork02.cpp:1058-1060).
                    PetLocation = [character.PosX, character.PosY, character.PosZ],
                    PetFrame = 0,
                    Unk624 = 0,
                    Unk625 = 0,
                    UniqueSkillNumber = 0,
                    UniqueSkillBuffTime = 0,
                    CostumeState = 0,
                    StellarCoreNumber = 0
                },
                CheckChangeActionState = 0
            });

            var entered = zone.Post(ZoneCommand.Enter(characterId, new PlayerEnterData(
                zoneSession,
                character.Name,
                character.Tribe,
                character.Gender,
                character.HeadType,
                character.FaceType,
                character.Level,
                character.MapId,
                character.PosX,
                character.PosY,
                character.PosZ,
                character.Heading,
                character.Life,
                character.MaxLife,
                character.Mana,
                character.MaxMana,
                character.FlushSequence,
                Items: bundle.Items,
                Stats: stats,
                IsMuted: isMuted,
                GuildId: guildMembership?.GuildId,
                GuildName: socialSnapshot.GuildName,
                GuildRoleDb: guildMembership?.Role ?? 0,
                TribeRole: tribeRole,
                FriendsBySlot: friendIdBySlot,
                Skills: bundle.Skills,
                TeacherCharacterId: mentorBond?.TeacherCharacterId,
                StudentCharacterId: mentorBond?.StudentCharacterId,
                QuestProgress: new QuestProgress(character.QuestStepPermanent, character.QuestActiveId,
                    character.QuestSort, character.QuestTargetPhase, character.QuestKillCounter),
                MissionJoinWar: character.JoinWar,
                MissionKillOtherTribe: character.MissionKillOtherTribe,
                MissionKillMonster: character.MissionKillMonster,
                MissionPlayTime: character.MissionPlayTime,
                AutoHuntEnabled: character.AutoHuntEnabled,
                AutoHuntConfig: character.AutoHuntConfig is { } configBytes &&
                                AutoHunt.TryRead(configBytes, out var autoHunt)
                    ? autoHunt
                    : null,
                AutoLifeRatio: character.AutoLifeRatio,
                AutoManaRatio: character.AutoManaRatio,
                PetGrowth: character.PetGrowth,
                PetActivity: character.PetActivity,
                StatVit: character.StatVit,
                StatStr: character.StatStr,
                StatInt: character.StatInt,
                StatDex: character.StatDex,
                StatPoints: character.StatPoints,
                Title: character.Title,
                Halo: character.Halo,
                RebirthCount: character.RebirthCount,
                Experience: character.Experience,
                ContributionPoints: character.ContributionPoints,
                TeacherPoint: character.TeacherPoint,
                Level2: character.Level2,
                Exp2: character.Exp2,
                HeroRankPoints: heroRankPoints,
                // Stat/elixir-potion lifetime counters (item-usage-consumables finding, Critical) --
                // game.Characters already persisted these five, but nothing read them back into
                // PlayerRuntimeState until now. See PlayerRuntimeState.EatLifePotion's own remarks.
                EatLifePotion: character.EatLifePotion,
                EatManaPotion: character.EatManaPotion,
                EatStrPotion: character.EatStrPotion,
                EatDexPotion: character.EatDexPotion,
                EatElePotion: character.EatElePotion,
                // mSupportSkillTimeUpRatio's Premium source field (behavior contract
                // "buff-application-stacking-decay") -- the character's real persisted
                // game.Characters.PremiumExpireUtc, not the PlayerEnterData default of 0. BuffX2Time stays at
                // its default (no persisted source exists yet -- see PlayerRuntimeState.BuffX2Time's own
                // remarks), so a fresh world entry always starts with that factor inactive.
                PremiumExpireUtc: character.PremiumExpireUtc)));

            // A dropped Enter is never replayed -- the character would stay permanently invisible despite the two
            // packets above already telling the client registration succeeded, so treat it as fatal.
            if (!entered)
            {
                logger.LogError(
                    "Zone {MapId} inbox full: dropped Enter for character {CharacterId} -- aborting session",
                    zone.MapId, characterId);
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            zoneSession.CurrentZone = zone;
            zoneSession.MarkRegistering();

            logger.LogInformation(
                "Character {CharacterId} (account {AccountId}) entered world on map {MapId} -- awaiting zone-ready",
                characterId, accountId, character.MapId);
        }

        try
        {
            await CompleteWorldEntryAsync();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Distinct from every explicit Abort(DisconnectReason.Faulted) call above and inside
            // CompleteWorldEntryAsync (firewall/ban/ticket-mismatch/tribe-consistency/map-not-hosted/dropped-
            // Enter): those are all validated precondition rejections with no exception involved.
            // ProcessingFault instead marks "an unhandled exception actually reached here", carrying the
            // account/character/map context that the generic SessionLoop/GameConnectionHost catches above this
            // one can never have, and is the same idempotent Abort() every other rejection path in this
            // service already uses (see ClientSession.Abort's own remarks) -- calling it here does not race or
            // conflict with the CompleteWorldEntryAsync's own explicit Abort(Faulted) on the dropped-Enter
            // path, since that path already returned before any exception could reach this catch.
            logger.LogError(ex,
                "Enter-world processing faulted for account {AccountId} character {CharacterId} on map {MapId} -- " +
                "the client may already hold a partial handshake if some of the three response payloads were " +
                "already sent; aborting session",
                accountId, characterId, character.MapId);
            zoneSession.Abort(DisconnectReason.ProcessingFault);
        }
    }

    /// <summary>
    ///     Server/ts25zone/S04_MyWork02.cpp:880-901 -- the zone-entry self-consistency switch, all four
    ///     branches: a main-faction Tribe (0-2) requires PreviousTribe == Tribe; the fourth faction (3)
    ///     requires PreviousTribe in {0,1,2}; any other Tribe value is rejected outright.
    /// </summary>
    private static bool IsTribeAndPreviousTribeConsistent(byte tribe, byte previousTribe)
    {
        return tribe switch
        {
            0 or 1 or 2 => previousTribe == tribe,
            3 => previousTribe is 0 or 1 or 2,
            _ => false
        };
    }

    private static ImmutableDictionary<byte, ItemStack> BuildEquipmentContainer(
        IReadOnlyList<CharacterItemSlotDto> items)
    {
        var builder = ImmutableDictionary.CreateBuilder<byte, ItemStack>();

        foreach (var item in items)
            if (item.Container == ContainerMatrix.Equipment)
                builder[item.Slot] = ItemStack.FromRow(item);

        return builder.ToImmutable();
    }

    /// <summary>
    ///     Buff[slot*2]/[slot*2+1] pairing (value, remaining-ticks) -- same convention
    ///     <see cref="Fenrir.Application.Game.Domain.World.Zone" />'s own ClearAllBuffs/ApplyBuffWrites use for
    ///     this identical wire array. A brand-new character has no rows at all (creation never writes any buff), so
    ///     an empty <paramref name="buffs" /> collapses to the same all-zero snapshot the previous hardcode produced.
    /// </summary>
    private static BuffInfo BuildBuffInfo(IReadOnlyList<CharacterBuffDto> buffs)
    {
        var buff = new int[70];

        foreach (var row in buffs)
        {
            if (row.SlotIndex >= 35)
                continue;

            buff[row.SlotIndex * 2] = row.Value;
            buff[row.SlotIndex * 2 + 1] = row.RemainingLegacyTicks;
        }

        return WorldStateTemplates.ZeroedBuffInfo with { Buff = buff };
    }

    /// <summary>
    ///     One value per slot (not the id/duration pair <see cref="BuildBuffInfo" /> produces) -- ObjectForAvatar's own
    ///     view of the same buff snapshot.
    /// </summary>
    private static int[] BuildEffectValueForView(IReadOnlyList<CharacterBuffDto> buffs)
    {
        var effectValueForView = new int[35];

        foreach (var row in buffs)
            if (row.SlotIndex < 35)
                effectValueForView[row.SlotIndex] = row.Value;

        return effectValueForView;
    }
}
