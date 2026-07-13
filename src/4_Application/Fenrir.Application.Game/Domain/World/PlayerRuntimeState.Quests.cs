namespace Fenrir.Application.Game.Domain.World;

public partial class PlayerRuntimeState
{
    public int QuestStepPermanent { get; set; }

    public int QuestActiveFlag { get; set; }

    public int QuestSort { get; set; }

    public int QuestTargetPhase { get; set; }

    public int QuestKillCounter { get; set; }

    public int MissionJoinWar { get; set; }

    public int MissionKillOtherTribe { get; set; }

    public int MissionKillMonster { get; set; }

    public int MissionPlayTime { get; set; }

    public int HeroRankPoints { get; set; }
}
