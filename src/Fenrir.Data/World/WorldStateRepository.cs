using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Data.World;

public sealed record WorldStateRepository(ICaeriusNetDbContext Db) : IWorldStateRepository
{
    private const int CompatibilityMutationMaxAttempts = 3;

    public async ValueTask EnsureInitializedAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_WorldState_EnsureInitialized", 0).Build();
        await Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask<(WorldStateRowDto? Row, ImmutableArray<WorldStateTribeDto> Tribes,
            ImmutableArray<WorldStateAllianceOfferDto> AllianceOffers)>
        GetAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_WorldState_Get", 18).Build();

        var (rows, tribes, allianceOffers) = await Db
            .QueryMultipleImmutableArrayAsync<WorldStateRowDto, WorldStateTribeDto, WorldStateAllianceOfferDto>(
                sp, ct);

        return (rows.Length > 0 ? rows[0] : null, tribes, allianceOffers);
    }

    public async ValueTask<bool> TryUpdateAsync(byte? zone038WinTribe, int? zone038WinTribeTime,
        bool tribeSymbolBattle, byte? monsterSymbol, int? monsterSymbolEndTime, byte? highTribe,
        short updateTribePoint, long expectedRevision, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(expectedRevision);

        var sp = new StoredProcedureParametersBuilder("game", "usp_WorldState_Update", 1)
            .AddParameter("Zone038WinTribe", (object?)zone038WinTribe ?? DBNull.Value, SqlDbType.TinyInt)
            .AddParameter("Zone038WinTribeTime", (object?)zone038WinTribeTime ?? DBNull.Value, SqlDbType.Int)
            .AddParameter("TribeSymbolBattle", tribeSymbolBattle, SqlDbType.Bit)
            .AddParameter("MonsterSymbol", (object?)monsterSymbol ?? DBNull.Value, SqlDbType.TinyInt)
            .AddParameter("MonsterSymbolEndTime", (object?)monsterSymbolEndTime ?? DBNull.Value, SqlDbType.Int)
            .AddParameter("HighTribe", (object?)highTribe ?? DBNull.Value, SqlDbType.TinyInt)
            .AddParameter("UpdateTribePoint", updateTribePoint, SqlDbType.SmallInt)
            .AddParameter("ExpectedRevision", expectedRevision, SqlDbType.BigInt)
            .Build();

        return await Db.ExecuteScalarAsync<bool>(sp, ct);
    }

    public async ValueTask<bool> TryUpdateTribePointsAsync(byte tribeId, int points,
        long expectedWorldStateRevision, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(expectedWorldStateRevision);

        var sp = new StoredProcedureParametersBuilder("game", "usp_WorldStateTribe_Update", 1)
            .AddParameter("TribeId", tribeId, SqlDbType.TinyInt)
            .AddParameter("Points", points, SqlDbType.Int)
            .AddParameter("ExpectedWorldStateRevision", expectedWorldStateRevision, SqlDbType.BigInt)
            .Build();

        return await Db.ExecuteScalarAsync<bool>(sp, ct);
    }

    public async ValueTask UpdateTribeAsync(byte tribeId, DateTime? symbolDateUtc, bool hasSymbol, int points,
        bool isClosed, byte symbolOwnerTribeId, CancellationToken ct)
    {
        await ApplyCompatibilityMutationAsync(
            (revision, token) => TryUpdateTribePointsAsync(tribeId, points, revision, token), ct);
    }

    public async ValueTask<bool> TryUpdateTribeSymbolStateAsync(byte tribeId, DateTime? symbolDateUtc,
        bool hasSymbol, bool isClosed, byte symbolOwnerTribeId, long expectedWorldStateRevision, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(expectedWorldStateRevision);

        var sp = new StoredProcedureParametersBuilder("game", "usp_WorldStateTribe_UpdateSymbolState", 1)
            .AddParameter("TribeId", tribeId, SqlDbType.TinyInt)
            .AddParameter("SymbolDateUtc", (object?)symbolDateUtc ?? DBNull.Value, SqlDbType.DateTime2)
            .AddParameter("HasSymbol", hasSymbol, SqlDbType.Bit)
            .AddParameter("IsClosed", isClosed, SqlDbType.Bit)
            .AddParameter("SymbolOwnerTribeId", symbolOwnerTribeId, SqlDbType.TinyInt)
            .AddParameter("ExpectedWorldStateRevision", expectedWorldStateRevision, SqlDbType.BigInt)
            .Build();

        return await Db.ExecuteScalarAsync<bool>(sp, ct);
    }

    public async ValueTask UpdateTribeSymbolStateAsync(byte tribeId, DateTime? symbolDateUtc, bool hasSymbol,
        bool isClosed, byte symbolOwnerTribeId, CancellationToken ct)
    {
        await ApplyCompatibilityMutationAsync(
            (revision, token) => TryUpdateTribeSymbolStateAsync(tribeId, symbolDateUtc, hasSymbol, isClosed,
                symbolOwnerTribeId, revision, token), ct);
    }

    public async ValueTask<bool> TryAddTribePointsAsync(byte tribeId, int delta, long expectedWorldStateRevision,
        CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(expectedWorldStateRevision);

        var sp = new StoredProcedureParametersBuilder("game", "usp_WorldStateTribe_AddPoints", 1)
            .AddParameter("TribeId", tribeId, SqlDbType.TinyInt)
            .AddParameter("Delta", delta, SqlDbType.Int)
            .AddParameter("ExpectedWorldStateRevision", expectedWorldStateRevision, SqlDbType.BigInt)
            .Build();

        return await Db.ExecuteScalarAsync<bool>(sp, ct);
    }

    public async ValueTask AddTribePointsAsync(byte tribeId, int delta, CancellationToken ct)
    {
        await ApplyCompatibilityMutationAsync(
            (revision, token) => TryAddTribePointsAsync(tribeId, delta, revision, token), ct);
    }

    public async ValueTask<bool> TrySetAllianceOfferAsync(byte fromTribeId, byte toTribeId, bool isAccepted,
        long expectedWorldStateRevision, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(expectedWorldStateRevision);

        var sp = new StoredProcedureParametersBuilder("game", "usp_WorldStateAllianceOffer_Set", 1)
            .AddParameter("FromTribeId", fromTribeId, SqlDbType.TinyInt)
            .AddParameter("ToTribeId", toTribeId, SqlDbType.TinyInt)
            .AddParameter("IsAccepted", isAccepted, SqlDbType.Bit)
            .AddParameter("ExpectedWorldStateRevision", expectedWorldStateRevision, SqlDbType.BigInt)
            .Build();

        return await Db.ExecuteScalarAsync<bool>(sp, ct);
    }

    public async ValueTask SetAllianceOfferAsync(byte fromTribeId, byte toTribeId, bool isAccepted,
        CancellationToken ct)
    {
        await ApplyCompatibilityMutationAsync(
            (revision, token) => TrySetAllianceOfferAsync(fromTribeId, toTribeId, isAccepted, revision, token), ct);
    }

    public async ValueTask<ReadOnlyCollection<TribeVoteDto>> GetTribeVotesAsync(byte tribeId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_TribeVote_GetByTribe", 10)
            .AddParameter("TribeId", tribeId, SqlDbType.TinyInt)
            .Build();

        return await Db.QueryAsReadOnlyCollectionAsync<TribeVoteDto>(sp, ct);
    }

    public async ValueTask RegisterTribeVoteCandidateAsync(byte tribeId, byte slotIndex, int candidateCharacterId,
        short candidateLevel, int killOtherTribeCount, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_TribeVote_Register", 0)
            .AddParameter("TribeId", tribeId, SqlDbType.TinyInt)
            .AddParameter("SlotIndex", slotIndex, SqlDbType.TinyInt)
            .AddParameter("CandidateCharacterId", candidateCharacterId, SqlDbType.Int)
            .AddParameter("CandidateLevel", candidateLevel, SqlDbType.SmallInt)
            .AddParameter("KillOtherTribeCount", killOtherTribeCount, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask AddTribeVotePointsAsync(byte tribeId, byte slotIndex, int points, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_TribeVote_AddPoints", 0)
            .AddParameter("TribeId", tribeId, SqlDbType.TinyInt)
            .AddParameter("SlotIndex", slotIndex, SqlDbType.TinyInt)
            .AddParameter("Points", points, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask ClearTribeVotesAsync(byte tribeId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_TribeVote_ClearTribe", 0)
            .AddParameter("TribeId", tribeId, SqlDbType.TinyInt)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    private async ValueTask ApplyCompatibilityMutationAsync(
        Func<long, CancellationToken, ValueTask<bool>> tryApply, CancellationToken ct)
    {
        for (var attempt = 0; attempt < CompatibilityMutationMaxAttempts; attempt++)
        {
            var (row, _, _) = await GetAsync(ct);
            if (row is null)
                throw new InvalidOperationException(
                    "Cannot mutate world state because its singleton row is missing after initialization.");

            if (await tryApply(row.Revision, ct))
                return;
        }

        throw new InvalidOperationException(
            $"World-state mutation conflicted {CompatibilityMutationMaxAttempts} consecutive times; the caller must reload and retry.");
    }
}
