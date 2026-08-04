using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.Data.Runtime;

public sealed record RelaySourceIdentityRepository(ICaeriusNetDbContext Db) : IRelaySourceIdentityRepository
{
    public async ValueTask<RelaySourceIdentityDto?> GetAsync(int characterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_GetRelaySourceIdentity", 1)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .Build();

        return await Db.FirstQueryAsync<RelaySourceIdentityDto>(sp, ct);
    }
}
