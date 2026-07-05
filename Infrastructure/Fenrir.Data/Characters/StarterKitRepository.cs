using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;

namespace Fenrir.Data.Characters;

public sealed record StarterKitRepository(ICaeriusNetDbContext Db) : IStarterKitRepository
{
    public async ValueTask<StarterKitBundle> GetByPreviousTribeAsync(byte previousTribe, short mapId,
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_StarterKit_GetByPreviousTribe", 64)
            .AddParameter("PreviousTribe", previousTribe, SqlDbType.TinyInt)
            .AddParameter("MapId", mapId, SqlDbType.SmallInt)
            .Build();

        var (equipment, inventory, skills, hotkeys, spawn) = await Db
            .QueryMultipleReadOnlyCollectionAsync<StarterKitEquipmentRowDto, StarterKitInventoryRowDto,
                StarterKitSkillRowDto, StarterKitHotkeyRowDto, StarterKitSpawnRowDto>(sp, ct);

        return new StarterKitBundle(equipment, inventory, skills, hotkeys,
            spawn.Count > 0 ? spawn[0] : null);
    }
}
