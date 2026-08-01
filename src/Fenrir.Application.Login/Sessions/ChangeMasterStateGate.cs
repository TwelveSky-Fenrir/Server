using Fenrir.Core.Opcodes;
using Fenrir.Protocol.Login;

namespace Fenrir.Application.Login.Sessions;

public static class ChangeMasterStateGate
{
    public static bool Allows(LoginSessionState state)
    {
        return state is not (LoginSessionState.Connected or LoginSessionState.VersionOk);
    }

    public static bool AppliesTo(byte opcode)
    {
        return opcode == Opcodes.Login.Incoming.ChangeMaster;
    }
}
