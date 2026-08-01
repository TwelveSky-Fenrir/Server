namespace Fenrir.Application.Login.Abstractions.RetiredItems;

public interface IRetiredItemPurgeService
{
    public ValueTask PurgeAsync(IReadOnlyList<CharacterRosterItemDto> rosterItems,
        CancellationToken cancellationToken);
}
