using Fenrir.Application.Login.Abstractions.Login;
using Fenrir.Application.Login.Domain;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Login;

namespace Fenrir.Application.Login.Handlers.Handlers;

/// <summary>
///     op11 CL_LOGIN_SEND — IP rate limit, then the application firewall, then version, then MAC restriction, then
///     auth run in that order so an over-budget/blocked/incompatible/banned-PC attempt never reaches Argon2id/SQL
///     account lookup.
/// </summary>
public sealed class LoginHandler(ILoginService loginService) : IAsyncPacketHandler<LoginRequest>
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
            case LoginOutcome.Failure:
                // Re-arms VersionOk so the client can retry on this same connection without a reconnect.
                if (result.ReArmVersionOk)
                    loginSession.MarkVersionOk();
                LoginTrain.SendFailure(session, result.ResultCode, packet.Id, result.ResultString);
                return;
            default:
                loginSession.MarkAuthenticated(result.AccountId);
                if (result.RequirePin)
                    loginSession.MarkPinRequired();

                var secondLoginSort = result.RequirePin ? 1 : 0;
                LoginTrain.Send(session,
                    LoginTrain.BuildLoginRecv(ResultSuccess, "MG" + result.AccountId, secondLoginSort,
                        result.PinMask),
                    LoginTrain.BuildAvatarSlots(result.Characters));
                return;
        }
    }
}
