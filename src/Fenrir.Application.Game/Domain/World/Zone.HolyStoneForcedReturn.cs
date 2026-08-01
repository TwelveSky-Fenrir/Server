using System.Threading.Channels;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    private const int HolyStoneForcedReturnInboxCapacity = 1024;

    private readonly Channel<HolyStoneForcedReturnZoneCommand> _holyStoneForcedReturnInbox =
        Channel.CreateBounded<HolyStoneForcedReturnZoneCommand>(
            new BoundedChannelOptions(HolyStoneForcedReturnInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    public bool PostHolyStoneForcedReturn(int characterId)
    {
        return _holyStoneForcedReturnInbox.Writer.TryWrite(new HolyStoneForcedReturnZoneCommand(characterId));
    }

    private void DrainHolyStoneForcedReturnCommands()
    {
        while (_holyStoneForcedReturnInbox.Reader.TryRead(out var command))
            try
            {
                ApplyHolyStoneForcedReturn(command.CharacterId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} Holy Stone forced return failed for character {CharacterId}",
                    MapId, command.CharacterId);
            }
    }

    private void ApplyHolyStoneForcedReturn(int characterId)
    {
        if (!_players.TryGetValue(characterId, out var state) || state.IsMovingZone)
            return;

        state.Session.Send(new ReturnToHomeZoneResponse());
    }
}

public readonly record struct HolyStoneForcedReturnZoneCommand(int CharacterId);
