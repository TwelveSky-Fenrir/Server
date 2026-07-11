namespace Fenrir.Application.Game.Domain.AntiCheat;

public static class TribeConsistencyValidator
{

        public static bool IsConsistent(int tribe, int previousTribe)
    {
        return tribe switch
        {
            0 or 1 or 2 => previousTribe == tribe,
            3 => previousTribe is 0 or 1 or 2,
            _ => false
        };
    }
}
