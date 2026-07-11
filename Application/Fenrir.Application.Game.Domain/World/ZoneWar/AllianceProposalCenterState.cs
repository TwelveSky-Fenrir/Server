using Fenrir.Application.Game.Domain.World.WorldState;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public readonly record struct AlliancePossibleInfo(bool CooldownActive, int ExpiryDateYyyyMmDd)
{
    public static readonly AlliancePossibleInfo Cleared = new(false, 0);
}

public sealed class AllianceProposalCenterState
{

        public const int SlotCount = 2;

    private static readonly int TribeCount = WorldStateService.TribeCount;

    private readonly byte?[,] _allianceState = new byte?[SlotCount, 2];
    private readonly AlliancePossibleInfo[] _possibleAllianceInfo = new AlliancePossibleInfo[TribeCount];
    private readonly Lock _lock = new();

    public static bool IsValidSlot(int slot)
    {
        return slot is >= 0 and < SlotCount;
    }

    public static bool IsValidTribe(int tribeId)
    {
        return tribeId >= 0 && tribeId < TribeCount;
    }

        public (byte? CellA, byte? CellB) GetSlot(int slot)
    {
        ValidateSlot(slot);
        lock (_lock)
        {
            return (_allianceState[slot, 0], _allianceState[slot, 1]);
        }
    }

        public void SetSlot(int slot, byte? cellA, byte? cellB)
    {
        ValidateSlot(slot);
        lock (_lock)
        {
            _allianceState[slot, 0] = cellA;
            _allianceState[slot, 1] = cellB;
        }
    }

        public void ClearSlot(int slot)
    {
        SetSlot(slot, null, null);
    }

        public bool SlotIsEmpty(int slot)
    {
        ValidateSlot(slot);
        lock (_lock)
        {
            return SlotIsEmptyCore(slot);
        }
    }

        public bool SlotMatchesPairEitherOrder(int slot, byte tribeA, byte tribeB)
    {
        ValidateSlot(slot);
        lock (_lock)
        {
            return SlotMatchesPairEitherOrderCore(slot, tribeA, tribeB);
        }
    }

        public AlliancePossibleInfo GetPossibleAllianceInfo(byte tribeId)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            return _possibleAllianceInfo[tribeId];
        }
    }

        public void SetPossibleAllianceCooldown(byte tribeId, int expiryDateYyyyMmDd)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            _possibleAllianceInfo[tribeId] = new AlliancePossibleInfo(true, expiryDateYyyyMmDd);
        }
    }

        public void ClearPossibleAllianceCooldown(byte tribeId)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            _possibleAllianceInfo[tribeId] = AlliancePossibleInfo.Cleared;
        }
    }

        public void ApplyFinalize(byte tribeA, byte tribeB)
    {
        ValidateTribeId(tribeA);
        ValidateTribeId(tribeB);
        lock (_lock)
        {
            var slot = SlotIsEmptyCore(0) ? 0 : 1;
            _possibleAllianceInfo[tribeA] = AlliancePossibleInfo.Cleared;
            _possibleAllianceInfo[tribeB] = AlliancePossibleInfo.Cleared;
            _allianceState[slot, 0] = tribeA;
            _allianceState[slot, 1] = tribeB;
        }
    }

        public void ApplyBreak(byte tribeA, byte tribeB, int expiryDateA, int expiryDateB)
    {
        ValidateTribeId(tribeA);
        ValidateTribeId(tribeB);
        lock (_lock)
        {
            var slot = SlotMatchesPairEitherOrderCore(0, tribeA, tribeB) ? 0 : 1;
            _possibleAllianceInfo[tribeA] = new AlliancePossibleInfo(true, expiryDateA);
            _possibleAllianceInfo[tribeB] = new AlliancePossibleInfo(true, expiryDateB);
            _allianceState[slot, 0] = null;
            _allianceState[slot, 1] = null;
        }
    }

        private bool SlotIsEmptyCore(int slot)
    {
        return _allianceState[slot, 0] is null && _allianceState[slot, 1] is null;
    }

        private bool SlotMatchesPairEitherOrderCore(int slot, byte tribeA, byte tribeB)
    {
        var cellA = _allianceState[slot, 0];
        var cellB = _allianceState[slot, 1];
        return (cellA == tribeA && cellB == tribeB) || (cellA == tribeB && cellB == tribeA);
    }

    private static void ValidateSlot(int slot)
    {
        if (!IsValidSlot(slot))
            throw new ArgumentOutOfRangeException(nameof(slot), slot, $"Alliance proposal slot must be 0-{SlotCount - 1}.");
    }

    private static void ValidateTribeId(byte tribeId)
    {
        if (!IsValidTribe(tribeId))
            throw new ArgumentOutOfRangeException(nameof(tribeId), tribeId, $"TribeId must be 0-{TribeCount - 1}.");
    }
}
