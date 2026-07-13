using Fenrir.Core.Packets.Shared;

namespace Fenrir.Application.Game.Domain.Progression;

public static class AutoHuntConfigValidator
{
    public enum Rejection
    {
        None,

        MalformedShape,

        NegativeSkillId,

        NegativeGrade,

        NegativeSelectorOrCount,

        FlagOutOfDomain
    }

    private const int BuffStoreLength = 16;

    private const int AttackTypeLength = 4;

    public static Result Validate(in AutoHunt config)
    {
        var buffStore = config.BuffStore;
        var attackType = config.AttackType;
        if (buffStore is null || buffStore.Length != BuffStoreLength ||
            attackType is null || attackType.Length != AttackTypeLength)
            return new Result(false, Rejection.MalformedShape);

        for (var i = 0; i < BuffStoreLength; i += 2)
        {
            if (buffStore[i] < 0)
                return new Result(false, Rejection.NegativeSkillId);
            if (buffStore[i + 1] < 0)
                return new Result(false, Rejection.NegativeGrade);
        }

        for (var i = 0; i < AttackTypeLength; i += 2)
        {
            if (attackType[i] < 0)
                return new Result(false, Rejection.NegativeSkillId);
            if (attackType[i + 1] < 0)
                return new Result(false, Rejection.NegativeGrade);
        }

        if (config.BuffType < 0 || config.HuntType < 0 || config.ItemType < 0 || config.MonNum < 0)
            return new Result(false, Rejection.NegativeSelectorOrCount);

        if (!IsFlag(config.InvenCmd) || !IsFlag(config.DeathCmd) || !IsFlag(config.AnimalPreyCmd) ||
            !IsFlag(config.AnimalFoodCmd))
            return new Result(false, Rejection.FlagOutOfDomain);

        return new Result(true, Rejection.None);
    }

    private static bool IsFlag(int value)
    {
        return value is 0 or 1;
    }

    public readonly record struct Result(bool IsValid, Rejection Rejection);
}
