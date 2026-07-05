using System.Collections.ObjectModel;

namespace Fenrir.Data.Abstractions.Tribes;

public interface ITribeRepository
{
    public ValueTask<byte> GetRoleForCharacterAsync(int characterId, CancellationToken ct);

    public ValueTask<ReadOnlyCollection<TribeSummaryDto>> GetAllAsync(CancellationToken ct);

    /// <summary>
    ///     TRIBE_WORK tSort 55's tally write: appoints (or, with a null character id, vacates) one tribe's
    ///     Force Leader. Throws if the tribe doesn't exist, or if a non-null candidate isn't a member of it.
    /// </summary>
    public ValueTask SetMasterAsync(byte tribeId, int? newMasterCharacterId, CancellationToken ct);

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

    /// <summary>
    ///     Atomically moves <paramref name="characterId" />'s entire current Money (the whole balance, never a
    ///     partial amount -- the mirror image of <see cref="WithdrawBankAsync" />; CZ_TRIBE_BANK_SEND carries no
    ///     separate amount field) into one tribe-bank slot. Returns the character's new (post-deposit) Money
    ///     total. Throws if the character currently has no money to deposit.
    /// </summary>
    public ValueTask<long> DepositBankAsync(byte tribeId, byte slotIndex, int characterId, CancellationToken ct);
}
