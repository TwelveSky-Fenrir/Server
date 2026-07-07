using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting;

/// <summary>
///     Bounded-staleness approximation of legacy's per-message live mute recheck
///     (<c>ZPP_ZONE_CHECK_MUTE_FOR_PLAYUSER_SEND</c>, opcode 45): periodically re-batches <c>admin.Mutes</c>
///     for every character this shard currently holds a live Zone session for and applies any change to
///     that character's <see cref="PlayerRuntimeState.IsMuted" /> -- same "singleton BackgroundService with
///     its own timer" shape as <see cref="AccountSessionKickPollHost" />.
/// </summary>
/// <remarks>
///     <para>
///         Legacy re-checks live on every single chat attempt across five distinct chat-send handlers
///         (<c>GENERAL_CHAT_SEND</c>/<c>GENERAL_SHOUT_SEND</c>/<c>GUILD_CHAT_SEND</c>/<c>TRIBE_CHAT_SEND</c>/
///         <c>WORLD_CHAT_SEND</c>, Server/ts25zone/S04_MyWork02.cpp:7762-7784,8075-8096,10736-10759,11522-11541,
///         15467-15484) because that recheck is essentially free there -- an in-memory <c>ts25playuser</c>
///         session-registry lookup, not a database round trip. Fenrir's <see cref="IMuteRepository" /> is a
///         genuine SQL call, and every one of the five Fenrir chat-send handlers this finding covers
///         (<c>LocalChatHandler</c>/<c>ShoutHandler</c>/<c>GuildChatHandler</c>/<c>TribeChatHandler</c>/
///         <c>WorldChatHandler</c>) is an <c>IInlinePacketHandler&lt;T&gt;</c> -- that interface's own
///         contract is "never SQL or a contended lock" on the synchronous session-loop path
///         (Network/Fenrir.Network.Abstractions/IInlinePacketHandler.cs). A literal per-message live requery
///         would therefore either violate that contract or require converting all five handlers to
///         <c>IAsyncPacketHandler&lt;T&gt;</c> and paying a SQL round trip on every chat message sent
///         cluster-wide -- neither is legacy-accurate in effect (legacy's own recheck cost is negligible;
///         Fenrir's would not be). This host instead implements the behavior contract's own explicitly-listed
///         alternative: "add a bounded-staleness periodic refresh". A GM mute applied or lifted mid-session
///         now takes effect within one poll interval (<see cref="GameServerOptions.MutePollIntervalSeconds" />,
///         default 15s) instead of only at the character's next world entry -- narrowing, not fully closing,
///         the parity gap the uppercom-playuser-extra-relay-opcodes finding identified.
///     </para>
///     <para>
///         Applies each changed character's new mute status via <see cref="ZoneCommand.SetMuted" />, posted
///         onto that character's own hosting <see cref="Zone" />, rather than writing
///         <see cref="PlayerRuntimeState.IsMuted" /> directly from this host's own timer thread -- preserving
///         the single-writer-per-zone invariant every other <see cref="PlayerRuntimeState" /> mutation
///         already relies on (see <see cref="ZoneCommandKind.MarkZoneTransferPending" />'s own remarks for
///         the established precedent this follows).
///     </para>
/// </remarks>
public sealed class MuteRefreshPollHost(
    ZoneRegistry zones,
    IMuteRepository mutes,
    IOptions<GameServerOptions> options,
    ILogger<MuteRefreshPollHost> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.MutePollIntervalSeconds));

        do
        {
            try
            {
                await PollOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A missed poll just delays picking up a mute/unmute by one cycle -- never worth crashing
                // the GameServer over.
                logger.LogError(ex, "Mute refresh poll failed for shard {ShardId}", options.Value.ShardId);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    /// <summary>Public, not private: exercised directly by tests instead of waiting on the real timer.</summary>
    public async ValueTask PollOnceAsync(CancellationToken ct)
    {
        // Snapshot every character this shard currently holds a live session for, together with the zone
        // that hosts it -- Zone's own _players is a ConcurrentDictionary, safe to enumerate from any thread
        // (the same guarantee ZoneRegistry.TryGetPlayerByName already relies on for its own cross-thread scan).
        var tracked = new Dictionary<int, (Zone Zone, bool WasMuted)>();
        foreach (var zone in zones.Zones)
        foreach (var player in zone.Players)
            tracked[player.CharacterId] = (zone, player.IsMuted);

        if (tracked.Count == 0)
            return;

        var mutedIds = await mutes.GetActiveCharacterIdsAsync(tracked.Keys, ct).ConfigureAwait(false);
        var mutedSet = mutedIds.IsEmpty ? [] : mutedIds.ToHashSet();

        foreach (var (characterId, (zone, wasMuted)) in tracked)
        {
            var isMuted = mutedSet.Contains(characterId);
            if (isMuted == wasMuted)
                continue;

            // A dropped Post here (full inbox, or the character already left this zone) just means this
            // change is picked up on the next poll instead -- same "never blocks, never worth retrying
            // synchronously" posture as every other Zone.Post caller.
            zone.Post(ZoneCommand.SetMuted(characterId, isMuted));
        }
    }
}
