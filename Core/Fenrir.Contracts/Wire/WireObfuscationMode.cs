namespace Fenrir.Contracts.Wire;

/// <summary>
///     Obfuscation applied to the WHOLE packet (post-Write / pre-TryRead), beyond normal
///     field-by-field serialization. §3.
/// </summary>
public enum WireObfuscationMode : byte
{
    /// <summary>No global pass (packet is sent/received exactly as serialized).</summary>
    None,

    /// <summary>
    ///     <c>XOR_PACKET(s, sizeof(*s))</c> over the whole buffer (header included), degenerate keystream {0x10, 0xFE,
    ///     0xFE, ...}, last byte untouched. §3.1.
    /// </summary>
    XorPacketGlobal,

    /// <summary>
    ///     Field-by-field obfuscation (<c>scopyAvtXor*</c>), opcode stays in clear. Unique to <c>LC_USER_AVATAR_RECV2</c>
    ///     . §3.2.
    /// </summary>
    XorFieldAvatar
}
