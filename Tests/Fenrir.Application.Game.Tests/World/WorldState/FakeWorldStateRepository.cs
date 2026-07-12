using System.Collections.Immutable;
using System.Collections.ObjectModel;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.World.WorldState;

internal sealed class FakeWorldStateRepository : IWorldStateRepository
{
    public int EnsureInitializedCallCount { get; private set; }
    public int UpdateCallCount { get; private set; }

    public List<(byte TribeId, DateTime? SymbolDate, bool HasSymbol, int Points, bool IsClosed)> TribeUpdateCalls
    {
        get;
    } = [];

    public List<(byte TribeId, DateTime? SymbolDate, bool HasSymbol, bool IsClosed)> SymbolStateUpdateCalls { get; } =
        [];

    public List<(byte TribeId, int Delta)> TribePointsDeltaCalls { get; } = [];
    public bool ThrowOnAddTribePoints { get; set; }
    public List<(byte From, byte To, bool IsAccepted)> AllianceOfferCalls { get; } = [];
    public WorldStateRowDto Row { get; set; } = new(1, null, 0, false, null, 0, null, 0, DateTime.UtcNow);

    public List<WorldStateTribeDto> Tribes { get; set; } =
    [
        new(0, null, true, 0, false),
        new(1, null, true, 0, false),
        new(2, null, true, 0, false),
        new(3, null, true, 0, false)
    ];

    public List<WorldStateAllianceOfferDto> AllianceOffers { get; set; } = [];
    public Dictionary<byte, List<TribeVoteDto>> VotesByTribe { get; } = new();
    public bool ThrowOnUpdate { get; set; }
    public bool ThrowOnGet { get; set; }

    public (byte? Zone038WinTribe, int? Zone038WinTribeTime, bool TribeSymbolBattle, byte? MonsterSymbol,
        int? MonsterSymbolEndTime, byte? HighTribe, short UpdateTribePoint)? LastWorldUpdate { get; private set; }

    public HashSet<byte> ThrowOnUpdateTribeForIds { get; } = [];

    public List<(byte TribeId, byte SlotIndex, int CandidateCharacterId, short CandidateLevel, int
        KillOtherTribeCount)> RegisterCalls { get; } = [];

    public List<(byte TribeId, byte SlotIndex, int Points)> AddPointsCalls { get; } = [];
    public List<byte> ClearCalls { get; } = [];

    public ValueTask EnsureInitializedAsync(CancellationToken ct)
    {
        EnsureInitializedCallCount++;
        return ValueTask.CompletedTask;
    }

    public ValueTask<(WorldStateRowDto? Row, ImmutableArray<WorldStateTribeDto> Tribes,
            ImmutableArray<WorldStateAllianceOfferDto> AllianceOffers)>
        GetAsync(CancellationToken ct)
    {
        if (ThrowOnGet)
            throw new InvalidOperationException("Simulated reconcile-read failure.");

        return ValueTask.FromResult<(WorldStateRowDto?, ImmutableArray<WorldStateTribeDto>,
            ImmutableArray<WorldStateAllianceOfferDto>)>(
            (Row, [..Tribes], [..AllianceOffers]));
    }

    public ValueTask UpdateAsync(byte? zone038WinTribe, int? zone038WinTribeTime, bool tribeSymbolBattle,
        byte? monsterSymbol, int? monsterSymbolEndTime, byte? highTribe, short updateTribePoint, CancellationToken ct)
    {
        if (ThrowOnUpdate)
            throw new InvalidOperationException("Simulated flush failure.");

        UpdateCallCount++;
        LastWorldUpdate = (zone038WinTribe, zone038WinTribeTime, tribeSymbolBattle, monsterSymbol,
            monsterSymbolEndTime, highTribe, updateTribePoint);
        return ValueTask.CompletedTask;
    }

    public ValueTask UpdateTribeAsync(byte tribeId, DateTime? symbolDate, bool hasSymbol, int points, bool isClosed,
        CancellationToken ct)
    {
        if (ThrowOnUpdateTribeForIds.Contains(tribeId))
            throw new InvalidOperationException($"Simulated tribe {tribeId} update failure.");

        TribeUpdateCalls.Add((tribeId, symbolDate, hasSymbol, points, isClosed));
        return ValueTask.CompletedTask;
    }

    public ValueTask UpdateTribeSymbolStateAsync(byte tribeId, DateTime? symbolDate, bool hasSymbol, bool isClosed,
        CancellationToken ct)
    {
        SymbolStateUpdateCalls.Add((tribeId, symbolDate, hasSymbol, isClosed));
        return ValueTask.CompletedTask;
    }

    public ValueTask AddTribePointsAsync(byte tribeId, int delta, CancellationToken ct)
    {
        if (ThrowOnAddTribePoints)
            throw new InvalidOperationException("Simulated point-delta flush failure.");

        TribePointsDeltaCalls.Add((tribeId, delta));

        var tribe = Tribes.Single(t => t.TribeId == tribeId);
        Tribes[Tribes.IndexOf(tribe)] = tribe with { Points = tribe.Points + delta };
        return ValueTask.CompletedTask;
    }

    public ValueTask SetAllianceOfferAsync(byte fromTribeId, byte toTribeId, bool isAccepted, CancellationToken ct)
    {
        AllianceOfferCalls.Add((fromTribeId, toTribeId, isAccepted));
        return ValueTask.CompletedTask;
    }

    public ValueTask<ReadOnlyCollection<TribeVoteDto>> GetTribeVotesAsync(byte tribeId, CancellationToken ct)
    {
        var votes = VotesByTribe.TryGetValue(tribeId, out var list) ? list : [];
        return ValueTask.FromResult(new ReadOnlyCollection<TribeVoteDto>(votes.OrderByDescending(v => v.VotePoint)
            .ThenBy(v => v.SlotIndex).ToList()));
    }

    public ValueTask RegisterTribeVoteCandidateAsync(byte tribeId, byte slotIndex, int candidateCharacterId,
        short candidateLevel, int killOtherTribeCount, CancellationToken ct)
    {
        RegisterCalls.Add((tribeId, slotIndex, candidateCharacterId, candidateLevel, killOtherTribeCount));

        var list = VotesByTribe.TryGetValue(tribeId, out var existing) ? existing : VotesByTribe[tribeId] = [];
        list.RemoveAll(v => v.SlotIndex == slotIndex);
        list.Add(new TribeVoteDto(tribeId, slotIndex, candidateCharacterId, candidateLevel, killOtherTribeCount, 0,
            DateTime.UtcNow));

        return ValueTask.CompletedTask;
    }

    public ValueTask AddTribeVotePointsAsync(byte tribeId, byte slotIndex, int points, CancellationToken ct)
    {
        AddPointsCalls.Add((tribeId, slotIndex, points));

        if (VotesByTribe.TryGetValue(tribeId, out var list))
        {
            var index = list.FindIndex(v => v.SlotIndex == slotIndex);
            if (index >= 0)
                list[index] = list[index] with { VotePoint = list[index].VotePoint + points };
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask ClearTribeVotesAsync(byte tribeId, CancellationToken ct)
    {
        ClearCalls.Add(tribeId);
        VotesByTribe.Remove(tribeId);
        return ValueTask.CompletedTask;
    }
}
