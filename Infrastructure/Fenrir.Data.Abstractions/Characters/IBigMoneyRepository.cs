namespace Fenrir.Data.Abstractions.Characters;

public interface IBigMoneyRepository
{
    public ValueTask AdjustBigMoneyStoreAsync(int characterId, int deltaBigMoney, int deltaBigStoreMoney,
        CancellationToken ct);

    public ValueTask AdjustBigMoneyBankAsync(int characterId, int deltaBigMoney, int accountId,
        int deltaVaultBigMoney, CancellationToken ct);

    public ValueTask AdjustBigMoneyConversionAsync(int characterId, long deltaMoney, int deltaBigMoney,
        CancellationToken ct);
}
