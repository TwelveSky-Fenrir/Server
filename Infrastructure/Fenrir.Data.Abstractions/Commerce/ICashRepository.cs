using Fenrir.Data.Abstractions.Characters;

namespace Fenrir.Data.Abstractions.Commerce;

public interface ICashRepository
{
    public ValueTask<int> GetBalanceAsync(int accountId, CancellationToken ct);

    public ValueTask<int> DebitAndGrantItemAsync(int accountId, int amount, byte reason, int productId,
        int characterId, byte container, IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct);

        public ValueTask CreditAsync(int accountId, int amount, byte reason, int? productId, CancellationToken ct);
}
