using System.Collections.Immutable;
using Fenrir.Application.Game.Avatars;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Guilds;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.Pets;
using Fenrir.Application.Game.Quests;
using Fenrir.Application.Game.Social;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.World;
using Fenrir.Application.Game.Handlers;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Serialization.Wire;
using Fenrir.Data.Admin;
using Fenrir.Data.Characters;
using Fenrir.Data.Guilds;
using Fenrir.Data.Security;
using Fenrir.Data.Social;
using Fenrir.Data.Tribes;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.ZoneLifecycle.Services;

/// <summary>
///     Business logic for op12, the world-entry handler. ZC_REGISTER_AVATAR_RECV carries no Result field, so
///     any anti-tamper failure here closes the socket rather than replying with a clean failure -- see
///     <c>EnterWorldHandler</c>'s own remarks. Owns every send/abort itself (rather than returning a Result for
///     the handler to translate) because success and failure are both threaded through many interleaved,
///     order-dependent session sends -- collapsing that into a single uniform result shape would restructure
///     control flow rather than merely relocate it.
/// </summary>
public interface IEnterWorldService
{
    ValueTask HandleAsync(EnterWorldRequest packet, ZoneClientSession zoneSession,
        CancellationToken cancellationToken);
}

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
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        // A GM-banned character (admin.Bans, ZONE_BLOCK_USER_FOR_PLAYUSER's Fenrir equivalent) must never reach
        // the world, unlike a mute -- checked before the bundle fetch below to not waste it on a rejected entry.
        if (await bans.IsActiveForCharacterAsync(characterId, cancellationToken))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        // tID must still name the account this socket was ticketed for.
        if (!ObfuscatedUidCodec.TryDecodeAccountId(packet.Id, out var decodedAccountId) ||
            decodedAccountId != accountId)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        // A client cannot enter the world already mid-action (move/skill/etc).
        if (packet.Action.Type != 0 || packet.Action.Sort is not (0 or 1))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var bundle = await characters.GetWorldEntryBundleAsync(characterId, cancellationToken);
        if (bundle is null)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var character = bundle.Character;

        // Resolve against the ticket-committed CharacterId; AvatarName only re-confirms it, never picks it.
        if (packet.AvatarName != character.Name)
        {
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
        await Task.WhenAll(isMutedTask.AsTask(), guildTask.AsTask(), tribeRoleTask.AsTask(),
            friendsTask.AsTask(), mentorTask.AsTask());

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
