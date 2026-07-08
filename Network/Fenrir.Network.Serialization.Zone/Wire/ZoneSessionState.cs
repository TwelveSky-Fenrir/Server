namespace Fenrir.Network.Serialization.Zone.Wire;

/// <summary>
///     <see cref="Registering" /> and <see cref="InWorld" /> are BOTH "gameplay-ready" as far as the legacy
///     client is concerned -- Réf. C++ : Server/ts25zone/S04_MyWork01.cpp:274-350 (<c>MyWork::WorkerProcess</c>,
///     the per-opcode dispatch loop) sets <c>bCheckPlayInfo = TRUE</c> by default for every opcode except
///     <c>P_TEMP_REGISTER_SEND</c> (op11) and <c>P_REGISTER_AVATAR_SEND</c> (op12), and that flag's only gate is
///     <c>tUserInfo-&gt;mReadyState</c> -- which S04_MyWork02.cpp:994 sets TRUE at the END of the op12 handler,
///     unconditionally, before the client has had any chance to send op13 (<c>CLIENT_OK_FOR_ZONE_SEND</c>).
///     So op13/<see cref="InWorld" /> is NOT the legacy gameplay-readiness gate; every incoming packet whose
///     legacy counterpart isn't op11/op12 itself should accept both <see cref="Registering" /> and
///     <see cref="InWorld" />, matching the real client's own observed behavior of sending ordinary gameplay
///     packets before its own op13 round trip completes.
/// </summary>
public enum ZoneSessionState : byte
{
    Connected,
    TicketConsumed,
    Registering,
    InWorld
}
