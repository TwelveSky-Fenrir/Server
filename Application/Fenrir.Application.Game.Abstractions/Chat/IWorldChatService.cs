using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Chat;

/// <summary>
///     Business logic for CZ_WORLD_CHAT_SEND (opcode 152): the anti-spam-bot minimum-level gate, the mute
///     gate, and the unfiltered process-wide broadcast. The wire's <c>TribeRole</c> field for this opcode is
///     actually the sender's tribe number, not a role -- passed through verbatim.
/// </summary>
public interface IWorldChatService
{
    /// <summary>Attempts to broadcast <paramref name="content" /> from <paramref name="sender" /> to every zone.</summary>
    public WorldChatOutcome TrySendChat(PlayerRuntimeState sender, string content);
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
