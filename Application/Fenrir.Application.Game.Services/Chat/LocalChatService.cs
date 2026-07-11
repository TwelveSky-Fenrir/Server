using System.Globalization;
using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Domain.Gm;
using Fenrir.Application.Game.Domain.Social.Chat;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Chat;

/// <summary>
///     See <see cref="ILocalChatService" />'s own remarks for the mute/GM-command/ordinary-chat split. Of the
///     six GM commands <see cref="LocalChatGmCommandParser" /> recognizes:
///     <list type="bullet">
///         <item>
///             <c>where</c> (Basic tier) -- fully implemented: a pure in-memory self-location echo.
///         </item>
///         <item>
///             <c>ygdrop on|off|status</c> (Elevated tier) -- fully implemented: toggles the shard-wide
///             <see cref="YangGokPvpDropEventState" /> (enable flag + 35% rate) and reports state via a
///             self-only system-chat line. The drop roll that READS this flag is a separate, not-yet-modeled
///             subsystem -- see that type's own consumer-gap remark.
///         </item>
///         <item>
///             <c>boss &lt;id&gt;</c> (Elevated tier) -- fully implemented: spawns the requested monster id
///             (id &gt;= 1) at the sender's own position via the same GM-summon slot pool the "moncall"
///             command (<see cref="Fenrir.Application.Game.Services.Gm.GmSummonMonsterService" />) uses, then
///             raises a shard-wide notice. A deliberately DISTINCT code path from moncall (no audit-log row,
///             an id lower-bound gate, and a server-wide notice) -- see that service's own remarks.
///         </item>
///         <item>
///             <c>kill200</c> (Basic tier) -- partially implemented: only the cited self-echo of the command
///             text back to the sender is reproduced. The sender-tribe zone-200-type battle-result counter
///             reset (legacy <c>mGAME.mZone200TypeBattleResult[tribe]</c>) has no Fenrir equivalent yet, so
///             that side effect is a documented gap, not silently faked.
///         </item>
///         <item>
///             <c>lab on|off|status</c> (Elevated tier) -- DEFERRED: a cross-process open/close relay to the
///             ts25center process for which Fenrir (shard-local, no center-equivalent) has no faithful
///             equivalent; see the <see cref="LocalChatGmCommandKind.Lab" /> case for the full rationale.
///         </item>
///         <item>
///             <c>?clear</c> (Basic tier) -- DEFERRED: a destructive full-inventory wipe that must be durably
///             persisted in a single stored procedure and cannot run on this inline, synchronous, no-I/O chat
///             path; see the <see cref="LocalChatGmCommandKind.ClearInventory" /> case.
///         </item>
///     </list>
///     Every one of the six is still tier-gated (a real "no permission" reply for an under-tiered sender on
///     the Elevated-gated three) and every one is consumed (never falls through to a broadcast of the raw GM
///     command text as ordinary chat), so a GM typing e.g. "boss 5" or "ygdrop on" is never leaked to nearby
///     players as chat. Only <c>kill200</c> deliberately re-emits its own command word, as a self-only echo,
///     matching legacy's own cited output.
/// </summary>
public sealed class LocalChatService(
    YangGokPvpDropEventState yangGokDropEvent,
    IWorldNoticeService worldNotice,
    ILogger<LocalChatService> logger) : ILocalChatService
{
    // Every synthetic system reply this service sends (the "where"/"ygdrop"/"boss" echoes and every
    // "no permission" denial) is addressed from this fixed name, matching the contract's "a fixed
    // system-looking sender name" for the ygdrop/lab confirmations and extended here for consistency.
    private const string SystemSenderName = "SYSTEM";

    // Socket is a reference-type array; a bare `default` would leave it null and crash the wire writer (same
    // fix as WhisperHandler's own EmptyLink).
    private static readonly ItemLinkInfo EmptyLink = new() { Index = 0, Activity = 0, Value = 0, Socket = new int[3] };

    public bool TryPostChat(Zone zone, ZoneClientSession zoneSession, PlayerRuntimeState sender, string content,
        ItemLinkInfo link)
    {
        if (sender.IsMuted)
            return false;

        if (zoneSession.IsGm && LocalChatGmCommandParser.TryParse(content, out var command))
        {
            HandleGmCommand(zone, zoneSession, sender, command);
            return true;
        }

        zone.PostChatCommand(new ChatZoneCommand
        {
            SenderCharacterId = sender.CharacterId,
            Kind = ChatBroadcastKind.Local,
            Content = content,
            Link = link
        });

        return true;
    }

    private void HandleGmCommand(Zone zone, ZoneClientSession zoneSession, PlayerRuntimeState sender,
        LocalChatGmCommand command)
    {
        if (!zoneSession.MeetsGmTier(command.RequiredTier))
        {
            // Legacy's per-command tier gate: a GM below the required tier gets a private "No permission."
            // system line, no disconnect, no state change (S04_MyWork02.cpp:7810-7815/7853-7858/7908-7913).
            SendSystemChat(sender, "You do not have permission to use this command.");
            return;
        }

        switch (command.Kind)
        {
            case LocalChatGmCommandKind.Where:
                // Read-only self-location echo, sender only (S04_MyWork02.cpp:7800-7806).
                SendSystemChat(sender,
                    $"zone {sender.MapId} ({(int)sender.PosX}, {(int)sender.PosY}, {(int)sender.PosZ})");
                return;

            case LocalChatGmCommandKind.YgDrop:
                HandleYgDrop(sender, command.Argument);
                return;

            case LocalChatGmCommandKind.Boss:
                HandleBoss(zone, sender, command.Argument);
                return;

            case LocalChatGmCommandKind.Kill200:
                HandleKill200(sender);
                return;

            case LocalChatGmCommandKind.Lab:
                // DEFERRED: legacy relays a per-tribe Labyrinth ("ZONE175") open/close state change to the
                // ts25center process (state value 64 open / 110 close, sub-index 5, per tribe -- see the
                // source behavior contract, S04_MyWork02.cpp:7864-7878). Fenrir is shard-local with no
                // ts25center-equivalent relay (the same limitation WhisperService/WorldNoticeService document),
                // and WorldInfo.Zone175TypeState is a READ-ONLY projection FROM center siege state
                // (ZoneCenterSiegeProjection), so this cross-process toggle cannot be faithfully reproduced
                // from a zone shard yet. Still tier-gated and consumed (never leaked as chat); logged so the
                // gap stays visible in telemetry.
                logger.LogWarning(
                    "Character {CharacterId} ({Name}) invoked GM 'lab' (argument {Argument}) -- the Labyrinth/ZONE175 center relay is not modeled in Fenrir (shard-local); no effect",
                    sender.CharacterId, sender.Name, command.Argument ?? "<none>");
                return;

            case LocalChatGmCommandKind.ClearInventory:
                // DEFERRED: a full-inventory wipe is a destructive economy mutation that must be durably
                // persisted in a single stored procedure (fenrir-database-engineer territory) and cannot run
                // on this inline, synchronous, no-I/O chat path. The legacy in-memory-only shape
                // (S04_MyWork02.cpp:7942-7958) also leaves a flagged persistence gap (the source behavior
                // contract's own "confirm the intended persistence/authority model" note), so it is
                // deliberately NOT reproduced as an in-memory-only wipe that would desync on the next login.
                logger.LogWarning(
                    "Character {CharacterId} ({Name}) invoked GM '?clear' -- inventory-wipe needs a durable stored-proc path and a persistence-authority decision; no effect (see workstream report)",
                    sender.CharacterId, sender.Name);
                return;

            default:
                // A newly-added LocalChatGmCommandKind that reaches here without its own case must never leak
                // as chat -- consume it and surface the omission rather than silently broadcasting it.
                logger.LogError(
                    "Character {CharacterId}: unhandled LocalChat GM command kind {Kind} -- consumed, no effect",
                    sender.CharacterId, command.Kind);
                return;
        }
    }

    /// <summary>
    ///     <c>ygdrop on|off|status</c> -- toggles/reports the shard-wide YangGok PvP-drop event. Exact-subform
    ///     match only (the parser already trimmed the remainder); anything else is a usage line. Every branch
    ///     replies to the sender only (S04_MyWork02.cpp:7808-7849).
    /// </summary>
    private void HandleYgDrop(PlayerRuntimeState sender, string? argument)
    {
        switch (argument)
        {
            case "on":
                yangGokDropEvent.Enable();
                SendSystemChat(sender,
                    $"YangGok PvP drop event: ON {YangGokPvpDropEventState.EnabledDropRatePercent}%");
                return;

            case "off":
                yangGokDropEvent.Disable();
                SendSystemChat(sender, "YangGok PvP drop event: OFF");
                return;

            case "status":
                SendSystemChat(sender, yangGokDropEvent.Enabled
                    ? $"YangGok PvP drop event: ON {yangGokDropEvent.DropRatePercent}%"
                    : "YangGok PvP drop event: OFF");
                return;

            default:
                SendSystemChat(sender, "Usage: ygdrop on|off|status");
                return;
        }
    }

    /// <summary>
    ///     <c>boss &lt;id&gt;</c> -- spawns the requested monster id at the sender's own position, then raises
    ///     a shard-wide notice. A malformed/absent/sub-1 id is rejected with a private usage line and no spawn
    ///     (S04_MyWork02.cpp:7918-7923). A valid id (&gt;= 1) is trusted verbatim into the spawn primitive with
    ///     no upper bound or allow-list, matching legacy (S04_MyWork02.cpp:7925) -- the spawn primitive's own
    ///     "unknown template id is a silent no-op" is the only safety net, more than legacy applied.
    /// </summary>
    private void HandleBoss(Zone zone, PlayerRuntimeState sender, string? argument)
    {
        if (!int.TryParse(argument, NumberStyles.Integer, CultureInfo.InvariantCulture, out var monsterId) ||
            monsterId < 1)
        {
            SendSystemChat(sender, "Usage: boss <monster id>");
            return;
        }

        // Fire-and-forget from this inline chat path: the spawn is applied on the zone's own next tick via
        // the same GmSummonMonsterTemplateId mirror moncall uses (spawns at the invoking GM's own current
        // position). Distinct code path from GmSummonMonsterService: no audit-log row, an id lower-bound gate,
        // and a shard-wide notice -- the two must remain independently invokable (that service's own remarks).
        if (!zone.PostTribeProgressCommand(
                new TribeProgressZoneCommand(sender.CharacterId, GmSummonMonsterTemplateId: monsterId)))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped GM 'boss' spawn for character {CharacterId} (monster {MonsterId})",
                zone.MapId, sender.CharacterId, monsterId);

        // Unlike moncall, boss announces the summon shard-wide (S04_MyWork02.cpp:7920), unconditionally after
        // posting -- matching legacy's own unconditional post-summon broadcast (the summon call is unchecked).
        worldNotice.Broadcast($"A boss (id {monsterId}) has been summoned.");
    }

    /// <summary>
    ///     <c>kill200</c> -- reproduces only the cited self-echo of the command text back to the sender
    ///     (S04_MyWork02.cpp:7936). GAP: the sender-tribe zone-200-type battle-result counter reset
    ///     (S04_MyWork02.cpp:7935) has no Fenrir equivalent -- that war-result subsystem is unmodeled, so the
    ///     reset-to-0 side effect is a documented hook (see this type's own remarks), never faked.
    /// </summary>
    private void HandleKill200(PlayerRuntimeState sender)
    {
        SendChatAs(sender, sender.Name, "kill200");
        logger.LogWarning(
            "Character {CharacterId} ({Name}) invoked GM 'kill200' -- the zone-200 battle-result reset is an unmodeled subsystem in Fenrir; only the self-echo was applied",
            sender.CharacterId, sender.Name);
    }

    private static void SendSystemChat(PlayerRuntimeState recipient, string content)
    {
        SendChatAs(recipient, SystemSenderName, content);
    }

    private static void SendChatAs(PlayerRuntimeState recipient, string avatarName, string content)
    {
        recipient.Session.Send(new LocalChatResponse
        {
            AvatarName = avatarName,
            Content = content,
            Link = EmptyLink
        });
    }
}
