using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using Fenrir.Data.Abstractions.Admin;

namespace Fenrir.Data.Admin;

public sealed record MuteRepository(ICaeriusNetDbContext Db) : IMuteRepository
{
    /// <summary>Called once at world entry, never per chat message; result is cached as a bool on player runtime state.</summary>
    public async ValueTask<bool> IsActiveForCharacterAsync(int characterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("admin", "usp_Mute_GetActiveForCharacter", 1)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .Build();

        var rows = await Db.QueryAsReadOnlyCollectionAsync<MuteRowDto>(sp, ct);
        return rows.Count > 0;
    }
}
