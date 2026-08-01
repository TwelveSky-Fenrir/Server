namespace Fenrir.Data.Abstractions.Characters;

public interface IWarPointRepository
{
    public ValueTask<WarPointPurchaseResult> BuyWarPointItemAsync(int characterId, int warPointCost, byte container,
        IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct);
}

public readonly record struct WarPointPurchaseResult(bool Purchased, int NewWarPointBalance);
