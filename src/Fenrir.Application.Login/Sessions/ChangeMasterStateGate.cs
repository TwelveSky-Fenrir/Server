using Fenrir.Core.Opcodes;
using Fenrir.Protocol.Login;

namespace Fenrir.Application.Login.Sessions;

// Server/ts25login/S04_MyWork02.cpp:1643-1646 : W_CHANGE_MASTER_SEND a un corps VIDE et ne teste ni
// mCheckValidState, ni IsSecondLogin, ni IsMovingZone -- seul handler entrant de ts25login dans ce cas.
// La regle metier portee est "sans effet et sans reponse" ; le puits joignable avant authentification ne
// l'est pas : op24 devient un rejet gate par l'etat de session.
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
