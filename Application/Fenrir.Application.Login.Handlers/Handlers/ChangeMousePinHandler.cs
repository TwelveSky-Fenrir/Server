using Fenrir.Application.Login.Abstractions.ChangeMousePin;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Login;

namespace Fenrir.Application.Login.Handlers.Handlers;

/// <summary>
///     op14 CL_CHANGE_MOUSE_PASSWORD_SEND — legacy quirk: a successful change also validates the PIN
///     (S04_MyWork02.cpp l.532), so the session goes straight to CharSelect.
/// </summary>
public sealed class ChangeMousePinHandler(IChangeMousePinService changeMousePinService)
    : IAsyncPacketHandler<ChangeMousePinRequest>
{
    private const int MaxPinFailures = 3;

    /// <summary>Legacy <c>c0000</c> — the mask echoed on every non-success reply of ops 13/14.</summary>
    private const string ZeroPin = "0000";

    public async ValueTask HandleAsync(ChangeMousePinRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var loginSession = (LoginClientSession)session;
        var accountId = loginSession.AccountId!.Value;

        var result = await changeMousePinService.ChangeMousePinAsync(accountId, packet.MousePassword,
            packet.ChangeMousePassword, cancellationToken);

        switch (result.Outcome)
        {
            case ChangeMousePinOutcome.NoPinConfigured:
                // No stored PIN => Quit; client must create one first (op13).
                loginSession.Abort(DisconnectReason.StateViolation);
                return;
            case ChangeMousePinOutcome.InvalidFormat:
                loginSession.Abort(DisconnectReason.Malformed);
                return;
            case ChangeMousePinOutcome.WrongPassword:
                session.Send(new ChangeMousePinResponse { Result = 1, MousePassword = ZeroPin });
                if (loginSession.RegisterPinFailure() >= MaxPinFailures)
                    loginSession.Abort(DisconnectReason.StateViolation);
                return;
            case ChangeMousePinOutcome.StorageFailure:
                // Legacy: storage failure replies 2 without disconnecting (S04_MyWork02.cpp l.525-530).
                session.Send(new ChangeMousePinResponse { Result = 2, MousePassword = ZeroPin });
                return;
            default:
                loginSession.MarkCharSelect();
                session.Send(new ChangeMousePinResponse { Result = 0, MousePassword = packet.ChangeMousePassword });
                return;
        }
    }
}
