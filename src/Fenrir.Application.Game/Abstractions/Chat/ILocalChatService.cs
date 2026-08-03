using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Core.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.Chat;

public interface ILocalChatService
{
    public ValueTask<bool> TryPostChatAsync(Zone zone, IZoneSession zoneSession, PlayerRuntimeState sender,
        string content, ItemLinkInfo link, CancellationToken cancellationToken);
}
