namespace Fenrir.Network.Abstractions;

public interface IOpcodeFrameSizeProvider
{
    public bool TryGetFrameSize(byte opcode, out int frameSize);
}
