namespace Fenrir.Network.Abstractions;

public interface IOutgoingPacket : IFenrirPacket
{

        public static abstract WireObfuscationMode Obfuscation { get; }

        public static abstract bool Compressed { get; }

    public int Write(Span<byte> destination);
}
