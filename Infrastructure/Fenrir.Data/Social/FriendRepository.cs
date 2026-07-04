using System.Collections.ObjectModel;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;

namespace Fenrir.Data.Social;

// One-directional by design: a row only says "CharacterId considers FriendCharacterId a friend".
public sealed record FriendRepository(ICaeriusNetDbContext Db) : IFriendRepository
{
    /// <summary>Loaded once at world entry (AVATAR_INFO's Friend[10]), never re-queried; Add/Remove also update the in-memory mirror.</summary>
    public async ValueTask<ReadOnlyCollection<CharacterFriendDto>> GetByCharacterAsync(int characterId,
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_CharacterFriend_GetByCharacter", 10)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .Build();

        return await Db.QueryAsReadOnlyCollectionAsync<CharacterFriendDto>(sp, ct);
    }

    /// <summary>CZ_FRIEND_MAKE_SEND (56); writes one slot for characterId only. Throws SQL 50267 if the slot is already occupied (only possible via a race).</summary>
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
