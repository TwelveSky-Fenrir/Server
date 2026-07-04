using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;

namespace Fenrir.Data.Admin;

/// <summary>
///     admin.Mutes access (Server Logic chapter, Phase C/V6 Social -- report 06 §1.7's own note: "the mute
///     repository lands with the Phase C chat vertical"). Singleton, injected only with
///     ICaeriusNetDbContext, same posture as every other repository in this project.
/// </summary>
public sealed record MuteRepository(ICaeriusNetDbContext Db)
{
    /// <summary>
    ///     Called ONCE at world entry (alongside <c>CharacterRepository.GetWorldEntryBundleAsync</c>) --
    ///     never re-queried per chat message (admin.usp_Mute_GetActiveForCharacter's own header). Covers
    ///     both character-level and account-level mutes in one round trip; only the presence of at least
    ///     one active row matters to the caller (cached as a hidden boolean flag on the game server's own
    ///     player runtime state, never re-queried thereafter).
    /// </summary>
    public async ValueTask<bool> IsActiveForCharacterAsync(int characterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("admin", "usp_Mute_GetActiveForCharacter", 1)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .Build();

        var rows = await Db.QueryAsReadOnlyCollectionAsync<MuteRowDto>(sp, ct);
        return rows.Count > 0;
    }
}
