using System.Collections.ObjectModel;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Login.Tests.TestSupport;

/// <summary>
///     In-memory stand-in for IWorldStateRepository, used only for DeleteAvatarService's tribe-role/election-
///     candidacy refusal check -- every member besides <see cref="GetTribeVotesAsync" /> throws, since nothing
///     else on this path calls them.
/// </summary>
internal sealed class FakeWorldStateRepository : IWorldStateRepository
{
    private readonly Dictionary<byte, List<TribeVoteDto>> _votesByTribe = new();

    /// <summary>No tribe has any registered Force Leader election candidates.</summary>
    public static FakeWorldStateRepository Empty()
    {
        return new FakeWorldStateRepository();
    }

    public static FakeWorldStateRepository WithVotes(params TribeVoteDto[] votes)
    {
        var repository = new FakeWorldStateRepository();
        foreach (var vote in votes)
        {
            if (!repository._votesByTribe.TryGetValue(vote.TribeId, out var slots))
                repository._votesByTribe[vote.TribeId] = slots = [];
            slots.Add(vote);
        }

        return repository;
    }

    public ValueTask<ReadOnlyCollection<TribeVoteDto>> GetTribeVotesAsync(byte tribeId, CancellationToken ct)
    {
        var votes = _votesByTribe.TryGetValue(tribeId, out var slots) ? slots : [];
        return ValueTask.FromResult(new ReadOnlyCollection<TribeVoteDto>(votes));
    }

    public ValueTask EnsureInitializedAsync(CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask<(WorldStateRowDto? Row, ReadOnlyCollection<WorldStateTribeDto> Tribes,
        ReadOnlyCollection<WorldStateAllianceOfferDto> AllianceOffers)> GetAsync(CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask UpdateAsync(byte? zone038WinTribe, int? zone038WinTribeTime, bool tribeSymbolBattle,
        byte? monsterSymbol, int? monsterSymbolEndTime, byte? highTribe, short updateTribePoint,
        CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask UpdateTribeAsync(byte tribeId, DateTime? symbolDate, bool hasSymbol, int points, bool isClosed,
        CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask UpdateTribeSymbolStateAsync(byte tribeId, DateTime? symbolDate, bool hasSymbol, bool isClosed,
        CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask AddTribePointsAsync(byte tribeId, int delta, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask SetAllianceOfferAsync(byte fromTribeId, byte toTribeId, bool isAccepted, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask RegisterTribeVoteCandidateAsync(byte tribeId, byte slotIndex, int candidateCharacterId,
        short candidateLevel, int killOtherTribeCount, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask AddTribeVotePointsAsync(byte tribeId, byte slotIndex, int points, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask ClearTribeVotesAsync(byte tribeId, CancellationToken ct)
    {
        throw new NotSupportedException();
    }
}
