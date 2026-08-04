using System.Net;

namespace Fenrir.Application.Login.Abstractions.ZoneTransfer;

public enum ZoneTransferOutcome
{
    CharacterNotFound,
    DeathPending,
    ShardUnavailable,
    Success,
    SlotEmpty
}

public readonly record struct ZoneTransferResult(ZoneTransferOutcome Outcome, string Ip, int Port, short Zone,
    string Capability);

public interface IZoneTransferService
{
    public ValueTask<ZoneTransferResult> RequestZoneTransferAsync(int accountId, byte avatarPost, Guid sessionToken,
        short accountGrade, IPAddress? sourceAddress, CancellationToken cancellationToken);
}
