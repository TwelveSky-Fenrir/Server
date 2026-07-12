using System.Threading.Channels;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    private const int GuildBuffExpiredStatSort = 59;

    private const int GuildBuffExpiryInboxCapacity = 64;

    private readonly Channel<GuildBuffExpiryZoneCommand> _guildBuffExpiryInbox =
        Channel.CreateBounded<GuildBuffExpiryZoneCommand>(
            new BoundedChannelOptions(GuildBuffExpiryInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    public bool PostGuildBuffExpiryCommand(in GuildBuffExpiryZoneCommand command)
    {
        return _guildBuffExpiryInbox.Writer.TryWrite(command);
    }

    private void DrainGuildBuffExpiryCommands()
    {
        while (_guildBuffExpiryInbox.Reader.TryRead(out var command))
        {
            try
            {
                ApplyGuildBuffExpiryCommand(in command);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} guild-buff-expiry command for guild {GuildId} failed", MapId,
                    command.GuildId);
            }
        }
    }

    private void ApplyGuildBuffExpiryCommand(in GuildBuffExpiryZoneCommand command)
    {
        foreach (var state in _players.Values)
        {
            if (state.GuildId != command.GuildId)
                continue;

            state.GuildBuffActive = false;
            state.GuildBuffActiveMirror = false;

            if (state.IsMovingZone)
                continue;

            state.Session.Send(new AvatarStateFlagResponse
            {
                ServerIndex = state.CharacterId,
                UniqueNumber = state.UniqueNumber,
                Sort = GuildBuffExpiredStatSort,
                Value01 = command.NewBuffTime,
                Value02 = 0,
                Value03 = 0
            });
        }
    }
}
