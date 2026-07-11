using Fenrir.Data.Abstractions.Characters;

namespace Fenrir.Application.Game.Tests.TestSupport;

/// <summary>
///     In-memory stand-in for <see cref="IWarPointRepository" />: records the last atomic-purchase call and
///     returns a configurable outcome, so <c>WarPointShopService</c> can be exercised without a real SQL Server.
/// </summary>
internal sealed class FakeWarPointRepository : IWarPointRepository
{
    public Call? LastCall { get; private set; }

    /// <summary>Outcome returned by the next <see cref="BuyWarPointItemAsync" /> call (default: purchased, balance 0).</summary>
    public WarPointPurchaseResult NextResult { get; set; } = new(true, 0);

    /// <summary>When true, the next call throws (simulates a genuine DB fault, distinct from a soft rejection).</summary>
    public bool ThrowOnNextCall { get; set; }

    public int CallCount { get; private set; }

    public ValueTask<WarPointPurchaseResult> BuyWarPointItemAsync(int characterId, int warPointCost, byte container,
        IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct)
    {
        CallCount++;
        LastCall = new Call(characterId, warPointCost, container, items);

        if (ThrowOnNextCall)
            throw new InvalidOperationException("Simulated usp_Character_BuyWarPointItem failure.");

        return ValueTask.FromResult(NextResult);
    }

    public sealed record Call(
        int CharacterId,
        int WarPointCost,
        byte Container,
        IReadOnlyList<CharacterItemSlotTvp> Items);
}
