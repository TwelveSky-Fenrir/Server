using System.Collections.ObjectModel;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Social;

namespace Fenrir.Data.Social;

public sealed record FriendRepository(ICaeriusNetDbContext Db) : IFriendRepository
{

        public async ValueTask<ReadOnlyCollection<CharacterFriendDto>> GetByCharacterAsync(int characterId,
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_CharacterFriend_GetByCharacter", 10)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .Build();

        return await Db.QueryAsReadOnlyCollectionAsync<CharacterFriendDto>(sp, ct);
    }

        public async ValueTask AddAsync(int characterId, byte slot, int friendCharacterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_CharacterFriend_Add", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("Slot", slot, SqlDbType.TinyInt)
            .AddParameter("FriendCharacterId", friendCharacterId, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

        public async ValueTask RemoveAsync(int characterId, byte slot, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_CharacterFriend_Remove", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("Slot", slot, SqlDbType.TinyInt)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }
}
