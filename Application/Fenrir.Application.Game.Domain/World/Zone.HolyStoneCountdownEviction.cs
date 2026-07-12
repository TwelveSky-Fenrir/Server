using System.Threading.Channels;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    private const int HolyStoneCountdownEvictionInboxCapacity = 4;

    private readonly Channel<HolyStoneCountdownEvictionZoneCommand> _holyStoneCountdownEvictionInbox =
        Channel.CreateBounded<HolyStoneCountdownEvictionZoneCommand>(
            new BoundedChannelOptions(HolyStoneCountdownEvictionInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    public bool PostHolyStoneCountdownEviction()
    {
        return _holyStoneCountdownEvictionInbox.Writer.TryWrite(default);
    }

    private void DrainHolyStoneCountdownEvictionCommands()
    {
        while (_holyStoneCountdownEvictionInbox.Reader.TryRead(out _))
        {
            try
            {
                ApplyHolyStoneCountdownEviction();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} HSB countdown eviction sweep failed", MapId);
            }
        }
    }

    private void ApplyHolyStoneCountdownEviction()
    {
        var (kind, owningTribe) = ReviveEligibilityZones.Classify(MapId);
        if (kind != ReviveZoneKind.FactionTerritory)
            return;

        var allyOfOwningTribe = worldState?.GetAllyOf(owningTribe);

        foreach (var state in _players.Values)
        {
            if (state.IsMovingZone)
                continue;

            if (HolyStoneTribeMatch.Matches(state.Tribe, owningTribe, allyOfOwningTribe))
                continue;

            state.Session.Send(new ReturnToHomeZoneResponse());
        }
    }
}

public readonly record struct HolyStoneCountdownEvictionZoneCommand;
