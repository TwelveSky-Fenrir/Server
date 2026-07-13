namespace Fenrir.Application.Game.Domain.Skills;

public static class AutoBuffActivationResolver
{
    public enum ResultKind
    {
        NoReply,

        Disconnect,
        Activate,
        Tick
    }

    public const int ChannelingActionSort = 41;

    public static int ManaAfterActivation(int mana)
    {
        return mana - (int)(mana * 0.9f);
    }

    public static Result Resolve(int sort, in Context ctx, int today)
    {
        switch (sort)
        {
            case 1:
                if (ctx.AutoBuffTime < today || ctx.ActionSort != 1)
                    return new Result(ResultKind.NoReply);

                var reducedMana = (int)(ctx.Mana * 0.9f);
                return ctx.Mana < reducedMana
                    ? new Result(ResultKind.Disconnect)
                    : new Result(ResultKind.Activate, ManaAfterActivation(ctx.Mana));

            case 2:
                return new Result(ResultKind.Tick);

            default:
                return new Result(ResultKind.Disconnect);
        }
    }

    public readonly record struct Result(ResultKind Kind, int ManaAfterActivation = 0);

    public readonly record struct Context(int AutoBuffTime, int ActionSort, int Mana);
}
