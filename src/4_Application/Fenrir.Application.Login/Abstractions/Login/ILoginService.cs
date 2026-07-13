using System.Collections.Immutable;
using System.Net;
using Fenrir.Domain.Login.Avatars;
using Fenrir.Application.Login.Packets;

namespace Fenrir.Application.Login.Abstractions.Login;

public enum LoginOutcome
{
    Failure,
    Success,

    DuplicateSessionEvicted
}

public sealed record LoginResult(
    LoginOutcome Outcome,
    int ResultCode,
    string ResultString,
    bool ReArmVersionOk,
    int AccountId,
    bool RequirePin,
    string PinMask,
    ImmutableArray<AvatarRosterEntry> Characters,
    Guid? SessionToken = null,
    short AccountGrade = 0)
{
    public static LoginResult SilentDropResult { get; } =
        new(LoginOutcome.DuplicateSessionEvicted, 0, "", false, 0, false, "", []);
}

public interface ILoginService
{
    public ValueTask<LoginResult> LoginAsync(long sessionId, IPEndPoint? remoteEndPoint, LoginRequest packet,
        CancellationToken cancellationToken);
}
