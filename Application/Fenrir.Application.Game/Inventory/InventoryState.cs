using System.Collections.Concurrent;
using System.Collections.Immutable;
using Fenrir.Data.Characters;

namespace Fenrir.Application.Game.Inventory;

/// <summary>
///     Per-player in-memory item containers, mutated only by Zone's own tick (single writer). Each container
///     is swapped atomically as a whole ImmutableDictionary snapshot, so a concurrent reader never observes a
///     half-written container.
/// </summary>
public sealed class InventoryState
{
    private readonly ConcurrentDictionary<byte, ImmutableDictionary<byte, ItemStack>> _containers = new();

    public ImmutableDictionary<byte, ItemStack> GetContainer(byte container)
    {
        return _containers.TryGetValue(container, out var slots) ? slots : ImmutableDictionary<byte, ItemStack>.Empty;
    }

    public ItemStack? GetSlot(byte container, byte slot)
    {
        return GetContainer(container).TryGetValue(slot, out var stack) ? stack : null;
    }

    /// <summary>Whole-container replace, mirroring usp_CharacterItems_ReplaceContainer's own semantics.</summary>
    internal void ReplaceContainer(byte container, ImmutableDictionary<byte, ItemStack> slots)
    {
        _containers[container] = slots;
    }

    /// <summary>World-entry seeding: groups the flat RS1 item rows back into per-container snapshots.</summary>
    internal void Seed(IReadOnlyList<CharacterItemSlotDto> rows)
    {
        var builders = new Dictionary<byte, ImmutableDictionary<byte, ItemStack>.Builder>();

        foreach (var row in rows)
        {
            if (!builders.TryGetValue(row.Container, out var builder))
                builders[row.Container] = builder = ImmutableDictionary.CreateBuilder<byte, ItemStack>();

            builder[row.Slot] = ItemStack.FromRow(row);
        }

        foreach (var (container, builder) in builders)
            _containers[container] = builder.ToImmutable();
    }
}
