namespace Fenrir.Data.Abstractions.Accounts;

public interface IAccountPinRepository
{

        public ValueTask<AccountPinDto?> GetAsync(int accountId, CancellationToken ct);

        public ValueTask SetAsync(int accountId, byte[] pinHash, byte[] pinSalt, CancellationToken ct);

        public ValueTask RecordAttemptAsync(int accountId, bool success, CancellationToken ct);
}
