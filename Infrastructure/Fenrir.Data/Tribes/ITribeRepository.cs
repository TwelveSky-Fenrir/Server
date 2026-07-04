using System.Collections.ObjectModel;

namespace Fenrir.Data.Tribes;

public interface ITribeRepository
{
    public ValueTask<byte> GetRoleForCharacterAsync(int characterId, CancellationToken ct);

    public ValueTask<ReadOnlyCollection<TribeSummaryDto>> GetAllAsync(CancellationToken ct);

    public ValueTask<ReadOnlyCollection<TribeSubMasterDto>> GetSubMastersAsync(byte tribeId, CancellationToken ct);

    public ValueTask SetSubMasterAsync(byte tribeId, byte slotIndex, int characterId, CancellationToken ct);

    public ValueTask ClearSubMasterAsync(byte tribeId, int characterId, CancellationToken ct);

    public ValueTask<ReadOnlyCollection<TribeBankSlotDto>> GetBankAsync(byte tribeId, CancellationToken ct);

    /// <summary>
    ///     Atomically empties one tribe-bank slot into <paramref name="characterId" />'s own money (the whole
    ///     balance, never a partial amount -- matches the legacy PlayUser process's
    ///     ZONE_TRIBE_BANK_LOAD_FOR_PLAYUSER_SEND). Returns the character's new Money total. Throws on an
    ///     empty slot or on a resulting balance that would exceed the legacy money cap.
    /// </summary>
    public ValueTask<long> WithdrawBankAsync(byte tribeId, byte slotIndex, int characterId, CancellationToken ct);
}
