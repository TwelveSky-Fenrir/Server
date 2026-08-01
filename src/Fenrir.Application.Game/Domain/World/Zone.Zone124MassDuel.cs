namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    // Server/ts25zone/S04_MyWork04.cpp:1962-1965 : le case 602 remet a plat les quatre champs mDuel_124_*
    // avant sa boucle, ce qui fait sortir Process_Zone_124_TYPE des sa premiere ligne (S07_MyGame01.cpp:4006).
    public void ResetZone124MassDuel()
    {
        _zone124MassDuel.Reset();
    }
}
