using Fenrir.Data.Abstractions.Characters;

namespace Fenrir.Application.Game.Tests.TestSupport;

internal sealed class FakeWarPointRepository : IWarPointRepository
{
    public Call? LastCall { get; private set; }

        public WarPointPurchaseResult NextResult { get; set; } = new(true, 0);

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
