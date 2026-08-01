using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.World;

public partial class PlayerRuntimeState
{
    public ImmutableDictionary<byte, int> PetBag { get; set; } = ImmutableDictionary<byte, int>.Empty;

    public int PetBagDate { get; set; }

    public int? GetPetBagSlot(byte slot)
    {
        return PetBag.TryGetValue(slot, out var itemId) ? itemId : null;
    }

    public void SetPetBagSlot(byte slot, int? itemId)
    {
        PetBag = itemId is { } id ? PetBag.SetItem(slot, id) : PetBag.Remove(slot);
    }
}
