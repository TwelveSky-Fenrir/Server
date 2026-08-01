namespace Fenrir.Application.Login.Sessions;

public readonly record struct GiftSlotEntry(int GiftId, int ProductId)
{
    public static GiftSlotEntry Empty { get; } = new(0, 0);

    public bool IsOccupied => GiftId != 0;
}

// Server/ts25login/S08_MyDB.cpp:939-974 : uGiftInfo est un tableau de 10 emplacements A POSITION FIXE,
// lu d'une colonne texte de 50 caracteres decoupee en 10 tranches de 5 chiffres. Un rang derive d'un tri
// se redensifie a chaque consommation et fait glisser tous les index superieurs.
public sealed class GiftSlotBoard
{
    public const int SlotCount = 10;

    private readonly GiftSlotEntry[] _slots = new GiftSlotEntry[SlotCount];

    public bool IsLoaded { get; private set; }

    public void Refresh(IReadOnlyCollection<PendingGiftDto> pending)
    {
        var ordered = pending
            .OrderBy(g => g.CreatedAtUtc)
            .ThenBy(g => g.GiftId)
            .ToArray();

        var stillPending = ordered.Select(g => g.GiftId).ToHashSet();

        for (var slot = 0; slot < SlotCount; slot++)
            if (_slots[slot].IsOccupied && !stillPending.Contains(_slots[slot].GiftId))
                _slots[slot] = GiftSlotEntry.Empty;

        var alreadyPlaced = _slots.Where(s => s.IsOccupied).Select(s => s.GiftId).ToHashSet();

        var nextFreeSlot = 0;
        foreach (var gift in ordered)
        {
            if (alreadyPlaced.Contains(gift.GiftId))
                continue;

            while (nextFreeSlot < SlotCount && _slots[nextFreeSlot].IsOccupied)
                nextFreeSlot++;

            if (nextFreeSlot == SlotCount)
                break;

            _slots[nextFreeSlot] = new GiftSlotEntry(gift.GiftId, gift.ProductId ?? 0);
        }

        IsLoaded = true;
    }

    public bool TryTake(int slot, out GiftSlotEntry entry)
    {
        if (slot < 0 || slot >= SlotCount)
        {
            entry = GiftSlotEntry.Empty;
            return false;
        }

        entry = _slots[slot];
        if (!entry.IsOccupied)
            return false;

        _slots[slot] = GiftSlotEntry.Empty;
        return true;
    }

    public void Restore(int slot, GiftSlotEntry entry)
    {
        if (slot < 0 || slot >= SlotCount)
            return;

        _slots[slot] = entry;
    }

    public int[] ToWireArray()
    {
        var giftItem = new int[SlotCount * 2];

        for (var slot = 0; slot < SlotCount; slot++)
            giftItem[slot * 2] = _slots[slot].ProductId;

        return giftItem;
    }

    public int OccupiedCount()
    {
        var count = 0;

        foreach (var slot in _slots)
            if (slot.IsOccupied)
                count++;

        return count;
    }
}
