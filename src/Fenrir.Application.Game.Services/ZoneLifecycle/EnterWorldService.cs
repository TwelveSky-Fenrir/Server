using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.AntiCheat;
using Fenrir.Application.Game.Domain.Avatars;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Social;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Domain.Game.GameData;
using Fenrir.Application.Game.Sessions;
using Fenrir.Domain.Game.Stats;
using Fenrir.Core.Packets.Shared;
using Fenrir.Network.Dispatch.Sessions;
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
    ICharacterShardLocationRepository characterShardLocations,
    ICharacterLogoutStateRepository logoutState,
    WorldStateService worldState,
    TowerWarState towerWar,
    ZoneCenterSiegeState zoneCenterSiegeState,
    TribeGuardCorridorState tribeGuardCorridorState,
    IOptions<GameServerOptions> options,
    ILogger<EnterWorldService> logger) : IEnterWorldService
{
    private const int Zone241TimeAvatarChangeInfoSort = 14;

    public async ValueTask HandleAsync(EnterWorldRequest packet, ZoneClientSession zoneSession,
        CancellationToken cancellationToken)
    {
        var accountId = zoneSession.AccountId!.Value;
        var characterId = zoneSession.CharacterId!.Value;

        if (!await firewall.IsAllowedAsync(zoneSession.RemoteEndPoint, cancellationToken))
        {
            logger.LogWarning(
                "Enter-world rejected for account {AccountId} character {CharacterId}: remote IP is firewalled",
                accountId, characterId);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (await bans.IsActiveForCharacterAsync(characterId, cancellationToken))
        {
            logger.LogWarning("Enter-world rejected for character {CharacterId}: character is GM-banned",
                characterId);
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

        if (!WrapCheckSpecialDestinationCatalog.IsInstancedDestination(character.MapId))
        {
            var (healedMapId, healedPosX, healedPosY, healedPosZ) = ZoneTribeSelfHeal.Apply(character.Tribe,
                character.MapId, character.PosX, character.PosY, character.PosZ);

            if (healedMapId != character.MapId)
                character = character with
                {
                    MapId = healedMapId, PosX = healedPosX, PosY = healedPosY, PosZ = healedPosZ
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

        if (WrapCheckSpecialDestinationCatalog.IsInstancedDestination(character.MapId))
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

            if (!worldData.MonstersById.ContainsKey(PersonalDungeonBossTables.ResolveCatalogD(character.MapId)))
            {
                logger.LogWarning(
                    "Enter-world rejected for character {CharacterId}: Legends-of-Darkness boss template missing for zone {MapId}",
                    characterId, character.MapId);
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            zoneSession.Send(new AvatarStateFlagResponse
            {
                ServerIndex = characterId,
                UniqueNumber = unchecked((uint)characterId),
                Sort = Zone241TimeAvatarChangeInfoSort,
                Value01 = character.ContributionPoints,
                Value02 = character.RebirthCount,
                Value03 = adjustedZone241Time
            });
        }

        async ValueTask CompleteWorldEntryAsync()
        {
            var (claimedPosX, claimedPosY, claimedPosZ) = isTicketTransfer
                ? (character.PosX, character.PosY, character.PosZ)
                : (packet.Action.Location[0], packet.Action.Location[1], packet.Action.Location[2]);
            var claimedFront = packet.Action.Front;

            var equipmentContainer = BuildEquipmentContainer(bundle.Items);
            var attributes = new CharacterBaseAttributes(character.StatVit, character.StatStr, character.StatInt,
                character.StatDex, character.Level, character.Tribe, character.PreviousTribe, character.Title,
                character.Halo, character.RebirthCount, character.Level2);

            var petItemId = PetSlots.ResolveEquippedPetItemId(bundle.Items);
            var petContribution = PetGrowthCalculator.Compute(petItemId, character.PetGrowth, character.PetActivity,
                worldData.ItemsById);

            var stats = EquipmentService.RecomputeStats(attributes, equipmentContainer, worldData,
                pet: petContribution);

            var isMutedTask = mutes.IsActiveForCharacterAsync(characterId, cancellationToken);
            var guildTask = guilds.GetByCharacterAsync(characterId, cancellationToken);
            var tribeRoleTask = tribes.GetRoleForCharacterAsync(characterId, cancellationToken);
            var friendsTask = friends.GetByCharacterAsync(characterId, cancellationToken);
            var mentorTask = mentors.GetForCharacterAsync(characterId, cancellationToken);

            var heroRankPointsTask = heroRankings.GetPointsAsync(characterId,
                HeroRankPointAccumulator.CurrentPeriodKind,
                cancellationToken);

            var shardLocationUpsertTask = characterShardLocations.UpsertAsync(characterId, options.Value.ShardId,
                character.MapId, character.Name, character.Tribe, cancellationToken);

            var runeRowsTask = runes.GetRunesAsync(characterId, cancellationToken);

            await Task.WhenAll(isMutedTask.AsTask(), guildTask.AsTask(), tribeRoleTask.AsTask(),
                friendsTask.AsTask(), mentorTask.AsTask(), heroRankPointsTask.AsTask(),
                shardLocationUpsertTask.AsTask(), runeRowsTask.AsTask());

            var isMuted = isMutedTask.Result;
            var guildMembership = guildTask.Result;
            var tribeRole = tribeRoleTask.Result;
            var friendRows = friendsTask.Result;
            var mentorBond = mentorTask.Result;
            var heroRankPoints = heroRankPointsTask.Result ?? 0;
            var (runeSystem, runeSystemStat) = BuildRuneArrays(runeRowsTask.Result);

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

            var registerRecv = new EnterWorldResponse
            {
                AvatarInfo = AvatarInfoFactory.CreateForCharacter(character, bundle.Items, socialSnapshot,
                    bundle.Skills, bundle.Hotkeys),
                BuffInfo = BuildBuffInfo(bundle.Buffs)
            };
            zoneSession.Send(in registerRecv);

            var broadcastWorldInfo = new WorldSnapshotResponse
            {
                WorldInfo = ZoneCenterSiegeProjection.Apply(
                    WorldStateProjection.Apply(
                        GuildRankingProjection.Apply(WorldStateTemplates.ZeroedWorldInfo, guildRanking.Top),
                        worldState),
                    zoneCenterSiegeState, tribeGuardCorridorState),
                TribeInfo = WorldStateTemplates.ZeroedTribeInfo
            };
            zoneSession.Send(in broadcastWorldInfo);

            zoneSession.Send(new AvatarActionResponse
            {
                ServerIndex = characterId,
                UniqueNumber = unchecked((uint)characterId),
                Data = new ObjectForAvatar
                {
                    VisibleState = 1,
                    SpecialState = 0,
                    KillOtherTribe = 0,
                    GoodFellow = 0,
                    GuildName = socialSnapshot.GuildName,
                    GuildRole = socialSnapshot.GuildRoleWire,
                    CallName = socialSnapshot.CallName,
                    GuildMarkEffect = 0,
                    Name = character.Name,
                    Tribe = character.Tribe,
                    PreviousTribe = character.PreviousTribe,
                    Gender = character.Gender,
                    HeadType = character.HeadType,
                    FaceType = character.FaceType,
                    Level1 = character.Level,
                    Level2 = character.Level2,
                    EquipForView = EquipmentViewCodec.BuildEquipForView(bundle.Items),
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
                        Location = [claimedPosX, claimedPosY, claimedPosZ],
                        TargetLocation = [claimedPosX, claimedPosY, claimedPosZ],
                        Front = claimedFront,
                        TargetFront = claimedFront,
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
                    PetLocation = [claimedPosX, claimedPosY, claimedPosZ],
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
                claimedPosX,
                claimedPosY,
                claimedPosZ,
                claimedFront,
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
                GuildBuffType: guildBuffType,
                GuildBuffActive: guildBuffActive,
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
                Zone241Time: character.Zone241Time,
                HeroRankPoints: heroRankPoints,
                EatLifePotion: character.EatLifePotion,
                EatManaPotion: character.EatManaPotion,
                EatStrPotion: character.EatStrPotion,
                EatDexPotion: character.EatDexPotion,
                EatElePotion: character.EatElePotion,
                DropItemTime: character.DropItemTime,
                WarPoint: character.WarPoint,
                PremiumExpireUtc: character.PremiumExpireUtc,
                PreviousTribe: character.PreviousTribe,
                StoreMoney: character.StoreMoney,
                BigMoney: character.BigMoney,
                InventoryDate: VaultDateNormalization.NormalizeIfExpired(character.InventoryDate, GameDate.Today()),
                StoreDate: character.StoreDate,
                PetBagDate: character.PetBagDate,
                M15PetLuckyBoxPity: character.M15PetLuckyBoxPity,
                SourceIp: SessionSourceIp.Normalize(zoneSession.RemoteEndPoint),
                RuneSystem: runeSystem,
                RuneSystemStat: runeSystemStat,
                ActionSort: packet.Action.Sort,
                ActionSkillNumber: packet.Action.SkillNumber,
                ActionSkillGradeNum1: packet.Action.SkillGradeNum1,
                ActionSkillGradeNum2: packet.Action.SkillGradeNum2,
                PetActionSort: packet.Action.PetSort,
                PetActionFront: packet.Action.PetFront,
                PetActionTargetLocationX: packet.Action.PetTargetLocation[0],
                PetActionTargetLocationY: packet.Action.PetTargetLocation[1],
                PetActionTargetLocationZ: packet.Action.PetTargetLocation[2],
                MountItemId: character.MountItemId,
                MountExpActivity: character.MountExpActivity,
                MountPower: character.MountPower,
                MountSlotIndex: character.MountSlotIndex,
                MountTime: character.MountTime)));

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

            try
            {
                await logoutState.UpsertAsync(characterId, character.MapId,
                    (int)claimedPosX, (int)claimedPosY, (int)claimedPosZ,
                    character.Life, character.Mana, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex,
                    "Failed to capture the logout-info snapshot for character {CharacterId} on map {MapId} after " +
                    "a successful world entry; the entry itself stands, only the snapshot write is skipped",
                    characterId, character.MapId);
            }

            zoneSession.Send(towerWar.BuildStatusSnapshot());
        }

        try
        {
            await CompleteWorldEntryAsync();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex,
                "Enter-world processing faulted for account {AccountId} character {CharacterId} on map {MapId} -- " +
                "the client may already hold a partial handshake if some of the three response payloads were " +
                "already sent; aborting session",
                accountId, characterId, character.MapId);
            zoneSession.Abort(DisconnectReason.ProcessingFault);
        }
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

    private static ImmutableDictionary<byte, ItemStack> BuildEquipmentContainer(
        IReadOnlyList<CharacterItemSlotDto> items)
    {
        var builder = ImmutableDictionary.CreateBuilder<byte, ItemStack>();

        foreach (var item in items)
            if (item.Container == ContainerMatrix.Equipment)
                builder[item.Slot] = ItemStack.FromRow(item);

        return builder.ToImmutable();
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

    private static int[] BuildEffectValueForView(IReadOnlyList<CharacterBuffDto> buffs)
    {
        var effectValueForView = new int[35];

        foreach (var row in buffs)
            if (row.SlotIndex < 35)
                effectValueForView[row.SlotIndex] = row.Value;

        return effectValueForView;
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
