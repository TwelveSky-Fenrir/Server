namespace Fenrir.Domain.Login.Avatars;

public static class TribeLineageGate
{
    // Server/ts25zone/S04_MyWork02.cpp:874-894 : la zone Quit() a l'entree sur un couple incoherent. Le
    // legacy ne le verifie PAS a la creation (S04_MyWork02.cpp:616-622), donc un couple hybride produisait un
    // personnage persiste puis definitivement inatteignable. Rendu serveur-autoritaire des la creation.
    public static bool IsCoherent(byte tribe, byte previousTribe)
    {
        return tribe switch
        {
            0 or 1 or 2 => previousTribe == tribe,
            FourthFactionGate.FourthFactionTribe => previousTribe is 0 or 1 or 2,
            _ => false
        };
    }

    public static bool BlocksCreation(byte tribe, byte previousTribe)
    {
        return !IsCoherent(tribe, previousTribe);
    }
}
