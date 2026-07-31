using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Inventory;

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

    public void ReplaceContainer(byte container, ImmutableDictionary<byte, ItemStack> slots)
    {
        _containers[container] = slots;
    }

    public void Seed(IReadOnlyList<CharacterItemSlotDto> rows)
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
