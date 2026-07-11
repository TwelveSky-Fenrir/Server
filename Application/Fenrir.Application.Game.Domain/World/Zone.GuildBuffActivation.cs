using System.Threading.Channels;
using Fenrir.Application.Game.Domain.Guilds;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{

    private const int GuildBuffActivationInboxCapacity = 64;

    private const int GuildBuffActivationInboxDrainCapPerTick = GuildBuffActivationInboxCapacity / 2;

    private readonly Channel<GuildBuffActivationZoneCommand> _guildBuffActivationInbox =
        Channel.CreateBounded<GuildBuffActivationZoneCommand>(
            new BoundedChannelOptions(GuildBuffActivationInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

        public bool PostGuildBuffActivationCommand(in GuildBuffActivationZoneCommand command)
    {
        return _guildBuffActivationInbox.Writer.TryWrite(command);
    }

    private void DrainGuildBuffActivationCommands()
    {
        var processed = 0;
        while (processed < GuildBuffActivationInboxDrainCapPerTick &&
               _guildBuffActivationInbox.Reader.TryRead(out var command))
        {
            processed++;
            try
            {
                ApplyGuildBuffActivationCommand(in command);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} guild-buff-activation command for guild {GuildId} failed", MapId,
                    command.GuildId);
            }
        }

        if (processed >= GuildBuffActivationInboxDrainCapPerTick)
            LogDrainCapEngaged(_guildBuffActivationInbox.Reader, "guild-buff-activation",
                GuildBuffActivationInboxDrainCapPerTick);
    }

    private void ApplyGuildBuffActivationCommand(in GuildBuffActivationZoneCommand command)
    {
        foreach (var state in _players.Values)
        {
            if (state.GuildId != command.GuildId)
                continue;

            state.GuildBuffType = command.BuffType;
            state.GuildBuffActive = command.BuffActive;
        }
    }
}
