using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.AntiCheat;
using Fenrir.Application.Game.Domain.Avatars;
using Fenrir.Application.Game.Domain.Costumes;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Mounts;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Social;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.StellarCores;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.ZoneLifecycle;
using Fenrir.Core.Packets.Shared;
using Fenrir.Domain.Game.GameData;
using Fenrir.Domain.Game.Stats;
using Fenrir.Protocol.Game;
using Fenrir.Security;
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
    IRuneRepository runes,
    PartyRegistry partyRegistry,
    ICharacterShardLocationRepository characterShardLocations,
    CharacterPresenceOwnership characterPresenceOwnership,
    ICharacterLogoutStateRepository logoutState,
    WorldStateService worldState,
    TowerWarState towerWar,
    ZoneCenterSiegeState zoneCenterSiegeState,
    TribeGuardCorridorState tribeGuardCorridorState,
    PopupEventState popupEventState,
    ValleyWarKillRegistry valleyWarCampaign,
    IOptions<GameServerOptions> options,
    ILogger<EnterWorldService> logger) : IEnterWorldService
{
    private const int Zone241TimeAvatarChangeInfoSort = 14;

    private const int ExperienceEventMessageSort = 56;

    private const int DropEventMessageSort = 57;

    private const int ContributionPointEventMessageSort = 58;

    private const int PetExperienceEventMessageSort = 901;

    private const int MountExperienceEventMessageSort = 902;

    private const int HeroRankPointStatSort = 904;

    private const int EnterWorldAvatarActionState = 1;

    public async ValueTask HandleAsync(EnterWorldRequest packet, IZoneSession zoneSession,
        CancellationToken cancellationToken)
    {
        var accountId = zoneSession.AccountId!.Value;
        var characterId = zoneSession.CharacterId!.Value;
        var presencePublished = false;

        async ValueTask RemovePublishedPresenceAsync()
        {
            if (!presencePublished)
                return;

            presencePublished = false;

            try
            {
                await characterPresenceOwnership.RemoveIfOwnerAsync(characterId, zoneSession.SessionId,
                    cancellationToken => characterShardLocations.RemoveAsync(characterId, options.Value.ShardId,
                        cancellationToken), CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Failed to remove the published cross-shard location for character {CharacterId} during aborted world entry",
                    characterId);
            }
        }

        if (!await firewall.IsAllowedAsync(zoneSession.RemoteEndPoint, cancellationToken))
        {
            logger.LogWarning(
                "Enter-world rejected for account {AccountId} character {CharacterId}: remote IP is firewalled",
                accountId, characterId);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        bool banIsActive;
        try
        {
            banIsActive = await BanAdmissionPolicy.IsBlockedAsync(bans, accountId, characterId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex,
                "Enter-world rejected for account {AccountId} character {CharacterId}: ban status could not be verified",
                accountId, characterId);
            zoneSession.Abort(DisconnectReason.ProcessingFault);
            return;
        }

        if (banIsActive)
        {
            logger.LogWarning("Enter-world rejected for account {AccountId} character {CharacterId}: ban is active",
                accountId, characterId);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (!ObfuscatedUidCodec.TryDecodeAccountId(packet.Id, out var decodedAccountId) ||
            decodedAccountId != accountId)
        {
            logger.LogWarning(
                "Enter-world rejected for account {AccountId} character {CharacterId}: obfuscated id mismatch",
                accountId, characterId);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

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

        if (character.Life <= 0)
        {
            logger.LogInformation(
                "Enter-world deferred for character {CharacterId}: the durable life state is dead and no validated resurrection has completed",
                characterId);
            zoneSession.Abort(DisconnectReason.StateViolation);
            return;
        }

        var ticketTargetMapId = zoneSession.TargetMapId;
        var isTicketTransfer = ticketTargetMapId is { } tmid && tmid != character.MapId;
        if (isTicketTransfer &&
            worldData.ZonesByNumber.TryGetValue(ticketTargetMapId!.Value, out var transferTargetDefinition))
        {
            var transferSpawn = transferTargetDefinition.FindSpawnPointFrom(character.MapId);
            var (spawnX, spawnY, spawnZ) = transferSpawn is null
                ? (transferTargetDefinition.Zone.DefaultSpawnX, transferTargetDefinition.Zone.DefaultSpawnY,
                    transferTargetDefinition.Zone.DefaultSpawnZ)
                : (transferSpawn.PosX, transferSpawn.PosY, transferSpawn.PosZ);
            character = character with
            {
                MapId = ticketTargetMapId.Value, PosX = spawnX, PosY = spawnY, PosZ = spawnZ
            };
        }

        if (packet.AvatarName != character.Name)
        {
            logger.LogWarning(
                "Enter-world rejected for character {CharacterId}: avatar name {SentName} does not match {ExpectedName}",
                characterId, packet.AvatarName, character.Name);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (!IsTribeAndPreviousTribeConsistent(character.Tribe, character.PreviousTribe))
        {
            logger.LogWarning(
                "Enter-world rejected for character {CharacterId}: tribe {Tribe}/previousTribe {PreviousTribe} are internally inconsistent",
                characterId, character.Tribe, character.PreviousTribe);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (!zones.TryGet(character.MapId, out var zone))
        {
            logger.LogWarning(
                "Character {CharacterId} is on map {MapId}, which this shard does not host -- aborting registration",
                characterId, character.MapId);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (zones.TryGetPlayerInOtherZone(characterId, zone, out var existingState, out var existingZone))
        {
            if (!existingState.IsMovingZone)
            {
                logger.LogError(
                    "Enter-world rejected for character {CharacterId}: already live on zone {ExistingMapId} " +
                    "without a pending transfer -- refusing duplicate admission on zone {MapId}",
                    characterId, existingZone.MapId, zone.MapId);
                zoneSession.Abort(DisconnectReason.StateViolation);
                return;
            }

            logger.LogWarning(
                "Character {CharacterId} entering zone {MapId} while a stale pending-transfer registration is " +
                "still live on zone {StaleMapId} -- evicting the stale connection so it can never resume as a " +
                "second live copy",
                characterId, zone.MapId, existingZone.MapId);

            existingState.Session.Abort(DisconnectReason.Evicted);
        }

        int? adjustedZone241TimeForEntry = null;

        if (options.Value.ChallengeContentEnabled &&
            WrapCheckSpecialDestinationCatalog.IsInstancedDestination(character.MapId))
        {
            if (character.Zone241Time < 1)
            {
                logger.LogWarning(
                    "Enter-world rejected for character {CharacterId}: no Legends-of-Darkness instance ticket remaining for zone {MapId}",
                    characterId, character.MapId);
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            int adjustedZone241Time;
            try
            {
                adjustedZone241Time = await characters.AdjustZone241TimeAsync(characterId, -1, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex,
                    "Enter-world rejected for character {CharacterId}: Legends-of-Darkness instance-ticket adjustment failed for zone {MapId}",
                    characterId, character.MapId);
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            character = character with { Zone241Time = adjustedZone241Time };
            adjustedZone241TimeForEntry = adjustedZone241Time;

            if (!worldData.MonstersById.ContainsKey(PersonalDungeonBossTables.ResolveCatalogD(character.MapId)))
            {
                logger.LogWarning(
                    "Enter-world rejected for character {CharacterId}: Legends-of-Darkness boss template missing for zone {MapId}",
                    characterId, character.MapId);
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }
        }

        async ValueTask CompleteWorldEntryAsync()
        {
            var (entryPosX, entryPosY, entryPosZ) = (character.PosX, character.PosY, character.PosZ);
            var entryHeading = character.Heading;

            var isMutedTask = mutes.IsActiveForCharacterAsync(characterId, cancellationToken);
            var guildTask = guilds.GetByCharacterAsync(characterId, cancellationToken);
            var tribeRoleTask = tribes.GetRoleForCharacterAsync(characterId, cancellationToken);
            var friendsTask = friends.GetByCharacterAsync(characterId, cancellationToken);
            var mentorTask = mentors.GetForCharacterAsync(characterId, cancellationToken);

            var heroRankPointsTask = heroRankings.GetPointsAsync(characterId,
                HeroRankPointAccumulator.CurrentPeriodKind,
                cancellationToken);

            var runeRowsTask = runes.GetRunesAsync(characterId, cancellationToken);

            var costumeRowsTask = characters.GetCostumesAsync(characterId, cancellationToken);

            var mountRowsTask = characters.GetMountsAsync(characterId, cancellationToken);

            var stellarCoreRowsTask = characters.GetStellarCoresAsync(characterId, cancellationToken);

            await Task.WhenAll(isMutedTask.AsTask(), guildTask.AsTask(), tribeRoleTask.AsTask(),
                friendsTask.AsTask(), mentorTask.AsTask(), heroRankPointsTask.AsTask(),
                runeRowsTask.AsTask(), costumeRowsTask.AsTask(),
                mountRowsTask.AsTask(), stellarCoreRowsTask.AsTask());

            var isMuted = isMutedTask.Result;
            var guildMembership = guildTask.Result;
            var tribeRole = tribeRoleTask.Result;
            var friendRows = friendsTask.Result;
            var mentorBond = mentorTask.Result;
            var heroRankPoints = heroRankPointsTask.Result ?? 0;
            var (runeSystem, runeSystemStat) = BuildRuneArrays(runeRowsTask.Result);
            var today = GameDate.Today();
            var entrySanitization = PlayerEnterDataSanitizer.Sanitize(bundle.Items, bundle.Hotkeys,
                costumeRowsTask.Result, stellarCoreRowsTask.Result, worldData.ItemsById, today, character.Life,
                character.Mana);
            var entryItems = entrySanitization.Items;

            foreach (var container in entrySanitization.CleanedContainers)
                await characters.ReplaceContainerAsync(characterId, container,
                    BuildContainerSlots(entryItems, container), cancellationToken);

            foreach (var hotkey in entrySanitization.ClearedHotkeys)
                await characters.UpsertHotkeySlotAsync(characterId, hotkey.Page, hotkey.KeyIndex, hotkey.Sort,
                    hotkey.Value1, hotkey.Value2, cancellationToken);

            if (entrySanitization.Life != character.Life || entrySanitization.Mana != character.Mana)
            {
                character = character with
                {
                    Life = entrySanitization.Life,
                    Mana = entrySanitization.Mana,
                    FlushSequence = checked(character.FlushSequence + 1)
                };
                await characters.ClampVitalsFloorAsync(characterId, character.FlushSequence, entrySanitization.Life,
                    entrySanitization.Mana, cancellationToken);
            }

            var (loadedCostumeWardrobe, loadedCostumeDate, loadedCostumeExpireDate) =
                CostumePersistenceCodec.Hydrate(entrySanitization.Costumes);
            var loadedCostumeIndex = CostumePersistenceCodec.NormalizeIndexOnLoad(character.CostumeIndex,
                loadedCostumeWardrobe);
            var costumeSweep = CostumePersistenceCodec.SweepExpiredRentSlots(loadedCostumeWardrobe,
                loadedCostumeDate, loadedCostumeExpireDate, loadedCostumeIndex, today);
            var costumeWardrobe = costumeSweep.Wardrobe;
            var costumeDate = costumeSweep.Date;
            var costumeExpireDate = costumeSweep.ExpireDate;
            var costumeIndex = costumeSweep.CostumeIndex;

            var loadedStellarCoreWardrobe = StellarCorePersistenceCodec.Hydrate(entrySanitization.StellarCores);
            var loadedStellarCoreExpireDate = StellarCoreExpireDateCodec.Decode(character.StellarCoreExpireDate);
            var loadedStellarCoreIndex = StellarCorePersistenceCodec.NormalizeIndexOnLoad(character.StellarCoreIndex,
                loadedStellarCoreWardrobe);
            var stellarCoreSweep = StellarCorePersistenceCodec.SweepExpiredRentSlots(loadedStellarCoreWardrobe,
                loadedStellarCoreExpireDate, loadedStellarCoreIndex, today);
            var stellarCoreWardrobe = stellarCoreSweep.Wardrobe;
            var stellarCoreExpireDate = stellarCoreSweep.ExpireDate;
            var stellarCoreIndex = stellarCoreSweep.CoreIndex;
            var mountGarageSlots = MountPersistenceCodec
                .Hydrate(mountRowsTask.Result)
                .SetItem(MountPersistenceCodec.PersistedGarageSlot,
                    (character.MountItemId, character.MountExpActivity, character.MountPower));
            var costumeSlots = BuildCostumeSlots(costumeWardrobe, costumeDate, costumeExpireDate);
            var mountSlots = BuildMountSlots(characterId, mountGarageSlots);
            var stellarCoreSlots = BuildStellarCoreSlots(characterId, stellarCoreWardrobe);

            if (entrySanitization.CostumesChanged || entrySanitization.StellarCoresChanged || costumeSweep.Changed ||
                stellarCoreSweep.Changed)
            {
                character = character with { FlushSequence = checked(character.FlushSequence + 1) };
                await characters.PersistProgressAsync(
                    [BuildEntryProgress(character, costumeIndex, stellarCoreIndex, stellarCoreExpireDate)],
                    BuildCostumeSlotTvps(characterId, costumeSlots), mountSlots, cancellationToken, stellarCoreSlots);
            }

            var persistedBuffs = BuildBuffInfo(bundle.Buffs);

            var guildSummary = guildMembership is { } membership
                ? await guilds.GetByIdAsync(membership.GuildId, cancellationToken)
                : null;
            var guildBuffType = guildSummary?.BuffType ?? 0;
            var guildBuffActive = guildSummary is { } summary && summary.BuffTime > 0 && summary.BuffState != 0;

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

            var rankResetDate = GameDate.Today();
            var rankBuffType = character.RankPointDate == rankResetDate ? character.RankBuffType : 0;
            var rankPoint = character.RankPointDate == rankResetDate ? character.RankPoint : 0;

            var enterSnapshot = new TaskCompletionSource<PlayerRuntimeState?>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var entered = zone.Post(ZoneCommand.Enter(characterId, new PlayerEnterData(
                zoneSession,
                character.Name,
                zoneSession.AccountGrade,
                zoneSession.AccountGrade,
                character.Tribe,
                character.Gender,
                character.HeadType,
                character.FaceType,
                character.Level,
                character.MapId,
                entryPosX,
                entryPosY,
                entryPosZ,
                entryHeading,
                entrySanitization.Life,
                character.MaxLife,
                entrySanitization.Mana,
                character.MaxMana,
                character.FlushSequence,
                Items: entryItems,
                IsMuted: isMuted,
                GuildId: guildMembership?.GuildId,
                GuildName: socialSnapshot.GuildName,
                GuildRoleDb: guildMembership?.Role ?? 0,
                GuildBuffType: guildBuffType,
                GuildBuffActive: guildBuffActive,
                TribeRole: tribeRole,
                FriendsBySlot: friendIdBySlot,
                Skills: bundle.Skills,
                Hotkeys: entrySanitization.Hotkeys,
                TeacherCharacterId: mentorBond?.TeacherCharacterId,
                StudentCharacterId: mentorBond?.StudentCharacterId,
                QuestProgress: new QuestProgress(character.QuestStepPermanent, character.QuestActiveId,
                    character.QuestSort, character.QuestTargetPhase, character.QuestKillCounter),
                MissionJoinWar: character.JoinWar,
                MissionKillOtherTribe: character.MissionKillOtherTribe,
                MissionKillMonster: character.MissionKillMonster,
                MissionPlayTime: character.MissionPlayTime,
                AutoHuntEnabled: false,
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
                Money: character.Money,
                ContributionPoints: character.ContributionPoints,
                TeacherPoint: character.TeacherPoint,
                Level2: character.Level2,
                Exp2: character.Exp2,
                Zone241Time: character.Zone241Time,
                HeroRankPoints: heroRankPoints,
                EatLifePotion: character.EatLifePotion,
                EatManaPotion: character.EatManaPotion,
                EatStrPotion: character.EatStrPotion,
                EatDexPotion: character.EatDexPotion,
                EatElePotion: character.EatElePotion,
                DropItemTime: character.DropItemTime,
                ImproveItemValue: character.ImproveItemValue,
                AddItemValue: character.AddItemValue,
                HighItemValue: character.HighItemValue,
                TaiyanKeyTimer: character.TaiyanKeyTimer,
                WarPoint: character.WarPoint,
                BloodCoin: character.BloodCoin,
                PremiumExpireUtc: character.PremiumExpireUtc,
                PreviousTribe: character.PreviousTribe,
                StoreMoney: character.StoreMoney,
                BigMoney: character.BigMoney,
                InventoryDate: VaultDateNormalization.NormalizeIfExpired(character.InventoryDate, GameDate.Today()),
                StoreDate: character.StoreDate,
                PetBagDate: character.PetBagDate,
                PlayTime1: character.PlayTime1,
                PlayTime3: character.PlayTime3,
                HsbStoneRewardClaimed: character.HsbStoneRewardClaimed == 1,
                TowerCpMilestoneCounter: character.TowerCpMilestoneCounter,
                WarriorPill: character.WarriorPill,
                WarriorScroll: character.WarriorScroll,
                SilverTime: character.SilverTime,
                GoldTime: character.GoldTime,
                DoubleKillNumTime: character.DoubleKillNumTime,
                DoubleKillExpTime: character.DoubleKillExpTime,
                DoubleKillNumTime2: character.DoubleKillNumTime2,
                M15PetLuckyBoxPity: character.M15PetLuckyBoxPity,
                SourceIp: SessionSourceIp.Normalize(zoneSession.RemoteEndPoint),
                RuneSystem: runeSystem,
                RuneSystemStat: runeSystemStat,
                ActionSort: 0,
                ActionSkillNumber: 0,
                ActionSkillGradeNum1: 0,
                ActionSkillGradeNum2: 0,
                PetActionSort: 0,
                PetActionFront: 0,
                PetActionTargetLocationX: entryPosX,
                PetActionTargetLocationY: entryPosY,
                PetActionTargetLocationZ: entryPosZ,
                MountItemId: character.MountItemId,
                MountExpActivity: character.MountExpActivity,
                MountPower: character.MountPower,
                MountSlotIndex: character.MountSlotIndex,
                MountTime: character.MountTime,
                VisibleState: character.VisibleState,
                SpecialState: character.SpecialState,
                UseOrnament: character.UseOrnament,
                PetExpX2Time: character.PetExpX2Time,
                AnimalAbsorbTime: character.AnimalAbsorbTime,
                AnimalAbsorbState: character.AnimalAbsorbState,
                CostumeIndex: costumeIndex,
                CostumeWardrobe: costumeWardrobe,
                CostumeDate: costumeDate,
                CostumeExpireDate: costumeExpireDate,
                AutoBuffTime: character.AutoBuffTime,
                AutoBuffSkill: AutoBuffSkillCodec.Decode(character.AutoBuffSkill),
                RankPointDate: rankResetDate,
                RankBuffType: rankBuffType,
                RankPoint: rankPoint,
                CloakLuckyBoxPity: character.CloakLuckyBoxPity,
                CloakVariantBoxPity: character.CloakVariantBoxPity,
                MountVariantBoxPity: character.MountVariantBoxPity,
                BuffX2Time: character.BuffX2Time,
                AutoHuntPaidDayBudget: VaultDateNormalization.NormalizeIfExpired(character.AutoTime,
                    GameDate.Today()),
                AutoHuntPaidMinuteBudget: character.AutoTime2,
                ProtectForDeath: character.ProtectForDeath,
                AnimalDoubleExp: character.AnimalDoubleExp,
                DmgBoost: character.DmgBoost,
                HPBoost: character.HPBoost,
                CriBoost: character.CriBoost,
                MountGarageSlots: mountGarageSlots,
                Buffs: persistedBuffs,
                SkillPoints: character.SkillPoints,
                ProtectForHalo: character.ProtectForHalo,
                BonusItemLevel: character.BonusItemLevel,
                BonusItemValue: character.BonusItemValue,
                TribeNotifyScrollCount: character.TribeNotifyScrollCount,
                TribeFourReturnAllowance: character.TribeFourReturnAllowance,
                ProtectForRefine: character.ProtectForRefine,
                ProtectForDestroy: character.ProtectForDestroy,
                ProtectForCostume: character.ProtectForCostume,
                ProtectForDestroy2: character.ProtectForDestroy2,
                LodRounds: character.LodRounds,
                StellarCoreIndex: stellarCoreIndex,
                StellarCoreWardrobe: stellarCoreWardrobe,
                StellarCoreExpireDate: stellarCoreExpireDate,
                EliteDungeonTime: character.EliteDungeonTime,
                DungeonKeyTime: character.DungeonKeyTime,
                IvyHallTicketTime: character.IvyHallTicketTime,
                ScrollOfSeekersTime: character.ScrollOfSeekersTime,
                FightingGodForDestroy: character.FightingGodForDestroy), enterSnapshot));

            if (!entered)
            {
                logger.LogError(
                    "Zone {MapId} inbox full: dropped Enter for character {CharacterId} -- aborting session",
                    zone.MapId, characterId);
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            zoneSession.CurrentZone = zone;

            var admitted = await enterSnapshot.Task.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken)
                .ConfigureAwait(false);

            if (admitted is null)
            {
                logger.LogError(
                    "Zone {MapId} did not admit character {CharacterId} after accepting Enter -- aborting session",
                    zone.MapId, characterId);
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            var partyRoster = partyRegistry.GetRoster(characterId);
            var projectionSocial = socialSnapshot with
            {
                PartyNames = partyRoster.Select(static member => member.Name).ToArray(),
                PartyName = PartyIdentityResolver.ResolveCurrentPartyName(partyRegistry, characterId, admitted.Name,
                    static _ => null)
            };
            var avatarProjection = AvatarInfoFactory.CreateWorldEntryProjection(admitted, projectionSocial);

            if (adjustedZone241TimeForEntry is { } adjustedZone241Time)
                zoneSession.Send(new AvatarStateFlagResponse
                {
                    ServerIndex = characterId,
                    UniqueNumber = unchecked((uint)characterId),
                    Sort = Zone241TimeAvatarChangeInfoSort,
                    Value01 = admitted.ContributionPoints,
                    Value02 = admitted.RebirthCount,
                    Value03 = adjustedZone241Time
                });

            zoneSession.Send(new EnterWorldResponse
            {
                AvatarInfo = avatarProjection.AvatarInfo,
                BuffInfo = avatarProjection.BuffInfo
            });

            zoneSession.Send(new WorldSnapshotResponse
            {
                WorldInfo = ZoneCenterSiegeProjection.Apply(
                    WorldStateProjection.Apply(
                        PopupEventWorldProjection.Apply(
                            GuildRankingProjection.Apply(WorldStateTemplates.ZeroedWorldInfo, guildRanking.Top),
                            popupEventState),
                        worldState) with { Zone200TypeState = valleyWarCampaign.GetWorldInfoState() },
                    zoneCenterSiegeState, tribeGuardCorridorState),
                TribeInfo = WorldStateTemplates.ZeroedTribeInfo
            });

            var avatarAction = zone.BuildAvatarActionRecv(admitted, avatarProjection.Pose,
                EnterWorldAvatarActionState);
            zoneSession.Send(avatarAction with
            {
                Data = avatarAction.Data with { PartyName = avatarProjection.PartyName }
            });

            SendRatioEventMessages(zoneSession, zone);

            if (admitted.HeroRankPoints > 0)
                zoneSession.Send(new AvatarStatUpdateResponse
                {
                    Sort = HeroRankPointStatSort,
                    Value = admitted.HeroRankPoints,
                    Value2 = 0
                });

            await characterPresenceOwnership.PublishAsync(characterId, zoneSession.SessionId,
                    publishCancellationToken => characterShardLocations.UpsertAsync(characterId, options.Value.ShardId,
                        character.MapId, character.Name, character.Tribe, publishCancellationToken), cancellationToken)
                .ConfigureAwait(false);
            presencePublished = true;

            try
            {
                await logoutState.UpsertAsync(characterId, character.MapId,
                    (int)entryPosX, (int)entryPosY, (int)entryPosZ,
                    admitted.Life, admitted.Mana, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex,
                    "Failed to capture the logout-info snapshot for character {CharacterId} on map {MapId} after " +
                    "a successful world entry; the entry itself stands, only the snapshot write is skipped",
                    characterId, character.MapId);
            }

            zoneSession.Send(towerWar.BuildStatusSnapshot());

            var bootstrapAcknowledgement = new TaskCompletionSource<ZoneCommandResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            if (!zone.Post(ZoneCommand.AcknowledgeEnterBootstrap(characterId, zoneSession.SessionId,
                    bootstrapAcknowledgement)))
                throw new InvalidOperationException(
                    $"Zone {zone.MapId} inbox rejected the bootstrap acknowledgement for character {characterId}.");

            var acknowledgement = await bootstrapAcknowledgement.Task.WaitAsync(TimeSpan.FromSeconds(2),
                cancellationToken).ConfigureAwait(false);
            if (acknowledgement.Kind != ZoneCommandResultKind.Applied)
                throw new InvalidOperationException(
                    $"Zone {zone.MapId} rejected the bootstrap acknowledgement for character {characterId}: " +
                    acknowledgement.Cause);

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
            await RemovePublishedPresenceAsync();
            logger.LogError(ex,
                "Enter-world processing faulted for account {AccountId} character {CharacterId} on map {MapId} -- " +
                "the client may already hold a partial handshake if some of the three response payloads were " +
                "already sent; aborting session",
                accountId, characterId, character.MapId);
            zoneSession.Abort(DisconnectReason.ProcessingFault);
        }
        catch (OperationCanceledException)
        {
            await RemovePublishedPresenceAsync();
            throw;
        }
    }

    private void SendRatioEventMessages(IZoneSession zoneSession, Zone zone)
    {
        if (!options.Value.Zones.TryGetValue(zone.MapId, out var config))
            return;

        var isZone126Type = zone.IsZone126TypeZone;

        if (config.GeneralExperienceRatio != 0)
            zoneSession.Send(new AvatarStatUpdateResponse
            {
                Sort = ExperienceEventMessageSort,
                Value = EncodeRatioEventValue(config.GeneralExperienceRatio, isZone126Type),
                Value2 = 0
            });

        if (config.NormalItemDropRatio != 0)
            zoneSession.Send(new AvatarStatUpdateResponse
            {
                Sort = DropEventMessageSort,
                Value = EncodeRatioEventValue(config.NormalItemDropRatio, isZone126Type),
                Value2 = 0
            });

        if (config.KillOtherTribeAddValue != 0)
            zoneSession.Send(new AvatarStatUpdateResponse
            {
                Sort = ContributionPointEventMessageSort,
                Value = config.KillOtherTribeAddValue,
                Value2 = 0
            });

        if (config.PetExperienceRatio != 0)
            zoneSession.Send(new AvatarStatUpdateResponse
            {
                Sort = PetExperienceEventMessageSort,
                Value = (int)(config.PetExperienceRatio * 0.1f),
                Value2 = 0
            });

        if (config.MountExperienceRatio != 0)
            zoneSession.Send(new AvatarStatUpdateResponse
            {
                Sort = MountExperienceEventMessageSort,
                Value = config.MountExperienceRatio,
                Value2 = 0
            });
    }

    private static int EncodeRatioEventValue(int configuredRatio, bool isZone126Type)
    {
        var scaled = (int)(configuredRatio * 0.1f * 10.0f);
        return isZone126Type ? scaled * 2 : scaled + 1;
    }

    private static bool IsTribeAndPreviousTribeConsistent(byte tribe, byte previousTribe)
    {
        return tribe switch
        {
            0 or 1 or 2 => previousTribe == tribe,
            3 => previousTribe is 0 or 1 or 2,
            _ => false
        };
    }

    private static CharacterItemSlotTvp[] BuildContainerSlots(IReadOnlyList<CharacterItemSlotDto> items,
        byte container)
    {
        return items.Where(item => item.Container == container)
            .Select(item => new CharacterItemSlotTvp(item.Slot, item.ItemId, item.Quantity, item.Enchant,
                item.Combine, item.Refine, item.Socket, item.SocketGem1, item.SocketGem2, item.SocketGem3,
                item.ExpireDate, item.Serial, item.XPos, item.YPos))
            .ToArray();
    }

    private static CharacterCostumeSlotDto[] BuildCostumeSlots(ImmutableArray<int> wardrobe,
        ImmutableArray<int> date, ImmutableArray<int> expireDate)
    {
        var slots = new List<CharacterCostumeSlotDto>();

        for (var slot = 0; slot < CostumePersistenceCodec.SlotCount && slot < wardrobe.Length; slot++)
        {
            var itemId = wardrobe[slot];
            if (itemId == 0)
                continue;

            slots.Add(new CharacterCostumeSlotDto((byte)slot, itemId,
                slot < date.Length ? date[slot] : 0, slot < expireDate.Length ? expireDate[slot] : 0));
        }

        return [.. slots];
    }

    private static CharacterCostumeSlotTvp[] BuildCostumeSlotTvps(int characterId,
        IReadOnlyList<CharacterCostumeSlotDto> slots)
    {
        return slots.Select(slot => new CharacterCostumeSlotTvp(characterId, slot.Slot, slot.ItemId,
            slot.ItemValue, slot.ExpireDate)).ToArray();
    }

    private static CharacterMountSlotTvp[] BuildMountSlots(int characterId,
        ImmutableArray<(int ItemId, int ExpActivity, int Power)> garage)
    {
        var slots = new List<CharacterMountSlotTvp>();

        for (var slot = 0; slot < MountPersistenceCodec.SlotCount && slot < garage.Length; slot++)
            if (garage[slot].ItemId > 0)
                slots.Add(new CharacterMountSlotTvp(characterId, (byte)slot, garage[slot].ItemId,
                    garage[slot].ExpActivity, garage[slot].Power));

        return [.. slots];
    }

    private static CharacterStellarCoreSlotTvp[] BuildStellarCoreSlots(int characterId,
        ImmutableArray<int> wardrobe)
    {
        var slots = new List<CharacterStellarCoreSlotTvp>();

        for (var slot = 0; slot < StellarCorePersistenceCodec.SlotCount && slot < wardrobe.Length; slot++)
            if (wardrobe[slot] > 0)
                slots.Add(new CharacterStellarCoreSlotTvp(characterId, (byte)slot, wardrobe[slot]));

        return [.. slots];
    }

    private static CharacterProgressTvp BuildEntryProgress(CharacterWorldSnapshotDto character, int costumeIndex,
        int stellarCoreIndex, ImmutableArray<int> stellarCoreExpireDate)
    {
        return new CharacterProgressTvp(
            character.CharacterId,
            character.FlushSequence,
            character.Level,
            character.Level2,
            character.Experience,
            character.Life,
            character.MaxLife,
            character.Mana,
            character.MaxMana,
            character.StatVit,
            character.StatStr,
            character.StatInt,
            character.StatDex,
            character.StatPoints,
            character.SkillPoints,
            character.ContributionPoints,
            character.Exp2,
            character.RebirthCount,
            character.EatLifePotion,
            character.EatManaPotion,
            character.EatStrPotion,
            character.EatDexPotion,
            character.EatElePotion,
            character.DropItemTime,
            character.M15PetLuckyBoxPity,
            character.MountItemId,
            character.MountExpActivity,
            character.MountPower,
            character.MountSlotIndex,
            character.MountTime,
            character.VisibleState,
            character.SpecialState,
            character.UseOrnament ? 1 : 0,
            character.Title,
            character.Halo,
            character.TeacherPoint,
            0,
            0,
            character.PetExpX2Time,
            character.AnimalAbsorbTime,
            character.AnimalAbsorbState,
            costumeIndex,
            character.ProtectForHalo,
            character.BonusItemLevel,
            character.BonusItemValue,
            character.TribeNotifyScrollCount,
            character.TribeFourReturnAllowance,
            character.BottleSlots,
            character.DrunkBottleIndex,
            character.AutoBuffTime,
            character.AutoBuffSkill,
            character.RankPointDate,
            character.RankBuffType,
            character.AutoTime,
            character.AutoTime2,
            character.BuffX2Time,
            character.PremiumExpireUtc,
            character.PetGrowth,
            character.PetActivity,
            character.ImproveItemValue,
            character.AddItemValue,
            character.HighItemValue,
            character.TaiyanKeyTimer,
            character.RankPoint,
            character.CloakLuckyBoxPity,
            character.CloakVariantBoxPity,
            character.MountVariantBoxPity,
            character.ProtectForRefine,
            character.ProtectForDestroy,
            character.ProtectForCostume,
            character.ProtectForDestroy2,
            character.LodRounds,
            StellarCoreExpireDateCodec.Encode(stellarCoreExpireDate),
            character.EliteDungeonTime,
            character.DungeonKeyTime,
            character.IvyHallTicketTime,
            character.ScrollOfSeekersTime,
            character.FightingGodForDestroy,
            character.PetBagDate,
            character.PlayTime1,
            character.PlayTime3,
            character.HsbStoneRewardClaimed,
            character.TowerCpMilestoneCounter,
            character.InventoryDate,
            character.StoreDate,
            character.WarriorPill,
            character.WarriorScroll,
            character.SilverTime,
            character.GoldTime,
            character.DoubleKillNumTime,
            character.DoubleKillExpTime,
            character.DoubleKillNumTime2,
            character.ProtectForDeath,
            character.AnimalDoubleExp,
            character.DmgBoost,
            character.HPBoost,
            character.CriBoost,
            stellarCoreIndex);
    }

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

    private static (ImmutableArray<int> RuneSystem, ImmutableArray<int> RuneSystemStat) BuildRuneArrays(
        IReadOnlyCollection<CharacterRuneSocketDto> rows)
    {
        var runeSystem = new int[RuneStatDecoder.SocketCount];
        var runeSystemStat = new int[RuneStatDecoder.SocketCount];

        foreach (var row in rows)
            if (row.SocketIndex < RuneStatDecoder.SocketCount)
            {
                runeSystem[row.SocketIndex] = row.RuneItemId;
                runeSystemStat[row.SocketIndex] = row.RuneStat;
            }

        return (runeSystem.ToImmutableArray(), runeSystemStat.ToImmutableArray());
    }
}
