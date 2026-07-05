using Fenrir.Application.Game.World;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Chat.Services;

/// <summary>
///     Business logic for CZ_WORLD_CHAT_SEND (opcode 152): the anti-spam-bot minimum-level gate, the mute
///     gate, and the unfiltered process-wide broadcast. The wire's <c>TribeRole</c> field for this opcode is
///     actually the sender's tribe number, not a role -- passed through verbatim.
/// </summary>
public interface IWorldChatService
{
    /// <summary>Attempts to broadcast <paramref name="content" /> from <paramref name="sender" /> to every zone.</summary>
    WorldChatOutcome TrySendChat(PlayerRuntimeState sender, string content);
}

public enum WorldChatOutcome
{
    /// <summary>Sender is below the minimum world-chat level -- the caller must Abort the session.</summary>
    LevelTooLow,

    /// <summary>Sender is muted -- silently ignored, no disconnect.</summary>
    Muted,

    /// <summary>Broadcast sent to every zone.</summary>
    Sent
}

public sealed class WorldChatService(ZoneRegistry zones) : IWorldChatService
{
    private const int MinimumLevel = 10;

    public WorldChatOutcome TrySendChat(PlayerRuntimeState sender, string content)
    {
        if (sender.Level < MinimumLevel)
            return WorldChatOutcome.LevelTooLow;

        if (sender.IsMuted)
            return WorldChatOutcome.Muted;

        var response = new WorldChatResponse
        {
            TribeRole = sender.Tribe,
            AvatarName = sender.Name,
            Content = content
        };

        foreach (var target in zones.Zones)
        foreach (var recipient in target.Players)
            recipient.Session.Send(response);

        return WorldChatOutcome.Sent;
    }
}
