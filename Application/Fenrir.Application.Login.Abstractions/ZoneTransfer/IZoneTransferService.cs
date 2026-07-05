namespace Fenrir.Application.Login.Abstractions.ZoneTransfer;

public enum ZoneTransferOutcome
{
    CharacterNotFound,
    ShardUnavailable,
    Success
}

public readonly record struct ZoneTransferResult(ZoneTransferOutcome Outcome, string Ip, int Port, short Zone);

public interface IZoneTransferService
{
    public ValueTask<ZoneTransferResult> RequestZoneTransferAsync(int accountId, byte avatarPost,
        CancellationToken cancellationToken);
}
