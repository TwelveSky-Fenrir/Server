namespace Fenrir.Cluster.Link;

/// <summary>
/// Point d'application local du fan-out reçu du CenterServer sur un lien sortant (<see cref="ICenterLink"/>). Le
/// fan-out Center n'est <b>pas</b> du dispatch par-session (pas de handler↔session) : c'est un effet broadcast
/// vers toutes les zones locales du shard. Ce seam <b>dédié</b> évite la collision avec l'<c>IFrameDispatcher</c>
/// client (déjà capté par le dispatcher de zone/login) et le <c>MessageDispatcher</c> local qui ne connaît pas les
/// opcodes Center. L'impl vit côté consommateur (GameServer) et route vers l'ingesteur de broadcast de zone.
/// </summary>
public interface ICenterFanOutSink
{
    /// <summary>Applique une trame de fan-out Center (opcode + charge sans l'octet d'opcode), <b>synchrone</b>,
    /// appelée dans la boucle de lecture uplink avant tout <c>await</c> ; in-memory only, sans I/O bloquante.</summary>
    void Receive(byte opcode, ReadOnlySpan<byte> payload);
}
