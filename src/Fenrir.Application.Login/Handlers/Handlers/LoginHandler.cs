using Fenrir.Application.Login.Abstractions.Login;
using Fenrir.Application.Login.Sessions;
using Fenrir.Protocol.Login;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Handlers.Handlers;

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
            case LoginOutcome.DuplicateSessionEvicted:
                return;
            case LoginOutcome.Failure:
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
                        result.PinMask, userSort: result.AccountGrade),
                    LoginTrain.BuildAvatarSlots(result.Characters));
                return;
        }
    }
}
