using Fenrir.Application.Game.Avatars;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Contracts.Wire;
using Fenrir.Data.Characters;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     op12, world-entry handler (wire contract §5.3/§5.4/§5.9) -- the single most sensitive handler in the zone
///     flow. ZC_REGISTER_AVATAR_RECV carries no Result field, so an anti-tamper failure here has no "clean" wire
///     response; the legacy posture ("any violation closes the socket") is reproduced via
///     <see cref="ClientSession.Abort" />. On success this sends both compressed world-entry packets, the
///     non-compressed self-spawn (§5.9 step 3), then hands the player off to <see cref="Zone" /> for tick-driven
///     visibility with everyone else already present.
/// </summary>
public sealed class CzRegisterAvatarSendHandler(
    CharacterRepository characters,
    ZoneRegistry zones,
    ILogger<CzRegisterAvatarSendHandler> logger)
    : IAsyncPacketHandler<CzRegisterAvatarSend>
{
    public async ValueTask HandleAsync(CzRegisterAvatarSend packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var accountId = zoneSession.AccountId!.Value;
        var characterId = zoneSession.CharacterId!.Value;

        // Anti-tamper #1 (§5.3): re-verify atoi(tID+2)==uUserIdx -- tID must still name the account this socket
        // was ticketed for (CZ_TEMP_REGISTER_SEND already checked this once, at op11).
        if (!LegacyUidCodec.TryDecodeAccountId(packet.Id, out var decodedAccountId) || decodedAccountId != accountId)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        // Anti-tamper #2 (§5.3): the legacy handler requires tAction.aType==0 and aSort in {0,1} at world-entry
        // time -- a client cannot enter the world already mid-action (move/skill/etc). Same posture: Quit().
        if (packet.Action.Type != 0 || packet.Action.Sort is not (0 or 1))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var character = await characters.GetForWorldEntryAsync(characterId, cancellationToken);
        if (character is null)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        // Anti-tamper #3 (ADR-0005, Fenrir-specific -- not in the legacy sequence above): resolve against the
        // ticket-committed CharacterId, only re-checking that AvatarName still names that same character --
        // never trust AvatarName to pick the character itself.
        if (packet.AvatarName != character.Name)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        // Routing (ADR-0012): the character's persisted map must be one this shard hosts. Reaching this branch
        // means the LoginServer's shard pick and reality disagree (mis-routed ticket, stale directory) -- same
        // clean-abort posture as an invalid ticket, and crucially BEFORE any world-entry packet is sent.
        if (!zones.TryGet(character.MapId, out var zone))
        {
            logger.LogWarning(
                "Character {CharacterId} is on map {MapId}, which this shard does not host -- aborting registration",
                characterId, character.MapId);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var registerRecv = new ZcRegisterAvatarRecv
        {
            AvatarInfo = AvatarInfoFactory.CreateForCharacter(character),
            BuffInfo = WorldStateTemplates.ZeroedBuffInfo
        };
        zoneSession.SendRaw(ZoneMessageFactory.Encode(in registerRecv));

        var broadcastWorldInfo = new ZcBroadcastWorldInfo
        {
            WorldInfo = WorldStateTemplates.ZeroedWorldInfo,
            TribeInfo = WorldStateTemplates.ZeroedTribeInfo
        };
        zoneSession.SendRaw(ZoneMessageFactory.Encode(in broadcastWorldInfo));

        // Self-spawn (§5.9 step 3): the player's own view of themselves, non-compressed, sent directly -- Zone
        // doesn't know this player exists yet (ZoneCommand.Enter below is only posted, not yet ticked).
        zoneSession.Send(new ZcAvatarActionRecv
        {
            ServerIndex = characterId,
            UniqueNumber = unchecked((uint)characterId),
            Data = new ObjectForAvatar
            {
                VisibleState = 0,
                SpecialState = 0,
                KillOtherTribe = 0,
                GoodFellow = 0,
                GuildName = "",
                GuildRole = 0,
                CallName = "",
                GuildMarkEffect = 0,
                Name = character.Name,
                Tribe = character.Tribe,
                PreviousTribe = 0,
                Gender = character.Gender,
                HeadType = character.HeadType,
                FaceType = character.FaceType,
                Level1 = character.Level,
                Level2 = 0,
                EquipForView = new int[26],
                AnimalNumber = 0,
                Title = 0,
                Halo = 0,
                RebirthNum = 0,
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
            character.FlushSequence)));

        // Unlike a dropped Move (superseded by the client's next one, Zone.Post's documented rationale), a
        // dropped Enter is NOT replayed by anything -- the character would never be added to _players, staying
        // permanently invisible to AOI/broadcast/write-behind persistence while the two packets already sent
        // above lead the client to believe registration succeeded. Bounded inbox (8192) is only expected to
        // fill under a real overload, so treat it like any other fatal condition for this handshake: log it
        // (it should page someone) and close the socket rather than leave a silent phantom player.
        if (!entered)
        {
            logger.LogError("Zone {MapId} inbox full: dropped Enter for character {CharacterId} -- aborting session",
                zone.MapId, characterId);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        // The session carries its zone from here on (movement handlers post to it; handoffs re-point it) --
        // set before the state transition so no InWorld packet can ever observe a null CurrentZone.
        zoneSession.CurrentZone = zone;

        // Not InWorld yet -- CzClientOkForZoneSendHandler (op13) makes that transition once the client acks.
        zoneSession.MarkRegistering();
    }
}
