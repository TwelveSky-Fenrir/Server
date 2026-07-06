using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Avatars;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.Social;
using Fenrir.Application.Game.Domain.World;
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
    ICharacterShardLocationRepository characterShardLocations,
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

        // A client cannot enter the world already mid-action (move/skill/etc).
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

        // The character's persisted map must be one this shard hosts (ADR-0012).
        if (!zones.TryGet(character.MapId, out var zone))
        {
            logger.LogWarning(
                "Character {CharacterId} is on map {MapId}, which this shard does not host -- aborting registration",
                characterId, character.MapId);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var equipmentContainer = BuildEquipmentContainer(bundle.Items);
        var attributes = new CharacterBaseAttributes(character.StatVit, character.StatStr, character.StatInt,
            character.StatDex, character.Level, character.Tribe, character.Title, character.Halo,
            character.RebirthCount);

        var petItemId = PetSlots.ResolveEquippedPetItemId(bundle.Items);
        var petContribution = PetGrowthCalculator.Compute(petItemId, character.PetGrowth, character.PetActivity,
            worldData.ItemsById);

        var stats = EquipmentService.RecomputeStats(attributes, equipmentContainer, worldData,
            pet: petContribution);

        // Loaded once here and cached on PlayerRuntimeState -- never re-queried per chat message afterwards.
        var isMutedTask = mutes.IsActiveForCharacterAsync(characterId, cancellationToken);
        var guildTask = guilds.GetByCharacterAsync(characterId, cancellationToken);
        var tribeRoleTask = tribes.GetRoleForCharacterAsync(characterId, cancellationToken);
        var friendsTask = friends.GetByCharacterAsync(characterId, cancellationToken);
        var mentorTask = mentors.GetForCharacterAsync(characterId, cancellationToken);

        // Cross-shard character-location directory (runtime.CharacterShardLocation): a same-shard-miss
        // fallback for whisper/friend-locate/guild-find. Per-connection, once-per-world-entry cost, not a
        // tick or per-packet hot path, so an extra awaited stored-procedure call here alongside the others is
        // unremarkable. Intra-shard zone-to-zone handoffs never call this again -- ShardId never changes on a
        // same-shard hop, and MapId staleness for a character who has since wandered to another map on the
        // SAME shard is an accepted bound (this directory is a same-shard-miss fallback only).
        var shardLocationUpsertTask = characterShardLocations.UpsertAsync(characterId, options.Value.ShardId,
            character.MapId, character.Name, character.Tribe, cancellationToken);

        await Task.WhenAll(isMutedTask.AsTask(), guildTask.AsTask(), tribeRoleTask.AsTask(),
            friendsTask.AsTask(), mentorTask.AsTask(), shardLocationUpsertTask.AsTask());

        var isMuted = isMutedTask.Result;
        var guildMembership = guildTask.Result;
        var tribeRole = tribeRoleTask.Result;
        var friendRows = friendsTask.Result;
        var mentorBond = mentorTask.Result;

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
            AvatarInfo = AvatarInfoFactory.CreateForCharacter(character, bundle.Items, socialSnapshot),
            BuffInfo = WorldStateTemplates.ZeroedBuffInfo
        };
        zoneSession.SendRaw(ZoneMessageFactory.Encode(in registerRecv));

        var broadcastWorldInfo = new WorldSnapshotResponse
        {
            WorldInfo = GuildRankingProjection.Apply(WorldStateTemplates.ZeroedWorldInfo, guildRanking.Top),
            TribeInfo = WorldStateTemplates.ZeroedTribeInfo
        };
        zoneSession.SendRaw(ZoneMessageFactory.Encode(in broadcastWorldInfo));

        // Self-spawn: Zone doesn't know this player exists yet (ZoneCommand.Enter below is only posted, not ticked).
        zoneSession.Send(new AvatarActionResponse
        {
            ServerIndex = characterId,
            UniqueNumber = unchecked((uint)characterId),
            Data = new ObjectForAvatar
            {
                VisibleState = 0,
                SpecialState = 0,
                KillOtherTribe = 0,
                GoodFellow = 0,
                GuildName = socialSnapshot.GuildName,
                GuildRole = socialSnapshot.GuildRoleWire,
                CallName = socialSnapshot.CallName,
                GuildMarkEffect = 0,
                Name = character.Name,
                Tribe = character.Tribe,
                PreviousTribe = 0,
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
                    Location = [character.PosX, character.PosY, character.PosZ],
                    TargetLocation = [character.PosX, character.PosY, character.PosZ],
                    Front = character.Heading,
                    TargetFront = character.Heading,
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
                EffectValueForView = new int[35],
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
                PetLocation = new float[3],
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
            Exp2: character.Exp2)));

        // A dropped Enter is never replayed -- the character would stay permanently invisible despite the two
        // packets above already telling the client registration succeeded, so treat it as fatal.
        if (!entered)
        {
            logger.LogError("Zone {MapId} inbox full: dropped Enter for character {CharacterId} -- aborting session",
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

    private static ImmutableDictionary<byte, ItemStack> BuildEquipmentContainer(
        IReadOnlyList<CharacterItemSlotDto> items)
    {
        var builder = ImmutableDictionary.CreateBuilder<byte, ItemStack>();

        foreach (var item in items)
            if (item.Container == ContainerMatrix.Equipment)
                builder[item.Slot] = ItemStack.FromRow(item);

        return builder.ToImmutable();
    }
}
