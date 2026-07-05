namespace Fenrir.Contracts.Wire;

/// <summary>Legacy owner of the opcode's meaning — opcode values overlap across servers.</summary>
public enum FenrirServer : byte
{
    Login,
    Zone
}
