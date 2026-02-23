using Fenrir.Network.Framing;

namespace Fenrir.GameServer.Metadata;

public abstract class Message : IMessage
{
    private const PacketType Type = PacketType.Unknown;

    public abstract void Deserialize(BinaryReader reader);

    public abstract void Serialize(BinaryWriter writer);

    public MessageMetadata ToMetadata()
    {
        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms))
        {
            Serialize(writer);
        }

        // TODO: Why does Message need to know about MessageMetadata? Use factory?
        // IMessageMetadataFactory factory = new MessageMetadataFactory();
        // factory.CreateMessageMetadata(message);
        return new MessageMetadata(
            // Should we have a Meta Length and a Packet Length?
            (int)ms.Length + 9, // Taille totale du message (inclus en-tête) // TODO: Move this to a better place? Better define what Length+9 means?
            0, // À définir selon la logique
            (byte)Type,
            ms.ToArray()
        );
    }
}