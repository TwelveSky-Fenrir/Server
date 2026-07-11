using Fenrir.Application.Game.Abstractions.GenericAction;

namespace Fenrir.Application.Game.Abstractions.Inventory;

public interface IBigMoneyTransferService
{

        public ValueTask<GenericActionResult> TransferStoreAsync(int sort, byte[] data, int characterId,
        CancellationToken cancellationToken);

        public ValueTask<GenericActionResult> TransferSaveAsync(int sort, byte[] data, int accountId, int characterId,
        CancellationToken cancellationToken);
}
