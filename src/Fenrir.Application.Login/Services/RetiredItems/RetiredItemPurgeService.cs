using Fenrir.Application.Login.Abstractions.RetiredItems;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Services.RetiredItems;

public sealed class RetiredItemPurgeService(
    ICharacterRepository characters,
    ILogger<RetiredItemPurgeService> logger) : IRetiredItemPurgeService
{
    public async ValueTask PurgeAsync(IReadOnlyList<CharacterRosterItemDto> rosterItems,
        CancellationToken cancellationToken)
    {
        if (rosterItems.Count == 0)
            return;

        foreach (var container in rosterItems.GroupBy(item => (item.CharacterId, item.Container)))
        {
            var rows = container.ToArray();
            if (!Array.Exists(rows, item => RetiredLoginItemCatalog.IsRetired(item.ItemId)))
                continue;

            var (characterId, containerId) = container.Key;

            var kept = rows
                .Where(item => !RetiredLoginItemCatalog.IsRetired(item.ItemId))
                .Select(item => new CharacterItemSlotTvp(item.Slot, item.ItemId, item.Quantity, item.Enchant,
                    item.Combine, item.Refine, item.Socket, item.SocketGem1, item.SocketGem2, item.SocketGem3,
                    item.ExpireDate, item.Serial))
                .ToArray();

            try
            {
                await characters.ReplaceContainerAsync(characterId, containerId, kept, cancellationToken);

                logger.LogInformation(
                    "Retired-item purge: removed {PurgedCount} item(s) from character {CharacterId} container {Container}",
                    rows.Length - kept.Length, characterId, containerId);
            }
            catch (SqlException ex)
            {
                logger.LogWarning(ex,
                    "Retired-item purge failed for character {CharacterId} container {Container}; the roster projection still hides those items but they remain persisted",
                    characterId, containerId);
            }
        }
    }
}
