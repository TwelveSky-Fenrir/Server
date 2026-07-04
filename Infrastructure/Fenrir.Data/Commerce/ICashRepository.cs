using Fenrir.Data.Characters;

namespace Fenrir.Data.Commerce;

/// <summary>Abstraction over Fenrir.Data.Commerce.CashRepository for DI/testability.</summary>
public interface ICashRepository
{
    public ValueTask<int> GetBalanceAsync(int accountId, CancellationToken ct);

    public ValueTask<int> DebitAndGrantItemAsync(int accountId, int amount, byte reason, int productId,
        int characterId, byte container, IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct);
}
