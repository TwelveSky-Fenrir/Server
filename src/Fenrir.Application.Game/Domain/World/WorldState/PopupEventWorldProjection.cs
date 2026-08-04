using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Core.Packets.Shared;

namespace Fenrir.Application.Game.Domain.World.WorldState;

public static class PopupEventWorldProjection
{
    public static WorldInfo Apply(WorldInfo template, PopupEventState popupState)
    {
        var typeCount = Enum.GetValues<PopupEventType>().Length;
        var typeState = new int[typeCount];
        var pvpRequirements = new int[typeCount];
        var pvmRequirements = new int[typeCount];

        foreach (var type in Enum.GetValues<PopupEventType>())
        {
            var snapshot = popupState.GetSnapshot(type);
            var index = (int)type;
            typeState[index] = snapshot.Enabled ? 1 : 0;
            pvpRequirements[index] = snapshot.PvpRequirement;
            pvmRequirements[index] = snapshot.PvmRequirement;
        }

        return template with
        {
            PopUpTypeState = typeState,
            PopUpKillAvt = pvpRequirements,
            PopUpKillMonster = pvmRequirements
        };
    }
}
