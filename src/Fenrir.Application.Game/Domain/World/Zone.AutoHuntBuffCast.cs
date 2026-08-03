using Fenrir.Core.Packets.Shared;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    private readonly List<int> _autoHuntBuffCastNeighborScratch = [];

    internal void BroadcastAutoHuntBuffCast(PlayerRuntimeState state, int actionType, int actionSort, int skillId,
        int skillGradeNum1, int skillGradeNum2)
    {
        state.ActionType = actionType;
        state.ActionSort = actionSort;
        state.ActionSkillNumber = skillId;
        state.ActionSkillGradeNum1 = skillGradeNum1;
        state.ActionSkillGradeNum2 = skillGradeNum2;

        var pet = PetActionFieldsOf(state);
        var action = new ActionInfo
        {
            Type = actionType,
            Sort = actionSort,
            Frame = 0,
            Location = [state.PosX, state.PosY, state.PosZ],
            TargetLocation = [state.PosX, state.PosY, state.PosZ],
            Front = state.Heading,
            TargetFront = state.Heading,
            PetLocation = pet.PetLocation,
            PetTargetLocation = pet.PetTargetLocation,
            PetFront = pet.PetFront,
            PetSort = pet.PetSort,
            TargetObjectSort = 0,
            TargetObjectIndex = 0,
            TargetObjectUniqueNumber = 0,
            SkillNumber = skillId,
            SkillGradeNum1 = skillGradeNum1,
            SkillGradeNum2 = skillGradeNum2,
            SkillValue = 0
        };

        _autoHuntBuffCastNeighborScratch.Clear();
        _grid.NeighborsExcludingSelf(_autoHuntBuffCastNeighborScratch, state.CurrentCell, state.CharacterId,
            state.PosX, state.PosY, state.PosZ);
        _autoHuntBuffCastNeighborScratch.Add(state.CharacterId);
        BroadcastAvatarAction(_autoHuntBuffCastNeighborScratch, state, action);
    }
}
