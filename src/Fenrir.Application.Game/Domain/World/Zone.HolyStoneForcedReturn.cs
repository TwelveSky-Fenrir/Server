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
        // Server/ts25zone/S07_MyGame01.cpp:3974-3985 : le predicat de selection est en amont
        // (!ReturnWinZone038(aTribe), :3982) ; ici ni un user non pret ni un IsMovingZone n'est expulse, et le
        // seul effet est l'opcode nu ZCP_RETURN_TO_AUTO_ZONE - le Quit() y est commente, aucune destination calculee.
        if (!_players.TryGetValue(characterId, out var state) || state.IsMovingZone)
            return;

        state.Session.Send(new ReturnToHomeZoneResponse());
    }
}

public readonly record struct HolyStoneForcedReturnZoneCommand(int CharacterId);
