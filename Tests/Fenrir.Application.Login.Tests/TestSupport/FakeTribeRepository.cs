using System.Collections.ObjectModel;
using Fenrir.Data.Abstractions.Tribes;

namespace Fenrir.Application.Login.Tests.TestSupport;

/// <summary>
///     In-memory stand-in for ITribeRepository, used only for CreateAvatarService's dominant-tribe creation
///     gate -- every member besides <see cref="GetAllAsync" /> throws, since nothing else on this path calls
///     them.
/// </summary>
internal sealed class FakeTribeRepository : ITribeRepository
{
    public List<TribeSummaryDto> Standings { get; init; } = [];

    /// <summary>0 (regular member) unless overridden per character id -- used by DeleteAvatarService tests.</summary>
    public Dictionary<int, byte> RolesByCharacterId { get; init; } = new();

    public ValueTask<ReadOnlyCollection<TribeSummaryDto>> GetAllAsync(CancellationToken ct)
    {
        return ValueTask.FromResult(new ReadOnlyCollection<TribeSummaryDto>(Standings));
    }

    public ValueTask<byte> GetRoleForCharacterAsync(int characterId, CancellationToken ct)
    {
        return ValueTask.FromResult(RolesByCharacterId.GetValueOrDefault(characterId));
    }

    public ValueTask SetMasterAsync(byte tribeId, int? newMasterCharacterId, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask<ReadOnlyCollection<TribeSubMasterDto>> GetSubMastersAsync(byte tribeId, CancellationToken ct)
    {
        throw new NotSupportedException();
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
        throw new NotSupportedException();
    }

    public ValueTask<long> WithdrawBankAsync(byte tribeId, byte slotIndex, int characterId, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask<long> DepositBankAsync(byte tribeId, byte slotIndex, int characterId, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    /// <summary>No standings seeded at all -- the "before any WorldState bootstrap" edge case.</summary>
    public static FakeTribeRepository Empty()
    {
        return new FakeTribeRepository();
    }

    public static FakeTribeRepository WithPoints(params (byte TribeId, int Points)[] standings)
    {
        return new FakeTribeRepository
        {
            Standings = [.. standings.Select(s => new TribeSummaryDto(s.TribeId, null, s.Points))]
        };
    }

    /// <summary>Seeds a single character's ReturnTribeRole encoding (0=none, 1=master, 2=sub-master).</summary>
    public static FakeTribeRepository WithRole(int characterId, byte role)
    {
        return new FakeTribeRepository { RolesByCharacterId = { [characterId] = role } };
    }
}
