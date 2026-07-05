using Fenrir.Application.Login.Abstractions.CreateMousePin;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Login;

namespace Fenrir.Application.Login.Handlers.Handlers;

/// <summary>
///     op13 CL_CREATE_MOUSE_PASSWORD_SEND — first-time PIN creation; stored hashed (never in clear, unlike legacy),
///     then opens char select.
/// </summary>
public sealed class CreateMousePinHandler(ICreateMousePinService createMousePinService)
    : IAsyncPacketHandler<CreateMousePinRequest>
{
    public async ValueTask HandleAsync(CreateMousePinRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var loginSession = (LoginClientSession)session;

        // AllowedStates=[PinRequired] gates this past MarkAuthenticated, so AccountId is always set here.
        var accountId = loginSession.AccountId!.Value;

        var result = await createMousePinService.CreateMousePinAsync(accountId, packet.MousePassword,
            cancellationToken);

        switch (result.Outcome)
        {
            case CreateMousePinOutcome.InvalidFormat:
                loginSession.Abort(DisconnectReason.Malformed);
                return;
            case CreateMousePinOutcome.AlreadyExists:
                // Legacy: creating over an existing PIN is a protocol violation (client should send op15/op14 instead).
                loginSession.Abort(DisconnectReason.StateViolation);
                return;
            case CreateMousePinOutcome.StorageFailure:
                // Legacy: storage failure is a silent Quit(), no reply (S04_MyWork02.cpp l.476-479).
                loginSession.Abort(DisconnectReason.Faulted);
                return;
            default:
                loginSession.MarkCharSelect();
                session.Send(new CreateMousePinResponse { Result = 0, MousePassword = packet.MousePassword });
                return;
        }
    }
}
