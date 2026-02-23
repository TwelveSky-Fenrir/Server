using System.Buffers;

namespace Fenrir.Network.Framing;

public interface IMessageDecoder
{
	// /// <summary>Decodes all message from the given <paramref name="sequence" />.</summary>
	// /// <param name="sequence">The sequence buffer.</param>
	// /// <param name="isReadByServer">Whether the message is read by a server session.</param>
	// /// <returns>A collection of network message of type <typeparamref name="TMessage" />.</returns>
	// IEnumerable<TMessage> DecodeMessages(ReadOnlySequence<byte> sequence, bool isReadByServer);

	IPacket TryReadPacket(ReadOnlySpan<byte> buffer);
}