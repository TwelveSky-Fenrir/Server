using System.Collections.ObjectModel;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Data.World;

public sealed record WorldEventSnapshotRepository(ICaeriusNetDbContext Db) : IWorldEventSnapshotRepository
{
    private const int EventKindLength = 48;
    private const int OccurrenceKeyLength = 96;
    private const int PhaseLength = 48;
    private const int Sha256HashLength = 32;

    public async ValueTask<ReadOnlyCollection<WorldEventSnapshotRowDto>> LoadAllAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_WorldEventSnapshot_LoadAll", 16).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<WorldEventSnapshotRowDto>(sp, ct);
    }

    public async ValueTask<bool> TryApplyAsync(string eventKind, string occurrenceKey, long expectedRevision,
        string phase, string canonicalPayload, byte[] canonicalPayloadHash, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(occurrenceKey);
        ArgumentOutOfRangeException.ThrowIfNegative(expectedRevision);
        ArgumentException.ThrowIfNullOrWhiteSpace(phase);
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalPayload);
        ArgumentNullException.ThrowIfNull(canonicalPayloadHash);
        ArgumentOutOfRangeException.ThrowIfNotEqual(canonicalPayloadHash.Length, Sha256HashLength);

        var sp = new StoredProcedureParametersBuilder("game", "usp_WorldEventSnapshot_Apply", 1)
            .AddParameter("EventKind", eventKind, SqlDbType.VarChar, EventKindLength)
            .AddParameter("OccurrenceKey", occurrenceKey, SqlDbType.VarChar, OccurrenceKeyLength)
            .AddParameter("ExpectedRevision", expectedRevision, SqlDbType.BigInt)
            .AddParameter("Phase", phase, SqlDbType.VarChar, PhaseLength)
            .AddParameter("CanonicalPayload", canonicalPayload, SqlDbType.NVarChar)
            .AddParameter("CanonicalPayloadHash", canonicalPayloadHash, SqlDbType.Binary, Sha256HashLength)
            .Build();

        return await Db.ExecuteScalarAsync<bool>(sp, ct);
    }
}
