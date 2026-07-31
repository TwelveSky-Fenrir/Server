namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public static class SiegeEventStateMap
{
    public static bool TryMapZone175(int eventCode, out int state)
    {
        state = eventCode switch
        {
            64 => 1,
            65 => 2,
            66 => 3,
            67 => 4,
            68 => 5,
            71 => 6,
            73 => 7,
            74 => 8,
            75 => 9,
            78 => 10,
            80 => 11,
            81 => 12,
            82 => 13,
            85 => 14,
            87 => 15,
            88 => 16,
            89 => 17,
            92 => 18,
            94 => 19,
            95 => 20,
            96 => 21,
            99 => 22,
            69 or 70 or 72 or 76 or 77 or 79 or 83 or 84 or 86 or 90 or 91 or 93 or 97 or 98 or 100 => 23,
            _ => -1
        };

        return state >= 0;
    }

    public static bool TryMapZone267(int eventCode, out int state)
    {
        state = eventCode switch
        {
            403 => 1,
            404 => 2,
            405 => 3,
            406 => 5,
            407 => 5,
            408 => 4,
            409 => 5,
            410 => 0,
            _ => -1
        };

        return state >= 0;
    }

    // 201 est le compte a rebours et n'ecrit aucun etat (Server/ts25center/S04_MyWork02.cpp:829) : la plage
    // commence donc a 202.
    public static bool TryMapZone194(int eventCode, out int state)
    {
        state = eventCode switch
        {
            202 => 1,
            203 => 2,
            204 => 3,
            205 => 5,
            206 => 4,
            207 => 5,
            208 => 0,
            _ => -1
        };

        return state >= 0;
    }

    public static bool TryMapZone335(int eventCode, out int state)
    {
        state = eventCode switch
        {
            1502 => 1,
            1503 => 2,
            1504 => 3,
            1505 => 4,
            1506 => 5,
            1507 => 0,
            _ => -1
        };

        return state >= 0;
    }

    public static bool TryMapZone241(int eventCode, out DenOfRebirthChallengeState state)
    {
        switch (eventCode)
        {
            case 411:
                state = DenOfRebirthChallengeState.ChallengeStarted;
                return true;
            case 412:
            case 413:
            case 414:
                state = DenOfRebirthChallengeState.Ended;
                return true;
            case 415:
                state = DenOfRebirthChallengeState.Idle;
                return true;
            default:
                state = DenOfRebirthChallengeState.Idle;
                return false;
        }
    }
}
