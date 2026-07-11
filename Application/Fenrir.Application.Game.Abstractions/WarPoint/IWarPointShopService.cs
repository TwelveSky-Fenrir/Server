using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.WarPoint;

public enum WarPointBuyStatus
{
    NotHandled,

    Aborted,

    SoftRejected,

    Succeeded
}

public readonly record struct WarPointBuyServiceResult(WarPointBuyStatus Status)
{
    public static readonly WarPointBuyServiceResult NotHandled = new(WarPointBuyStatus.NotHandled);
    public static readonly WarPointBuyServiceResult Aborted = new(WarPointBuyStatus.Aborted);
    public static readonly WarPointBuyServiceResult SoftRejected = new(WarPointBuyStatus.SoftRejected);
    public static readonly WarPointBuyServiceResult Succeeded = new(WarPointBuyStatus.Succeeded);
}

public interface IWarPointShopService
{
    public ValueTask<WarPointBuyServiceResult> TryBuyAsync(Zone zone, PlayerRuntimeState state, int accountId,
        int characterId, int npcId, int itemId, int requestedQuantity, byte destinationPage, byte destinationSlot,
        CancellationToken ct);
}
