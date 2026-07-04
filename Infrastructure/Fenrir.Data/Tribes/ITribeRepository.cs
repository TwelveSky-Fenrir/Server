using System.Collections.ObjectModel;

namespace Fenrir.Data.Tribes;

/// <summary>Abstraction over Fenrir.Data.Tribes.TribeRepository for DI/testability.</summary>
public interface ITribeRepository
{
    public ValueTask<byte> GetRoleForCharacterAsync(int characterId, CancellationToken ct);

    public ValueTask<ReadOnlyCollection<TribeSummaryDto>> GetAllAsync(CancellationToken ct);

    public ValueTask<ReadOnlyCollection<TribeSubMasterDto>> GetSubMastersAsync(byte tribeId, CancellationToken ct);

    public ValueTask SetSubMasterAsync(byte tribeId, byte slotIndex, int characterId, CancellationToken ct);

    public ValueTask ClearSubMasterAsync(byte tribeId, int characterId, CancellationToken ct);

    public ValueTask<ReadOnlyCollection<TribeBankSlotDto>> GetBankAsync(byte tribeId, CancellationToken ct);

    public ValueTask WithdrawBankAsync(byte tribeId, byte slotIndex, int amount, CancellationToken ct);
}
