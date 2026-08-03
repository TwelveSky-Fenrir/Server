using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Tribes;

public readonly record struct TribeBankResult(bool Success, int Sort, int[]? TribeBankInfo, int Money,
    bool Disconnect = false)
{
    public static readonly TribeBankResult Aborted = new(false, 0, null, 0);

    public static readonly TribeBankResult Disconnected = new(false, 0, null, 0, true);
}

public interface ITribeBankService
{
    public ValueTask<TribeBankResult> ViewAsync(IZoneSession zoneSession, PlayerRuntimeState state,
        CancellationToken ct);
}
