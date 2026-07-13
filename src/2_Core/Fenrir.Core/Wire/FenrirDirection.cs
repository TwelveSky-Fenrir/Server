namespace Fenrir.Core.Wire;

/// <summary>
/// Sens d'une trame — détermine la forme d'en-tête (9 o client→serveur, 1 o serveur→client) et si le paquet
/// est soumis au <c>SessionStateGate</c> (seul <see cref="Incoming"/> est gardé).
/// </summary>
public enum FenrirDirection : byte
{
    Incoming = 0,
    Outgoing = 1
}
