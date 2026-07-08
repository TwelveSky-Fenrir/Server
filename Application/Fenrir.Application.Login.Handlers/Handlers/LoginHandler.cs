using Fenrir.Application.Login.Abstractions.Login;
using Fenrir.Application.Login.Domain;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Login.Sessions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Login.Packets.Login;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Handlers.Handlers;

/// <summary>
///     op11 CL_LOGIN_SEND — IP rate limit, then maintenance lockdown, then server-full quota, then the
///     application firewall, then version, then MAC restriction, then auth run in that order so an
///     under-maintenance/over-capacity/over-budget/blocked/incompatible/banned-PC attempt never reaches
///     Argon2id/SQL account lookup. See <c>LoginService</c>'s own remarks for the maintenance/quota citation.
/// </summary>
public sealed class LoginHandler(ILoginService loginService, ILogger<LoginHandler> logger)
    : IAsyncPacketHandler<LoginRequest>
{
    private const int ResultSuccess = 0;

    public async ValueTask HandleAsync(LoginRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var loginSession = (LoginClientSession)session;

        var result = await loginService.LoginAsync(loginSession.SessionId, loginSession.RemoteEndPoint, packet,
            cancellationToken);

        switch (result.Outcome)
        {
            case LoginOutcome.RateLimited:
                // Silent drop, no reply/abort: a legitimate NAT-shared client that burst its IP budget just retries later.
                return;
            case LoginOutcome.DuplicateSessionEvicted:
                // Silent drop, no reply/abort: the stale local session for this account was just evicted; this
                // attempt is dropped too (see LoginService's ConflictLogin remarks) -- the client resends.
                return;
            case LoginOutcome.Failure:
                // Re-arms VersionOk so the client can retry on this same connection without a reconnect.
                if (result.ReArmVersionOk)
                    loginSession.MarkVersionOk();
                LoginTrain.SendFailure(session, result.ResultCode, packet.Id, result.ResultString);
                return;
            default:
                loginSession.MarkAuthenticated(result.AccountId, result.AccountGrade);
                loginSession.MarkAccountSessionToken(result.SessionToken!.Value);
                if (result.RequirePin)
                    loginSession.MarkPinRequired();

                logger.LogInformation(
                    "Session {SessionId} authenticated as account {AccountId}, PIN required {RequirePin}",
                    loginSession.SessionId, result.AccountId, result.RequirePin);

                var secondLoginSort = result.RequirePin ? 1 : 0;
                LoginTrain.Send(session,
                    LoginTrain.BuildLoginRecv(ResultSuccess, "MG" + result.AccountId, secondLoginSort,
                        result.PinMask),
                    LoginTrain.BuildAvatarSlots(result.Characters));
                return;
        }
    }
}
