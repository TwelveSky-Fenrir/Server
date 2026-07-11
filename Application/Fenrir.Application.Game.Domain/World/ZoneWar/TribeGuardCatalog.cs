using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public readonly record struct GuardSlotCoordinate(float X, float Y, float Z, int ReservedSlotIndex);

public sealed record GuardPostDefinition(
    short MapId,
    byte TribeId,
    byte MonsterMainType,
    byte MonsterSpecialType,
    ImmutableArray<GuardSlotCoordinate> Slots,
    byte? RequiresTribeSymbolOwnedBy = null)
{

        public const int SlotsPerPost = 5;
}

public sealed class GuardPostCatalog(
    ImmutableArray<GuardPostDefinition> ordinaryPosts,
    ImmutableArray<GuardPostDefinition> zone038WinnerPosts)
{
    public static readonly GuardPostCatalog Empty = new([], []);

        public ImmutableArray<GuardPostDefinition> OrdinaryPosts { get; } = ordinaryPosts;

        public ImmutableArray<GuardPostDefinition> Zone038WinnerPosts { get; } = zone038WinnerPosts;
}
