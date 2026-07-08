using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Domain.Social.Chat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Chat;

/// <summary>
///     See <see cref="ILocalChatService" />'s own remarks for the mute/GM-command/ordinary-chat split. Of the
///     six GM commands <see cref="LocalChatGmCommandParser" /> recognizes, only <c>where</c> is fully
///     implemented (pure in-memory self-location echo, no missing subsystem). The tier gate itself (Basic for
///     <c>where</c>/<c>kill200</c>/<c>?clear</c>, Elevated for <c>ygdrop</c>/<c>lab</c>/<c>boss</c>, matching the
///     contract's own per-command thresholds) is enforced for every one of the six, including a real
///     "no permission" reply for an under-tiered sender on the Elevated-gated three -- but a sufficiently-tiered
///     sender's <c>ygdrop</c>/<c>lab</c>/<c>boss</c>/<c>kill200</c>/<c>?clear</c> is a deliberate, logged no-op:
///     each needs a subsystem Fenrir does not model yet (a global yang/gok PvP drop-rate flag, per-tribe
///     Labyrinth/"ZONE175" battle state relayed to the center process, spawn-monster-by-id, a per-tribe
///     zone-200 battle-result counter, or a full-inventory-wipe stored procedure respectively) -- see
///     <see cref="LocalChatGmCommandParser" />'s own remarks for the full citation list. The command is still
///     consumed (never falls through to a broadcast of the raw GM command text as ordinary chat) so a GM typing
///     e.g. "boss 5" is never leaked to nearby players as chat.
/// </summary>
public sealed class LocalChatService(ILogger<LocalChatService> logger) : ILocalChatService
{
    // Socket is a reference-type array; a bare `default` would leave it null and crash the wire writer (same
    // fix as WhisperHandler's own EmptyLink).
    private static readonly ItemLinkInfo EmptyLink = new() { Index = 0, Activity = 0, Value = 0, Socket = new int[3] };

    // Every synthetic self-only reply this service sends (the "where" echo and every "no permission" denial)
    // is addressed from this fixed name, matching the contract's "a fixed system-looking sender name" for the
    // ygdrop/lab confirmations and extended here to every other synthetic reply for consistency.
    private const string SystemSenderName = "SYSTEM";

    public bool TryPostChat(Zone zone, ZoneClientSession zoneSession, PlayerRuntimeState sender, string content,
        ItemLinkInfo link)
    {
        if (sender.IsMuted)
            return false;

        if (zoneSession.IsGm && LocalChatGmCommandParser.TryParse(content, out var command))
        {
            HandleGmCommand(zoneSession, sender, command);
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

    private void HandleGmCommand(ZoneClientSession zoneSession, PlayerRuntimeState sender, LocalChatGmCommand command)
    {
        if (!zoneSession.MeetsGmTier(command.RequiredTier))
        {
            SendSystemChat(sender, "You do not have permission to use this command.");
            return;
        }

        switch (command.Kind)
        {
            case LocalChatGmCommandKind.Where:
                SendSystemChat(sender,
                    $"zone {sender.MapId} ({(int)sender.PosX}, {(int)sender.PosY}, {(int)sender.PosZ})");
                return;

            default:
                // ygdrop/lab/boss/kill200/?clear: tier already confirmed sufficient above, but the underlying
                // subsystem doesn't exist in Fenrir yet -- see this type's own remarks for which one each
                // command needs. Intentionally silent to the caller (no fabricated success reply) and never
                // falls through to ordinary chat delivery; only logged so the gap stays visible in telemetry.
                logger.LogWarning(
                    "Character {CharacterId} ({Name}) invoked LocalChat GM command {Kind} (argument {Argument}) -- " +
                    "recognized and tier-gated, but its subsystem is not implemented in Fenrir yet; no effect",
                    sender.CharacterId, sender.Name, command.Kind, command.Argument ?? "<none>");
                return;
        }
    }

    private static void SendSystemChat(PlayerRuntimeState recipient, string content)
    {
        recipient.Session.Send(new LocalChatResponse
        {
            AvatarName = SystemSenderName,
            Content = content,
            Link = EmptyLink
        });
    }
}
