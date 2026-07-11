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
    public async ValueTask EnsureInitializedAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_WorldState_EnsureInitialized", 0).Build();
        await Db.ExecuteAsync(sp, ct);
    }

        public async ValueTask<(WorldStateRowDto? Row, ReadOnlyCollection<WorldStateTribeDto> Tribes,
            ReadOnlyCollection<WorldStateAllianceOfferDto> AllianceOffers)>
        GetAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_WorldState_Get", 17).Build();

        var (rows, tribes, allianceOffers) = await Db
            .QueryMultipleReadOnlyCollectionAsync<WorldStateRowDto, WorldStateTribeDto, WorldStateAllianceOfferDto>(
                sp, ct);

        return (rows.Count > 0 ? rows[0] : null, tribes, allianceOffers);
    }

    public async ValueTask UpdateAsync(byte? zone038WinTribe, int? zone038WinTribeTime, bool tribeSymbolBattle,
        byte? monsterSymbol, int? monsterSymbolEndTime, byte? highTribe, short updateTribePoint, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_WorldState_Update", 0)
            .AddParameter("Zone038WinTribe", (object?)zone038WinTribe ?? DBNull.Value, SqlDbType.TinyInt)
            .AddParameter("Zone038WinTribeTime", (object?)zone038WinTribeTime ?? DBNull.Value, SqlDbType.Int)
            .AddParameter("TribeSymbolBattle", tribeSymbolBattle, SqlDbType.Bit)
            .AddParameter("MonsterSymbol", (object?)monsterSymbol ?? DBNull.Value, SqlDbType.TinyInt)
            .AddParameter("MonsterSymbolEndTime", (object?)monsterSymbolEndTime ?? DBNull.Value, SqlDbType.Int)
            .AddParameter("HighTribe", (object?)highTribe ?? DBNull.Value, SqlDbType.TinyInt)
            .AddParameter("UpdateTribePoint", updateTribePoint, SqlDbType.SmallInt)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask UpdateTribeAsync(byte tribeId, DateTime? symbolDate, bool hasSymbol, int points,
        bool isClosed, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_WorldStateTribe_Update", 0)
            .AddParameter("TribeId", tribeId, SqlDbType.TinyInt)
            .AddParameter("SymbolDate", (object?)symbolDate ?? DBNull.Value, SqlDbType.DateTime2)
            .AddParameter("HasSymbol", hasSymbol, SqlDbType.Bit)
            .AddParameter("Points", points, SqlDbType.Int)
            .AddParameter("IsClosed", isClosed, SqlDbType.Bit)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask UpdateTribeSymbolStateAsync(byte tribeId, DateTime? symbolDate, bool hasSymbol,
        bool isClosed, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_WorldStateTribe_UpdateSymbolState", 0)
            .AddParameter("TribeId", tribeId, SqlDbType.TinyInt)
            .AddParameter("SymbolDate", (object?)symbolDate ?? DBNull.Value, SqlDbType.DateTime2)
            .AddParameter("HasSymbol", hasSymbol, SqlDbType.Bit)
            .AddParameter("IsClosed", isClosed, SqlDbType.Bit)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask AddTribePointsAsync(byte tribeId, int delta, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_WorldStateTribe_AddPoints", 0)
            .AddParameter("TribeId", tribeId, SqlDbType.TinyInt)
            .AddParameter("Delta", delta, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask SetAllianceOfferAsync(byte fromTribeId, byte toTribeId, bool isAccepted,
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_WorldStateAllianceOffer_Set", 0)
            .AddParameter("FromTribeId", fromTribeId, SqlDbType.TinyInt)
            .AddParameter("ToTribeId", toTribeId, SqlDbType.TinyInt)
            .AddParameter("IsAccepted", isAccepted, SqlDbType.Bit)
            .Build();

        await Db.ExecuteAsync(sp, ct);
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
}
