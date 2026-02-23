using System.Diagnostics.CodeAnalysis;

namespace Fenrir.Network.Factory;

/// <summary>
///     A factory abstraction that creates instances of <typeparamref name="TMessage" /> based on its
///     <typeparamref name="TPacketType" />.
/// </summary>
/// <typeparam name="TPacketType">The type of the key.</typeparam>
/// <typeparam name="TMessage">The type of the message.</typeparam>
public interface IMessageFactory<in TPacketType, TMessage>
    where TPacketType : notnull
    where TMessage : class
{
	/// <summary>Attempts to get a <paramref name="message" /> based on its <paramref name="key" />.</summary>
	/// <param name="key">The key of the message.</param>
	/// <param name="message">The found message.</param>
	/// <returns><see langword="true" /> if the message was found; otherwise, <see langword="false" />.</returns>
	bool TryGetMessage(TPacketType key, [NotNullWhen(true)] out TMessage? message);
}