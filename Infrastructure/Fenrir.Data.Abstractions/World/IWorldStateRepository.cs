using System.Collections.ObjectModel;

namespace Fenrir.Data.Abstractions.World;

public interface IWorldStateRepository
{
    public ValueTask EnsureInitializedAsync(CancellationToken ct);

    public ValueTask<(WorldStateRowDto? Row, ReadOnlyCollection<WorldStateTribeDto> Tribes,
        ReadOnlyCollection<WorldStateAllianceOfferDto> AllianceOffers)> GetAsync(CancellationToken ct);

    public ValueTask UpdateAsync(byte? zone038WinTribe, int? zone038WinTribeTime, bool tribeSymbolBattle,
        byte? monsterSymbol, int? monsterSymbolEndTime, byte? highTribe, short updateTribePoint, CancellationToken ct);

    public ValueTask UpdateTribeAsync(byte tribeId, DateTime? symbolDate, bool hasSymbol, int points, bool isClosed,
        CancellationToken ct);

    public ValueTask UpdateTribeSymbolStateAsync(byte tribeId, DateTime? symbolDate, bool hasSymbol, bool isClosed,
        CancellationToken ct);

    public ValueTask AddTribePointsAsync(byte tribeId, int delta, CancellationToken ct);

    public ValueTask SetAllianceOfferAsync(byte fromTribeId, byte toTribeId, bool isAccepted, CancellationToken ct);

    public ValueTask<ReadOnlyCollection<TribeVoteDto>> GetTribeVotesAsync(byte tribeId, CancellationToken ct);

    public ValueTask RegisterTribeVoteCandidateAsync(byte tribeId, byte slotIndex, int candidateCharacterId,
        short candidateLevel, int killOtherTribeCount, CancellationToken ct);

    public ValueTask AddTribeVotePointsAsync(byte tribeId, byte slotIndex, int points, CancellationToken ct);

    public ValueTask ClearTribeVotesAsync(byte tribeId, CancellationToken ct);
}
