using System.Buffers;
using System.Threading.Channels;
using Fenrir.Application.Game.Domain.Social.Chat;
using Fenrir.Core.Wire;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    private const int ChatInboxCapacity = 2048;

    private readonly Channel<ChatZoneCommand> _chatInbox =
        Channel.CreateBounded<ChatZoneCommand>(
            new BoundedChannelOptions(ChatInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    private readonly List<int> _localChatNeighborScratch = [];

    private readonly List<int> _localChatRecipientScratch = [];

    public bool PostChatCommand(in ChatZoneCommand command)
    {
        return _chatInbox.Writer.TryWrite(command);
    }

    private void DrainChatCommands(int maximum)
    {
        for (var processed = 0; processed < maximum && _chatInbox.Reader.TryRead(out var command); processed++)
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

    private void ApplyChatCommand(in ChatZoneCommand command)
    {
        if (!_players.TryGetValue(command.SenderCharacterId, out var sender))
            return;

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
                BroadcastChatFrame(in response, _localChatRecipientScratch, true);
                break;
            }
            case ChatBroadcastKind.Shout:
            {
                var response = new ShoutResponse
                    { AvatarName = sender.Name, Content = command.Content, Link = command.Link };
                BroadcastChatFrame(in response, _players.Keys, false);
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
                BroadcastChatFrame(in response, recipientIds, false);
                break;
            }
        }
    }

    private void BroadcastChatFrame<TPacket>(in TPacket response, IEnumerable<int> recipientIds,
        bool requiresPositionalEligibility)
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
                    IPacketSession? clientSession;
                    var eligible = requiresPositionalEligibility
                        ? TryGetBroadcastRecipient(id, out _, out clientSession)
                        : TryGetZoneWideBroadcastRecipient(id, out clientSession);

                    if (eligible)
                        clientSession!.SendRaw(span);
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

    private bool IsAlliedOrSameTribe(byte senderTribe, byte candidateTribe)
    {
        return candidateTribe == senderTribe || worldState?.GetAllyOf(senderTribe) == candidateTribe;
    }
}
