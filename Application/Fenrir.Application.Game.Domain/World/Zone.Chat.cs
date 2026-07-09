using System.Buffers;
using System.Threading.Channels;
using Fenrir.Application.Game.Domain.Social.Chat;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    /// <summary>Bounded capacity for <see cref="_chatInbox" /> -- also the basis for <see cref="ChatInboxDrainCapPerTick" />.</summary>
    private const int ChatInboxCapacity = 2048;

    /// <summary>
    ///     Per-tick drain cap for <see cref="_chatInbox" /> -- same "half of this channel's own bounded
    ///     capacity" convention as <see cref="InboxDrainCapPerTick" /> (see that constant's own remarks for the
    ///     full rationale and the Fenrir-side-safeguard-not-legacy-parity caveat).
    /// </summary>
    private const int ChatInboxDrainCapPerTick = ChatInboxCapacity / 2;

    /// <summary>
    ///     Raw local/shout/tribe chat sends that need this zone's tick-owned <see cref="_grid" />/
    ///     <see cref="_players" /> to resolve their audience -- every other chat channel (whisper/party/guild/
    ///     world/notices) fans out directly via <c>ZoneRegistry</c>/<see cref="Players" /> without touching the tick thread.
    /// </summary>
    private readonly Channel<ChatZoneCommand> _chatInbox =
        Channel.CreateBounded<ChatZoneCommand>(
            new BoundedChannelOptions(ChatInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>
    ///     Reusable scratch buffer for <see cref="ApplyChatCommand" />'s Local-chat raw AOI-neighbor scan --
    ///     feeds <see cref="_localChatRecipientScratch" />'s tribe/alliance filter below. Same non-allocating
    ///     shape and reuse justification as <see cref="Zone.PlayerLifecycle" />'s <c>_moveNeighborScratch</c>:
    ///     single tick thread, cleared before use, consumed entirely before <see cref="ApplyChatCommand" />
    ///     returns.
    /// </summary>
    private readonly List<int> _localChatNeighborScratch = [];

    /// <summary>
    ///     Reusable scratch buffer for <see cref="ApplyChatCommand" />'s Local-chat recipient list -- replaces a
    ///     per-message <c>new List&lt;int&gt;()</c> filtered from <see cref="_localChatNeighborScratch" /> by
    ///     tribe/alliance. Same reuse posture as every other scratch buffer in this file family.
    /// </summary>
    private readonly List<int> _localChatRecipientScratch = [];

    public bool PostChatCommand(in ChatZoneCommand command)
    {
        return _chatInbox.Writer.TryWrite(command);
    }

    private void DrainChatCommands()
    {
        var processed = 0;
        while (processed < ChatInboxDrainCapPerTick && _chatInbox.Reader.TryRead(out var command))
        {
            processed++;
            try
            {
                ApplyChatCommand(in command);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} chat command from character {CharacterId} failed", MapId,
                    command.SenderCharacterId);
            }
        }

        if (processed >= ChatInboxDrainCapPerTick)
            LogDrainCapEngaged(_chatInbox.Reader, "chat", ChatInboxDrainCapPerTick);
    }

    /// <summary>
    ///     Resolves Local/Shout/Tribe chat's audience on this zone's own tick thread. Local = AOI-neighbor
    ///     broadcast filtered by the sender's own tribe OR its currently-allied tribe (via
    ///     <see cref="WorldState.WorldStateService.GetAllyOf" />, the same live alliance lookup the legacy
    ///     <c>mGAME.ReturnAllianceTribe</c> performs); Shout = whole-zone, unfiltered; Tribe = whole-zone,
    ///     filtered the same tribe-or-ally way as Local but with no spatial restriction. The sender always
    ///     receives its own echo (its own tribe trivially matches itself).
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:7980-8007 (GENERAL_CHAT_SEND -- Local: readiness +
    ///     tribe/alliance match + coarse cell adjacency + exact radius) ; Server/ts25zone/S04_MyWork02.cpp:11522-11558
    ///     (TRIBE_CHAT_SEND -- readiness + tribe/alliance match, no radius, same-zone-process only, no
    ///     ts25center relay) ; Server/Header/function.h:2964-2975 (<c>ReturnAlliance</c> -- the live,
    ///     runtime-mutable alliance-pair lookup both handlers call through <c>ReturnAllianceTribe</c>).
    /// </remarks>
    private void ApplyChatCommand(in ChatZoneCommand command)
    {
        if (!_players.TryGetValue(command.SenderCharacterId, out var sender))
            return; // sender disconnected/handed off between post and drain -- benign

        switch (command.Kind)
        {
            case ChatBroadcastKind.Local:
            {
                var response = new LocalChatResponse
                    { AvatarName = sender.Name, Content = command.Content, Link = command.Link };
                _localChatNeighborScratch.Clear();
                _grid.Neighbors(_localChatNeighborScratch, sender.CurrentCell, sender.PosX, sender.PosY,
                    sender.PosZ);
                _localChatRecipientScratch.Clear();
                foreach (var id in _localChatNeighborScratch)
                    if (_players.TryGetValue(id, out var recipient) &&
                        IsAlliedOrSameTribe(sender.Tribe, recipient.Tribe))
                        _localChatRecipientScratch.Add(id);
                BroadcastChatFrame(in response, _localChatRecipientScratch);
                break;
            }
            case ChatBroadcastKind.Shout:
            {
                var response = new ShoutResponse
                    { AvatarName = sender.Name, Content = command.Content, Link = command.Link };
                BroadcastChatFrame(in response, _players.Keys);
                break;
            }
            case ChatBroadcastKind.Tribe:
            {
                var response = new TribeChatResponse
                    { AvatarName = sender.Name, Content = command.Content, Link = command.Link };
                var recipientIds = new List<int>();
                foreach (var recipient in _players.Values)
                    if (IsAlliedOrSameTribe(sender.Tribe, recipient.Tribe))
                        recipientIds.Add(recipient.CharacterId);
                BroadcastChatFrame(in response, recipientIds);
                break;
            }
        }
    }

    /// <summary>
    ///     Serialize-once broadcast for Local/Shout/Tribe chat -- same rent-once/write-once/copy-N-times idiom
    ///     as <see cref="BroadcastAvatarAction" />/<see cref="BroadcastMonsterAction" />. A recipient whose
    ///     transport fails mid-loop (already gone/broken) is logged and skipped; it never aborts delivery to
    ///     the rest of <paramref name="recipientIds" /> the way an unguarded <c>Session.Send</c> would.
    /// </summary>
    private void BroadcastChatFrame<TPacket>(in TPacket response, IEnumerable<int> recipientIds)
        where TPacket : struct, IOutgoingPacket
    {
        var total = FrameWriter.FrameSizeOf<TPacket>();
        var rented = ArrayPool<byte>.Shared.Rent(total);

        try
        {
            var span = rented.AsSpan(0, total);
            FrameWriter.WriteFrame(in response, span);

            foreach (var id in recipientIds)
                try
                {
                    if (_players.TryGetValue(id, out var recipient) &&
                        recipient.Session is ClientSession clientSession)
                        clientSession.SendRaw(span);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Zone {MapId} chat broadcast to character {RecipientId} failed", MapId, id);
                }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    /// <summary>
    ///     True if <paramref name="candidateTribe" /> is <paramref name="senderTribe" /> itself, or is
    ///     whatever tribe <see cref="WorldState.WorldStateService.GetAllyOf" /> currently reports as allied
    ///     to it -- the shared Local/Tribe chat audience predicate (see <see cref="ApplyChatCommand" />'s own
    ///     remarks for the legacy citation). <c>worldState</c> being null (unit tests that construct a
    ///     <see cref="Zone" /> without one) degrades to a same-tribe-only match, never throws.
    /// </summary>
    private bool IsAlliedOrSameTribe(byte senderTribe, byte candidateTribe)
    {
        return candidateTribe == senderTribe || worldState?.GetAllyOf(senderTribe) == candidateTribe;
    }
}
