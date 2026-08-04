using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.Data.Runtime;

public sealed record WorldEventLocalEffectRepository(ICaeriusNetDbContext Db) : IWorldEventLocalEffectRepository
{
    public async ValueTask<WorldStateInboundEffectResultDto> ApplyWorldStateHighTribeAsync(
        WorldStateInboundEffectRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(request);

        if (!WorldStateHighTribeEffectPayload.TryRead(request.Payload, out var highTribe))
            throw new ArgumentException("The world-state high-tribe payload is invalid.", nameof(request));

        var sp = new StoredProcedureParametersBuilder("runtime", "usp_WorldInboxEffect_ApplyWorldStateHighTribe", 1, 5)
            .AddParameter("OutboxId", request.OutboxId, SqlDbType.BigInt)
            .AddParameter("DestinationShardId", request.DestinationShardId, SqlDbType.TinyInt)
            .AddParameter("OperationKey", request.OperationKey, SqlDbType.UniqueIdentifier)
            .AddParameter("Payload", request.Payload, SqlDbType.VarBinary, WorldStateHighTribeEffectPayload.Size)
            .AddParameter("HighTribe", (object?)highTribe ?? DBNull.Value, SqlDbType.TinyInt)
            .Build();

        return await Db.FirstQueryAsync<WorldStateInboundEffectResultDto>(sp, ct).ConfigureAwait(false) ??
               throw new InvalidOperationException(
                   "usp_WorldInboxEffect_ApplyWorldStateHighTribe always returns an effect result.");
    }

    private static void Validate(WorldStateInboundEffectRequest request)
    {
        if (request.OutboxId <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "The outbox identifier must be positive.");
        if (request.OperationKey == Guid.Empty)
            throw new ArgumentException("A local world effect requires an operation key.", nameof(request));
        if (!WorldStateHighTribeEffectPayload.TryRead(request.Payload, out _))
            throw new ArgumentException("The world-state high-tribe payload is invalid.", nameof(request));
    }
}
