using System.Collections.Immutable;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Characters;

namespace Fenrir.Data.Characters;

public sealed record CharacterLogoutStateRepository(ICaeriusNetDbContext Db) : ICharacterLogoutStateRepository
{
    public async ValueTask UpsertAsync(int characterId, int lastZone, int posX, int posY, int posZ, int life,
        int mana, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_CharacterLogoutState_Upsert", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("LastZone", lastZone, SqlDbType.Int)
            .AddParameter("PosX", posX, SqlDbType.Int)
            .AddParameter("PosY", posY, SqlDbType.Int)
            .AddParameter("PosZ", posZ, SqlDbType.Int)
            .AddParameter("Life", life, SqlDbType.Int)
            .AddParameter("Mana", mana, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask PersistBatchAsync(IReadOnlyList<CharacterLogoutStateTvp> rows, CancellationToken ct)
    {
        if (rows.Count == 0)
            return;

        var sp = new StoredProcedureParametersBuilder("game", "usp_CharacterLogoutState_PersistBatch", 0)
            .AddTvpParameter("Snapshots", rows)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask<ImmutableArray<CharacterLogoutStateDto>> GetByAccountAsync(int accountId,
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_CharacterLogoutState_GetByAccount", 3)
            .AddParameter("AccountId", accountId, SqlDbType.Int)
            .Build();

        return await Db.QueryAsImmutableArrayAsync<CharacterLogoutStateDto>(sp, ct);
    }
}
