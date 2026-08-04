namespace Fenrir.Application.Game.Domain.World.Loot;

public sealed class GroundItemFactory(IGroundItemSerialGenerator serialGenerator)
{
    private readonly IGroundItemSerialGenerator _serialGenerator =
        serialGenerator ?? throw new ArgumentNullException(nameof(serialGenerator));

    public GroundItemFactoryResult Create(in GroundItemDropRequest request)
    {
        var validation = Validate(request);
        if (validation != GroundItemFactoryOutcome.Success)
            return GroundItemFactoryResult.Reject(validation);

        var serialRequest = new GroundItemSerialGenerationRequest(request.Item, request.Origin);
        var serialNumber = _serialGenerator.Generate(in serialRequest);
        if (serialNumber <= 0)
            return GroundItemFactoryResult.Reject(GroundItemFactoryOutcome.InvalidGeneratedSerial);

        var replication = new GroundItemReplicationState(
            request.Replication.State,
            request.Replication.SocketGems,
            request.Replication.ExpireDate);

        var drop = new GroundItemDrop(request.Item, request.Quantity, serialNumber, request.Origin, replication);
        return new GroundItemFactoryResult(GroundItemFactoryOutcome.Success, drop);
    }

    private static GroundItemFactoryOutcome Validate(in GroundItemDropRequest request)
    {
        if (request.Item.ItemId <= 0)
            return GroundItemFactoryOutcome.InvalidItemId;

        if (request.Item.ItemType < 0)
            return GroundItemFactoryOutcome.InvalidItemType;

        if (request.Quantity <= 0)
            return GroundItemFactoryOutcome.InvalidQuantity;

        var sockets = request.Replication.SocketGems;
        if (sockets.First < 0 || sockets.Second < 0 || sockets.Third < 0)
            return GroundItemFactoryOutcome.InvalidSocketGems;

        if (request.Replication.ExpireDate < 0)
            return GroundItemFactoryOutcome.InvalidExpireDate;

        return !Enum.IsDefined(request.Origin)
            ? GroundItemFactoryOutcome.InvalidOrigin
            : GroundItemFactoryOutcome.Success;
    }
}

public readonly record struct GroundItemFactoryResult(
    GroundItemFactoryOutcome Outcome,
    GroundItemDrop? Drop)
{
    public bool Succeeded => Outcome == GroundItemFactoryOutcome.Success;

    internal static GroundItemFactoryResult Reject(GroundItemFactoryOutcome outcome)
    {
        return new GroundItemFactoryResult(outcome, null);
    }
}

public enum GroundItemFactoryOutcome
{
    Success,
    InvalidItemId,
    InvalidItemType,
    InvalidQuantity,
    InvalidSocketGems,
    InvalidExpireDate,
    InvalidOrigin,
    InvalidGeneratedSerial
}
