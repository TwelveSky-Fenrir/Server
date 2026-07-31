using Fenrir.Application.Game.Domain.World.WorldState;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public enum DenOfRebirthChallengeState : byte
{
    Idle = 0,
    ChallengeStarted = 1,
    Ended = 2
}

public sealed class ZoneCenterSiegeState
{
    public const int Zone175Instances = 4;

    public const int Zone175Slots = 8;

    public const int Zone049Slots = 13;

    public const int Zone241Instances = 20;

    private static readonly int TribeCount = WorldStateService.TribeCount;
    private readonly float[] _experienceBonusRatio = new float[TribeCount];
    private readonly float[] _itemDropBonusRatio = new float[TribeCount];

    private readonly int[] _killOtherTribeBonus = new int[TribeCount];
    private readonly Lock _lock = new();
    private readonly float[] _myoungItemDropBonusRatio = new float[TribeCount];
    private readonly int[] _zone038DtmValue = new int[TribeCount];
    private readonly int[] _zone049State = new int[Zone049Slots];
    private readonly int[] _zone049StateTime = new int[Zone049Slots];
    private readonly int[,] _zone175 = new int[Zone175Instances, Zone175Slots];
    private readonly DenOfRebirthChallengeState[] _zone241 = new DenOfRebirthChallengeState[Zone241Instances];
    private readonly int[] _zone267 = new int[TribeCount];
    private int _zone335;


    public int Zone335
    {
        get
        {
            lock (_lock)
            {
                return _zone335;
            }
        }
    }

    public static bool IsValidZone175Cell(int instance, int slot)
    {
        return instance is >= 0 and < Zone175Instances && slot is >= 0 and < Zone175Slots;
    }

    public static bool IsValidTribe(int tribeId)
    {
        return tribeId is >= 0 && tribeId < TribeCount;
    }

    public static bool IsValidZone241Instance(int instance)
    {
        return instance is >= 0 and < Zone241Instances;
    }

    public static bool IsValidZone049Slot(int slot)
    {
        return slot is >= 0 and < Zone049Slots;
    }


    public int GetZone049State(int slot)
    {
        ValidateZone049Slot(slot);
        lock (_lock)
        {
            return _zone049State[slot];
        }
    }

    public int GetZone049StateTime(int slot)
    {
        ValidateZone049Slot(slot);
        lock (_lock)
        {
            return _zone049StateTime[slot];
        }
    }

    public void SetZone049State(int slot, int state, bool stampTime)
    {
        ValidateZone049Slot(slot);
        lock (_lock)
        {
            _zone049State[slot] = state;
            if (stampTime)
                _zone049StateTime[slot] = NowAsLegacyHhMm();
        }
    }


    public int GetZone175(int instance, int slot)
    {
        ValidateZone175Cell(instance, slot);
        lock (_lock)
        {
            return _zone175[instance, slot];
        }
    }

    public void SetZone175(int instance, int slot, int stateCode)
    {
        ValidateZone175Cell(instance, slot);
        lock (_lock)
        {
            _zone175[instance, slot] = stateCode;
        }
    }

    public void ResetZone175(int instance, int slot)
    {
        ValidateZone175Cell(instance, slot);
        lock (_lock)
        {
            _zone175[instance, slot] = 0;
        }
    }


    public int GetZone267(byte tribeId)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            return _zone267[tribeId];
        }
    }

    public void SetZone267(byte tribeId, int stateCode)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            _zone267[tribeId] = stateCode;
        }
    }

    public void ResetZone267(byte tribeId)
    {
        SetZone267(tribeId, 0);
    }


    public DenOfRebirthChallengeState GetZone241(int instance)
    {
        ValidateZone241Instance(instance);
        lock (_lock)
        {
            return _zone241[instance];
        }
    }

    public void SetZone241(int instance, DenOfRebirthChallengeState state)
    {
        ValidateZone241Instance(instance);
        lock (_lock)
        {
            _zone241[instance] = state;
        }
    }

    public void ResetZone241(int instance)
    {
        SetZone241(instance, DenOfRebirthChallengeState.Idle);
    }

    public void SetZone335(int phaseCode)
    {
        lock (_lock)
        {
            _zone335 = phaseCode;
        }
    }

    public void ResetZone335()
    {
        SetZone335(0);
    }

    public void ResetTribeBonusFields()
    {
        lock (_lock)
        {
            Array.Clear(_experienceBonusRatio);
            Array.Clear(_itemDropBonusRatio);
            Array.Clear(_myoungItemDropBonusRatio);
            Array.Clear(_killOtherTribeBonus);
        }
    }


    public int GetZone038DtmValue(byte tribeId)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            return _zone038DtmValue[tribeId];
        }
    }

    public void SetZone038DtmValue(byte tribeId, int value)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            _zone038DtmValue[tribeId] = value;
        }
    }


    public float GetExperienceBonusRatio(byte tribeId)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            return _experienceBonusRatio[tribeId];
        }
    }

    public void SetExperienceBonusRatio(byte tribeId, float value)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            _experienceBonusRatio[tribeId] = value;
        }
    }

    public float GetItemDropBonusRatio(byte tribeId)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            return _itemDropBonusRatio[tribeId];
        }
    }

    public void SetItemDropBonusRatio(byte tribeId, float value)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            _itemDropBonusRatio[tribeId] = value;
        }
    }

    public float GetMyoungItemDropBonusRatio(byte tribeId)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            return _myoungItemDropBonusRatio[tribeId];
        }
    }

    public void SetMyoungItemDropBonusRatio(byte tribeId, float value)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            _myoungItemDropBonusRatio[tribeId] = value;
        }
    }

    public int GetKillOtherTribeBonus(byte tribeId)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            return _killOtherTribeBonus[tribeId];
        }
    }

    public void SetKillOtherTribeBonus(byte tribeId, int value)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            for (byte i = 0; i < TribeCount; i++)
                _killOtherTribeBonus[i] = i == tribeId ? value : 0;
        }
    }

    private static void ValidateZone175Cell(int instance, int slot)
    {
        if (!IsValidZone175Cell(instance, slot))
            throw new ArgumentOutOfRangeException(nameof(instance),
                $"Zone175 instance must be 0-{Zone175Instances - 1} and slot 0-{Zone175Slots - 1}; got instance={instance}, slot={slot}.");
    }

    private static void ValidateZone241Instance(int instance)
    {
        if (!IsValidZone241Instance(instance))
            throw new ArgumentOutOfRangeException(nameof(instance), instance,
                $"Zone241 instance must be 0-{Zone241Instances - 1}.");
    }

    private static void ValidateTribeId(byte tribeId)
    {
        if (!IsValidTribe(tribeId))
            throw new ArgumentOutOfRangeException(nameof(tribeId), tribeId, $"TribeId must be 0-{TribeCount - 1}.");
    }

    private static void ValidateZone049Slot(int slot)
    {
        if (!IsValidZone049Slot(slot))
            throw new ArgumentOutOfRangeException(nameof(slot), slot, $"Zone049 slot must be 0-{Zone049Slots - 1}.");
    }

    private static int NowAsLegacyHhMm()
    {
        var now = DateTime.UtcNow;
        return now.Hour * 100 + now.Minute;
    }
}
