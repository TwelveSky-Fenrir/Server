using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Abstractions.Sessions;

namespace Fenrir.Application.Game.Abstractions.Tribes;

public readonly record struct TribeBankResult(bool Success, int Sort, int[]? TribeBankInfo, int Money)
{
    public static readonly TribeBankResult Aborted = new(false, 0, null, 0);
}

public interface ITribeBankService
{
    public ValueTask<TribeBankResult> ViewAsync(IZoneSession zoneSession, PlayerRuntimeState state,
        CancellationToken ct);
}
