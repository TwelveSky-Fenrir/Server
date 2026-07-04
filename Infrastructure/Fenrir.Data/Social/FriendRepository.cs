using System.Data;
using System.Collections.ObjectModel;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;

namespace Fenrir.Data.Social;

/// <summary>
///     game.CharacterFriends access (Phase C/V6 Social -- CZ_FRIEND_* family, contracts/05_social.md).
///     One-directional by design (see game.CharacterFriends' own header): a row here only ever says
///     "CharacterId considers FriendCharacterId a friend".
/// </summary>
public sealed record FriendRepository(ICaeriusNetDbContext Db)
{
    /// <summary>Loaded once at world entry (AVATAR_INFO's <c>Friend[10]</c> field) -- never re-queried afterwards; live mutations (Add/Remove) go through the same call sites that update the in-memory mirror.</summary>
    public async ValueTask<ReadOnlyCollection<CharacterFriendDto>> GetByCharacterAsync(int characterId,
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_CharacterFriend_GetByCharacter", 10)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .Build();

        return await Db.QueryAsReadOnlyCollectionAsync<CharacterFriendDto>(sp, ct);
    }

    /// <summary>CZ_FRIEND_MAKE_SEND (opcode 56) -- writes ONE slot for <paramref name="characterId" /> only. Throws SQL error 50267 if the slot is already occupied (FriendRegistry has already verified this cannot happen under normal play; a race is the only way to hit it).</summary>
    public async ValueTask AddAsync(int characterId, byte slot, int friendCharacterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_CharacterFriend_Add", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("Slot", slot, SqlDbType.TinyInt)
            .AddParameter("FriendCharacterId", friendCharacterId, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    /// <summary>CZ_FRIEND_DELETE_SEND (opcode 58) -- clears one slot; idempotent (an already-empty slot is a silent no-op).</summary>
    public async ValueTask RemoveAsync(int characterId, byte slot, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_CharacterFriend_Remove", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("Slot", slot, SqlDbType.TinyInt)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }
}
