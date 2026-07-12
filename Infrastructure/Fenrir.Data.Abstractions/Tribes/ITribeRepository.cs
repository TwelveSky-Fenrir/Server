using System.Collections.ObjectModel;

namespace Fenrir.Data.Abstractions.Tribes;

public interface ITribeRepository
{
    public ValueTask<byte> GetRoleForCharacterAsync(int characterId, CancellationToken ct);

    public ValueTask<ReadOnlyCollection<TribeSummaryDto>> GetAllAsync(CancellationToken ct);

    public ValueTask SetMasterAsync(byte tribeId, int? newMasterCharacterId, CancellationToken ct);

    public ValueTask<ReadOnlyCollection<TribeSubMasterDto>> GetSubMastersAsync(byte tribeId, CancellationToken ct);

    public ValueTask SetSubMasterAsync(byte tribeId, byte slotIndex, int characterId, CancellationToken ct);

    public ValueTask ClearSubMasterAsync(byte tribeId, int characterId, CancellationToken ct);

    public ValueTask<ReadOnlyCollection<TribeBankSlotDto>> GetBankAsync(byte tribeId, CancellationToken ct);

    /// <summary>
    ///     All-4-tribes bank-total read in one round trip via <c>game.vw_TribeBankTotals</c>: a tribe never
    ///     deposited into still comes back with <c>TotalAmount</c> = 0 rather than being omitted. game.TribeBank
    ///     is memory-optimized and the hottest write path in this domain (deposit/withdraw/tax-sweep, callable
    ///     from whichever shard hosts the acting player/zone), so this stays a live SQL read, never a per-shard
    ///     cache.
    /// </summary>
    public ValueTask<ReadOnlyCollection<TribeBankTotalDto>> GetBankTotalsAsync(CancellationToken ct);

    public ValueTask<long> WithdrawBankAsync(byte tribeId, byte slotIndex, int characterId, CancellationToken ct);

    public ValueTask<long> DepositBankAsync(byte tribeId, byte slotIndex, int characterId, CancellationToken ct);
}
