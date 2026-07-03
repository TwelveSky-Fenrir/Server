using System.Collections.Concurrent;
using System.Collections.Immutable;
using Fenrir.Data.Characters;

namespace Fenrir.Application.Game.Inventory;

/// <summary>
///     A player's in-memory item containers while <c>InWorld</c> -- the inventory/equipment twin of
///     <see cref="Fenrir.Application.Game.World.PlayerRuntimeState" /> (referenced from it, one instance per
///     player, architecture reference §10.1: "un seul écrivain"). Mutated ONLY by
///     <see cref="Fenrir.Application.Game.World.Zone" />'s own tick (<c>Zone.HandleEnter</c> at world entry,
///     <c>Zone.ApplyInventoryCommand</c> for every later container move/equip/unequip) -- exactly the same
///     single-writer posture as every other field on
///     <see cref="Fenrir.Application.Game.World.PlayerRuntimeState" />. Handlers only ever READ this (e.g. to
///     validate a move against the CURRENT contents before posting a command) -- the same benign,
///     already-documented read-race <see cref="Fenrir.Application.Game.World.PlayerRuntimeState" />'s own
///     position fields already accept from <c>ZoneMoveHandler</c>.
/// </summary>
/// <remarks>
///     Backed by <see cref="ConcurrentDictionary{TKey,TValue}" /> of <see cref="ImmutableDictionary{TKey,TValue}" />
///     snapshots (never a plain <c>Dictionary</c> mutated slot-by-slot): each container's ENTIRE content is
///     swapped atomically by <see cref="ReplaceContainer" />, so a concurrent reader on another thread always
///     observes either the old or the new snapshot in full, NEVER a half-written container -- the same
///     lock-free "safe concurrent reads, single tick-thread writer" contract <c>Zone._players</c> documents
///     for itself.
/// </remarks>
public sealed class InventoryState
{
    private readonly ConcurrentDictionary<byte, ImmutableDictionary<byte, ItemStack>> _containers = new();

    /// <summary>The full current content of <paramref name="container" />, or empty if nothing occupies it (never null).</summary>
    public ImmutableDictionary<byte, ItemStack> GetContainer(byte container)
    {
        return _containers.TryGetValue(container, out var slots) ? slots : ImmutableDictionary<byte, ItemStack>.Empty;
    }

    /// <summary>Convenience single-slot read -- null when the slot is empty or the container has never been touched.</summary>
    public ItemStack? GetSlot(byte container, byte slot)
    {
        return GetContainer(container).TryGetValue(slot, out var stack) ? stack : null;
    }

    /// <summary>
    ///     Wholesale container replace -- mirrors <c>usp_CharacterItems_ReplaceContainer</c>'s own "whole
    ///     container" semantics so the in-memory cache and the DB never structurally disagree about what a
    ///     "container" means. Internal: the ONLY legitimate callers are
    ///     <see cref="Fenrir.Application.Game.World.Zone" />'s own tick (world-entry seeding via
    ///     <see cref="Seed" />, and <c>Zone.ApplyInventoryCommand</c> for every later change) -- see this
    ///     type's class remarks on the single-writer invariant.
    /// </summary>
    internal void ReplaceContainer(byte container, ImmutableDictionary<byte, ItemStack> slots)
    {
        _containers[container] = slots;
    }

    /// <summary>
    ///     World-entry seeding (<c>Zone.HandleEnter</c>): groups the flat RS1 item rows
    ///     (<see cref="CharacterWorldEntryBundle.Items" />) back into their per-container snapshots. Containers
    ///     absent from <paramref name="rows" /> (a character with nothing in, say, StorePage1) are simply never
    ///     touched -- <see cref="GetContainer" /> already treats "never touched" as empty.
    /// </summary>
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
