namespace Fenrir.Application.Game.Abstractions.World;

public readonly record struct WorldEventPublicationIdentity(Guid OperationId, Guid CorrelationId)
{
    public bool IsValid => OperationId != Guid.Empty && CorrelationId != Guid.Empty;

    public static WorldEventPublicationIdentity Create()
    {
        return new WorldEventPublicationIdentity(Guid.NewGuid(), Guid.NewGuid());
    }
}

public readonly record struct WorldEventUplinkResult
{
    private WorldEventUplinkResult(WorldEventUplinkResultKind kind, WorldEventPublicationIdentity identity)
    {
        Kind = kind;
        Identity = identity;
    }

    public WorldEventUplinkResultKind Kind { get; }

    public WorldEventPublicationIdentity Identity { get; }

    public bool IsEnqueued => Kind == WorldEventUplinkResultKind.Enqueued;

    public static WorldEventUplinkResult Enqueued(WorldEventPublicationIdentity identity)
    {
        return new WorldEventUplinkResult(WorldEventUplinkResultKind.Enqueued, identity);
    }

    public static WorldEventUplinkResult Backpressured(WorldEventPublicationIdentity identity)
    {
        return new WorldEventUplinkResult(WorldEventUplinkResultKind.Backpressured, identity);
    }

    public static WorldEventUplinkResult Faulted(WorldEventPublicationIdentity identity)
    {
        return new WorldEventUplinkResult(WorldEventUplinkResultKind.Faulted, identity);
    }
}

public enum WorldEventUplinkResultKind : byte
{
    Faulted = 0,

    Enqueued,

    Backpressured
}
