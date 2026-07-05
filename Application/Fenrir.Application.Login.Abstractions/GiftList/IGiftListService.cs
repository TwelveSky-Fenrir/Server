namespace Fenrir.Application.Login.Abstractions.GiftList;

public interface IGiftListService
{
    public ValueTask<int[]> GetGiftListAsync(int accountId, CancellationToken cancellationToken);
}
