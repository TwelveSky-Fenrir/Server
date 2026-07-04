using System.Collections.ObjectModel;
using Fenrir.Data.Tribes;

namespace Fenrir.Application.Game.Tests.Handlers.Tribes;

internal sealed class FakeTribeRepository : ITribeRepository
{
    public Dictionary<(byte TribeId, byte SlotIndex), int> Bank { get; } = new();
    public List<TribeSubMasterDto> SubMasters { get; } = [];
    public long MoneyAfterWithdraw { get; set; }
    public Exception? WithdrawException { get; set; }
    public (byte TribeId, byte SlotIndex, int CharacterId)? LastWithdrawCall { get; private set; }

    public ValueTask<byte> GetRoleForCharacterAsync(int characterId, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask<ReadOnlyCollection<TribeSummaryDto>> GetAllAsync(CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask<ReadOnlyCollection<TribeSubMasterDto>> GetSubMastersAsync(byte tribeId, CancellationToken ct)
    {
        var matches = SubMasters.Where(s => s.TribeId == tribeId).ToList();
        return ValueTask.FromResult(new ReadOnlyCollection<TribeSubMasterDto>(matches));
    }

    public ValueTask SetSubMasterAsync(byte tribeId, byte slotIndex, int characterId, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask ClearSubMasterAsync(byte tribeId, int characterId, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask<ReadOnlyCollection<TribeBankSlotDto>> GetBankAsync(byte tribeId, CancellationToken ct)
    {
        var slots = Bank
            .Where(kv => kv.Key.TribeId == tribeId)
            .Select(kv => new TribeBankSlotDto(tribeId, kv.Key.SlotIndex, kv.Value))
            .ToList();
        return ValueTask.FromResult(new ReadOnlyCollection<TribeBankSlotDto>(slots));
    }

    public ValueTask<long> WithdrawBankAsync(byte tribeId, byte slotIndex, int characterId, CancellationToken ct)
    {
        LastWithdrawCall = (tribeId, slotIndex, characterId);

        if (WithdrawException is { } ex)
            throw ex;

        Bank[(tribeId, slotIndex)] = 0;
        return ValueTask.FromResult(MoneyAfterWithdraw);
    }
}
