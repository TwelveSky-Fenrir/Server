namespace Fenrir.Core.Abstractions;

public interface IOpcodeFrameSizeProvider
{
    public bool TryGetFrameSize(byte opcode, out int frameSize);
}
