using System.Collections.ObjectModel;
using Fenrir.Data.Abstractions.Tribes;

namespace Fenrir.Application.Game.Tests.Handlers.Tribes;

internal sealed class FakeTribeRepository : ITribeRepository
{
    public Dictionary<(byte TribeId, byte SlotIndex), int> Bank { get; } = new();
    public List<TribeSubMasterDto> SubMasters { get; } = [];
    public List<TribeSummaryDto> Tribes { get; set; } = [];
    public List<(byte TribeId, int? NewMasterCharacterId)> SetMasterCalls { get; } = [];
    public long MoneyAfterWithdraw { get; set; }
    public Exception? WithdrawException { get; set; }
    public (byte TribeId, byte SlotIndex, int CharacterId)? LastWithdrawCall { get; private set; }
    public long MoneyAfterDeposit { get; set; }
    public Exception? DepositException { get; set; }
    public (byte TribeId, byte SlotIndex, int CharacterId)? LastDepositCall { get; private set; }
    public int DepositAmount { get; set; }

    public ValueTask<byte> GetRoleForCharacterAsync(int characterId, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask<ReadOnlyCollection<TribeSummaryDto>> GetAllAsync(CancellationToken ct)
    {
        return ValueTask.FromResult(new ReadOnlyCollection<TribeSummaryDto>(Tribes));
    }

    public ValueTask SetMasterAsync(byte tribeId, int? newMasterCharacterId, CancellationToken ct)
    {
        SetMasterCalls.Add((tribeId, newMasterCharacterId));

        var index = Tribes.FindIndex(t => t.TribeId == tribeId);
        if (index >= 0)
            Tribes[index] = Tribes[index] with { MasterCharacterId = newMasterCharacterId };

        return ValueTask.CompletedTask;
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

    public List<(byte TribeId, int CharacterId)> ClearSubMasterCalls { get; } = [];

    public ValueTask ClearSubMasterAsync(byte tribeId, int characterId, CancellationToken ct)
    {
        ClearSubMasterCalls.Add((tribeId, characterId));
        SubMasters.RemoveAll(s => s.TribeId == tribeId && s.CharacterId == characterId);
        return ValueTask.CompletedTask;
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

    public ValueTask<long> DepositBankAsync(byte tribeId, byte slotIndex, int characterId, CancellationToken ct)
    {
        LastDepositCall = (tribeId, slotIndex, characterId);

        if (DepositException is { } ex)
            throw ex;

        Bank[(tribeId, slotIndex)] = Bank.GetValueOrDefault((tribeId, slotIndex)) + DepositAmount;
        return ValueTask.FromResult(MoneyAfterDeposit);
    }
}
