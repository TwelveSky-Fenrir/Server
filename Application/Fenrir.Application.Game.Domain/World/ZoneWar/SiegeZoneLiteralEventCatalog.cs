using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public static class SiegeZoneLiteralEventCatalog
{

        public const int Zone267NoOpEventCode = 402;

        public const int Zone267WritesState1EventCode = 403;

        public const int Zone267WritesState2EventCode = 404;

        public const int Zone267WritesState3EventCode = 405;

        public const int Zone267WritesState5EventCodeA = 406;

        public const int Zone267WritesState5EventCodeB = 407;

        public const int Zone267WritesState4EventCode = 408;

        public const int Zone267WritesState5EventCodeC = 409;

        public const int Zone267ResetEventCode = 410;


        public const int Zone241ChallengeStartedEventCode = 411;

        public const int Zone241FailureEventCodeA = 412;

        public const int Zone241FailureEventCodeB = 413;

        public const int Zone241SuccessEventCode = 414;

        public const int Zone241ReturnTownEventCode = 415;


        public const int WarHasStartedEventCode = 416;


        public const int InstinctDefenseFormationInUseEventCode = 418;

        public const int ReturnToOriginalFactionEventCode = 419;

        public const int RemainToFolBeginEventCode = 420;

        public const int FolBeganEventCode = 421;

        public const int FolSuccessEventCode = 422;

        public const int UnlabeledEventCode423 = 423;

        public const int FolSuccessDuplicateLabelEventCode = 424;

        public const int RemainToFolAnnihilationEventCode = 425;

        public const int FolAnnihilationBeganEventCode = 426;

        public const int FolAnnihilationSucceedEventCode = 427;

        public const int FolAnnihilationFailedEventCode = 428;

        public const int AllFactionsAllianceRevokedEventCode = 500;

        public static readonly FrozenSet<int> PureRelayEventCodes = new[]
    {
        WarHasStartedEventCode, InstinctDefenseFormationInUseEventCode, ReturnToOriginalFactionEventCode,
        RemainToFolBeginEventCode, FolBeganEventCode, FolSuccessEventCode, UnlabeledEventCode423,
        FolSuccessDuplicateLabelEventCode, RemainToFolAnnihilationEventCode, FolAnnihilationBeganEventCode,
        FolAnnihilationSucceedEventCode, FolAnnihilationFailedEventCode, AllFactionsAllianceRevokedEventCode
    }.ToFrozenSet();

        public static readonly FrozenSet<int> KnownGapEventCodes = new[] { 417 }.ToFrozenSet();

        public static bool TryGetLegacyLabel(int eventCode, out string label)
    {
        switch (eventCode)
        {
            case Zone241ChallengeStartedEventCode:
                label = "Den of Rebirth Challenge 0 - 11 // 0 // Name";
                return true;
            case Zone241FailureEventCodeA:
            case Zone241FailureEventCodeB:
                label = "Den of Rebirth Failure 0 - 11 // 0 // Name";
                return true;
            case Zone241SuccessEventCode:
                label = "Den of Rebirth Success 0 - 11 // 0 // Name";
                return true;
            case Zone241ReturnTownEventCode:
                label = "Den of Rebirth Return Town 0 - 11 //";
                return true;
            case WarHasStartedEventCode:
                label = "The War has started";
                return true;
            case InstinctDefenseFormationInUseEventCode:
                label = "Instinct defense formation in use";
                return true;
            case ReturnToOriginalFactionEventCode:
                label = "return to original faction";
                return true;
            case RemainToFolBeginEventCode:
                label = "remain to FoL begin";
                return true;
            case FolBeganEventCode:
                label = "FoL began";
                return true;
            case FolSuccessEventCode:
            case FolSuccessDuplicateLabelEventCode:
                label = "FoL success";
                return true;
            case RemainToFolAnnihilationEventCode:
                label = "remain to FoL annhi";
                return true;
            case FolAnnihilationBeganEventCode:
                label = "FoL annhi began";
                return true;
            case FolAnnihilationSucceedEventCode:
                label = "FoL annhi succeed";
                return true;
            case FolAnnihilationFailedEventCode:
                label = "FoL annhi falied";
                return true;
            case AllFactionsAllianceRevokedEventCode:
                label = "all factions alliance revoked";
                return true;
            default:
                label = string.Empty;
                return false;
        }
    }
}
