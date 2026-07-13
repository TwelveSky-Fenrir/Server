namespace Fenrir.Core.Wire;

/// <summary>
/// Tailles d'en-tête legacy byte-exactes : <b>9 o</b> client→serveur (<c>int/int/byte opcode</c> à l'offset 8),
/// <b>1 o</b> serveur→client. <b>Pas de length-prefix</b> — la taille de trame vient du registre d'opcodes généré
/// (analogue de <c>W_FUNCTION[256]{PROC,SIZE}</c>).
/// </summary>
public static class WireHeaderSizes
{
    public const int ClientPacketSize = 9;

    public const int DefaultPacketSize = 1;

    public static int SizeFor(FenrirDirection direction)
    {
        return direction switch
        {
            FenrirDirection.Incoming => ClientPacketSize,
            FenrirDirection.Outgoing => DefaultPacketSize,
            _ => throw new ArgumentOutOfRangeException(nameof(direction))
        };
    }
}
