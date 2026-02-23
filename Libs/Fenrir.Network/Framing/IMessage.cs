namespace Fenrir.Network.Framing;

public interface IMessage
{
    // TODO: A way to get type constant/static?

    public void Deserialize(BinaryReader reader);
    public void Serialize(BinaryWriter writer);
    public IMessageMetadata ToMetadata();
}
