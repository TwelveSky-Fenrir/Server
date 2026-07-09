using System.Threading.Channels;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World;

/// <summary>
///     Guild-buff-reserve-exhaustion immediate strip-effect push. <c>Guilds.GuildBuffDecay</c>/
///     <c>Hosting.Guilds.GuildBuffDecayHost</c> already own the persisted-reserve decay/flooring half; this
///     partial is the other half -- the instant a guild's reserve first reaches exhaustion, every currently
///     hosted member of that guild (cluster-wide, via <c>Hosting.GuildBuffExpiryRelayHost</c>'s own fan-out)
///     has its live combat-affecting buff gate flipped off immediately, not merely reflected the next time
///     GUILD_INFO happens to be re-read.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25center/S07_MyGame01.cpp:291-331 (edge-triggered <c>gBuffTime &lt; 1</c> branch,
///     the hub-side trigger this partial's own callers -- <c>GuildBuffDecayHost</c>/
///     <c>GuildBuffExpiryRelayHost</c> -- reproduce) ; Server/ts25center/S05_MyTransfer.cpp:65-73,
///     Server/ts25center/H05_MyTransfer.h:28-29 (the dedicated wire opcode this push rides, carrying guild
///     name/buff time/a fixed-placeholder buff type never read on the receiving end) ;
///     Server/ts25zone/UpperCom/S06_MyUpperCom02.cpp:359-378 (the zone-side handler
///     <see cref="ApplyGuildBuffExpiryCommand" /> reimplements: iterate every connected/ready session, match
///     the expiring guild, flip both in-memory fields unconditionally -- including a session mid-zone-transfer
///     -- then send the client notification ONLY when that session is not mid-transfer) ;
///     Server/Header/Protocol/STRUCT.h:1573 (S059IS_IN_OR_JOIN_GUILD -- the reused/shared avatar-state-changed
///     subtype this push rides; not a dedicated "buff expired" message).
/// </remarks>
public sealed partial class Zone
{
    /// <summary>S059IS_IN_OR_JOIN_GUILD (Server/Header/Protocol/STRUCT.h:1573) -- see this file's own remarks.</summary>
    private const int GuildBuffExpiredStatSort = 59;

    /// <summary>
    ///     Bounded capacity for <see cref="_guildBuffExpiryInbox" /> -- also the basis for
    ///     <see cref="GuildBuffExpiryInboxDrainCapPerTick" />. Small: a guild's reserve reaching exhaustion is a
    ///     rare, edge-triggered event (at most one push per guild per exhaustion, gated by
    ///     <c>GuildBuffDecayHost</c>'s own 30 s poll), never a per-tick/per-combat volume.
    /// </summary>
    private const int GuildBuffExpiryInboxCapacity = 64;

    /// <summary>
    ///     Per-tick drain cap for <see cref="_guildBuffExpiryInbox" /> -- see <see cref="InboxDrainCapPerTick" />'s
    ///     own remarks.
    /// </summary>
    private const int GuildBuffExpiryInboxDrainCapPerTick = GuildBuffExpiryInboxCapacity / 2;

    private readonly Channel<GuildBuffExpiryZoneCommand> _guildBuffExpiryInbox =
        Channel.CreateBounded<GuildBuffExpiryZoneCommand>(
            new BoundedChannelOptions(GuildBuffExpiryInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>
    ///     Posted once per exhausted guild per pass, once per hosted zone -- by <c>Hosting.Guilds.GuildBuffDecayHost</c>
    ///     for this shard's own same-shard delivery, and by <c>Hosting.GuildBuffExpiryRelayHost</c> for every
    ///     OTHER live shard's own cross-shard delivery. A no-op on a zone with no matching guild member.
    /// </summary>
    public bool PostGuildBuffExpiryCommand(in GuildBuffExpiryZoneCommand command)
    {
        return _guildBuffExpiryInbox.Writer.TryWrite(command);
    }

    private void DrainGuildBuffExpiryCommands()
    {
        var processed = 0;
        while (processed < GuildBuffExpiryInboxDrainCapPerTick &&
               _guildBuffExpiryInbox.Reader.TryRead(out var command))
        {
            processed++;
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

        if (processed >= GuildBuffExpiryInboxDrainCapPerTick)
            LogDrainCapEngaged(_guildBuffExpiryInbox.Reader, "guild-buff-expiry",
                GuildBuffExpiryInboxDrainCapPerTick);
    }

    /// <summary>
    ///     Server/ts25zone/UpperCom/S06_MyUpperCom02.cpp:359-378's own per-session loop, reimplemented per zone:
    ///     every currently hosted member of the expiring guild has both in-memory buff-on/off fields
    ///     (<see cref="PlayerRuntimeState.GuildBuffActive" />/<see cref="PlayerRuntimeState.GuildBuffActiveMirror" />)
    ///     flipped unconditionally, including a member currently mid-zone-transfer
    ///     (<see cref="PlayerRuntimeState.IsMovingZone" />) -- but only a non-transferring member additionally
    ///     receives the client-facing <see cref="AvatarStateFlagResponse" /> notification, sent immediately
    ///     after the flip, matching legacy's own send-gate exactly. Self-targeted only (this is a private
    ///     buff-state notice, not an externally visible cosmetic change), unlike this file family's own
    ///     AOI-broadcast helpers (<c>BroadcastAvatarStateFlag</c>).
    /// </summary>
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
