using System.Threading.Channels;
using Fenrir.Application.Game.Domain.Guilds;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    private const int GuildBuffActivationInboxCapacity = 64;

    private readonly Channel<GuildBuffActivationZoneCommand> _guildBuffActivationInbox =
        Channel.CreateBounded<GuildBuffActivationZoneCommand>(
            new BoundedChannelOptions(GuildBuffActivationInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    public bool PostGuildBuffActivationCommand(in GuildBuffActivationZoneCommand command)
    {
        return _guildBuffActivationInbox.Writer.TryWrite(command);
    }

    private void DrainGuildBuffActivationCommands(int maximum)
    {
        for (var processed = 0;
             processed < maximum && _guildBuffActivationInbox.Reader.TryRead(out var command);
             processed++)
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

    private void ApplyGuildBuffActivationCommand(in GuildBuffActivationZoneCommand command)
    {
        foreach (var state in _players.Values)
        {
            if (state.GuildId != command.GuildId)
                continue;

            state.GuildBuffType = command.BuffType;
            state.GuildBuffActive = command.BuffActive;

            if (state.IsMovingZone)
                continue;

            var changedSlots = state.BuffChangeScratch;
            Array.Clear(changedSlots);
            RecomputeStatsAndBroadcastBuffs(state, changedSlots);
        }
    }
}
