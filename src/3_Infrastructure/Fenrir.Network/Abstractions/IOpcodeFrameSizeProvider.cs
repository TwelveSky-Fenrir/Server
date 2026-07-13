namespace Fenrir.Network.Abstractions;

/// <summary>
/// Fournit la taille de trame attendue pour un opcode (analogue de <c>W_FUNCTION[256].SIZE</c>) — le framing
/// legacy n'ayant <b>pas</b> de length-prefix. Implémenté par l'<c>OpcodeRegistry</c> généré (côté Application).
/// </summary>
public interface IOpcodeFrameSizeProvider
{
    public bool TryGetFrameSize(byte opcode, out int frameSize);
}
