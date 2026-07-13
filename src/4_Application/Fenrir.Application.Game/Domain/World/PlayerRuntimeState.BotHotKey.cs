namespace Fenrir.Application.Game.Domain.World;

public partial class PlayerRuntimeState
{
    public int NoManaCount { get; set; }

    public int AutoHuntPaidDayBudget { get; set; }

    public int AutoHuntPaidMinuteBudget { get; set; }

    public int AutoHuntBudgetMinuteAccrualTicks { get; set; }
}
