namespace Fenrir.Application.Game.Domain.World.Loot;

public interface IGroundItemSerialGenerator
{
    public int Generate(in GroundItemSerialGenerationRequest request);
}

public readonly record struct GroundItemSerialGenerationRequest(
    GroundItemReference Item,
    GroundItemOrigin Origin);
