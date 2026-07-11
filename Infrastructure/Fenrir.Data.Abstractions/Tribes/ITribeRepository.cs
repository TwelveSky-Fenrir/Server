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
    ///     <para>
    ///         <b>Correction:</b> backs CZ_TRIBE_BANK_SEND (opcode 82) sort 2 -- a fresh, definitive full read
    ///         of Server/ts25zone/S04_MyWork02.cpp:11560-11607 and Server/ts25playuser/S04_MyWork02.cpp:269-377
    ///         resolved a 3-way contradiction in favor of sort 2 being exclusively this withdraw. An earlier
    ///         revision of this doc comment claimed the opposite (that this method had no application-layer
    ///         caller and that sort 2 was a deposit); that claim was wrong. The application-layer caller is
    ///         <c>TribeBankWithdrawService.WithdrawAsync</c>, invoked directly from <c>TribeBankHandler</c>.
    ///     </para>
    /// </summary>
    public ValueTask<long> WithdrawBankAsync(byte tribeId, byte slotIndex, int characterId, CancellationToken ct);

    /// <summary>
    ///     Atomically moves <paramref name="characterId" />'s entire current Money (the whole balance, never a
    ///     partial amount -- the mirror image of <see cref="WithdrawBankAsync" />) into one tribe-bank slot.
    ///     Returns the character's new (post-deposit) Money total. Throws if the character currently has no
    ///     money to deposit.
    ///     <para>
    ///         <b>Correction:</b> does NOT back CZ_TRIBE_BANK_SEND sort 2 or any other client-invocable
    ///         sub-command of opcode 82 -- see <see cref="WithdrawBankAsync" />'s own remarks for the
    ///         corrected finding. Legacy has no client-invocable deposit path at all; this method has no
    ///         application-layer caller as of this correction (its one caller, <c>TribeBankService.DepositAsync</c>,
    ///         is itself no longer reachable from any opcode). Kept, not removed, pending a separate decision.
    ///     </para>
    /// </summary>
    public ValueTask<long> DepositBankAsync(byte tribeId, byte slotIndex, int characterId, CancellationToken ct);
}
