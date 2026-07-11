using System.Collections.Immutable;
using System.Net;
using Fenrir.Application.Login.Domain.Avatars;
using Fenrir.Network.Serialization.Login.Packets.Login;

namespace Fenrir.Application.Login.Abstractions.Login;

public enum LoginOutcome
{

        RateLimited,
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
    public static LoginResult RateLimitedResult { get; } =
        new(LoginOutcome.RateLimited, 0, "", false, 0, false, "", []);

        public static LoginResult SilentDropResult { get; } =
        new(LoginOutcome.DuplicateSessionEvicted, 0, "", false, 0, false, "", []);
}

public interface ILoginService
{
    public ValueTask<LoginResult> LoginAsync(long sessionId, IPEndPoint? remoteEndPoint, LoginRequest packet,
        CancellationToken cancellationToken);
}
