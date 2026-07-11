using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Domain.World.WorldState;

public static class WorldInfoBootReset
{

        private const int PossibleAllianceValuesPerTribe = 2;

        private const int AllianceSlotCount = 2;

    private const int TribeIdsPerAllianceSlot = 2;

    private static readonly int TribeCount = WorldStateService.TribeCount;

        private static readonly int PopupTypeCount = Enum.GetValues<PopupEventType>().Length;

        public static readonly WorldInfo ZeroedTemplate = Apply(WorldStateTemplates.ZeroedWorldInfo);

        public static WorldInfo Apply(WorldInfo template, int? allianceEmptySlotSentinel = null)
    {
        var result = template with
        {
            TribeVoteState = new int[TribeCount],

            CloseVoteState = new int[TribeCount],

            PossibleAllianceInfo = new int[TribeCount * PossibleAllianceValuesPerTribe],

            Zone038DTMValue = new int[TribeCount],

            TribeNokSanStone = new int[TribeCount],
            NokSanStoneState = new int[Zone195NokSanState.StoneSlotCount],

            PopUpTypeState = new int[PopupTypeCount],
            PopUpKillAvt = new int[PopupTypeCount],
            PopUpKillMonster = new int[PopupTypeCount]
        };

        if (allianceEmptySlotSentinel is { } sentinel)
        {
            var allianceState = new int[AllianceSlotCount * TribeIdsPerAllianceSlot];
            Array.Fill(allianceState, sentinel);
            result = result with { AllianceState = allianceState };
        }

        return result;
    }
}
