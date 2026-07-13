using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Stats;
using Fenrir.Data.WriteBehind;
using Fenrir.Application.Game.Packets.Zone;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    private const int RebirthTierLevelUpAvatarChangeInfoSort = 11;

    private const int Zone101TimeStatSort = 18;

    public void ApplyHighLevelExperienceGain(PlayerRuntimeState target, int gain,
        bool antiCheatExperienceFlagged = false)
    {
        var input = HighLevelExperienceInputFactory.Build(target, gain, antiCheatExperienceFlagged,
            worldData.LevelsByLevel);
        var outcome = HighLevelExperienceResolver.Resolve(input);

        HighLevelExperienceOutcomeApplier.Apply(target, outcome);

        switch (outcome.Kind)
        {
            case HighLevelExperienceOutcomeKind.None:
                return;

            case HighLevelExperienceOutcomeKind.MainPoolFill:
            case HighLevelExperienceOutcomeKind.RebirthTierAccrual:
                target.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
                return;

            case HighLevelExperienceOutcomeKind.RebirthTierLevelUp:
                ApplyRebirthTierLevelUp(target, outcome);
                return;

            default:
                throw new ArgumentOutOfRangeException(nameof(outcome), outcome.Kind,
                    "Unhandled HighLevelExperienceOutcomeKind.");
        }
    }

    private void ApplyRebirthTierLevelUp(PlayerRuntimeState target, HighLevelExperienceOutcome outcome)
    {
        var equipmentContainer = target.Inventory.GetContainer(ContainerMatrix.Equipment);
        var petItemId = equipmentContainer.TryGetValue(PetSlots.EquipmentSlot, out var petStack)
            ? petStack.ItemId
            : 0;
        var petContribution = PetGrowthCalculator.Compute(petItemId, target.PetGrowth, target.PetActivity,
            worldData.ItemsById);
        var attributes = new CharacterBaseAttributes(target.StatVit, target.StatStr, target.StatInt,
            target.StatDex, target.Level, target.Tribe, target.PreviousTribe, target.Title, target.Halo,
            target.RebirthCount, target.Level2);
        var stats = EquipmentService.RecomputeStats(attributes, equipmentContainer, worldData, target.Buffs,
            petContribution, target);

        target.Stats = stats;
        target.MaxLife = stats.MaxLife;
        target.MaxMana = stats.MaxMana;

        if (target.Life > 0)
        {
            target.Life = stats.MaxLife;
            target.Mana = stats.MaxMana;
        }

        target.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression | DirtyFlags.Vitals);

        BroadcastAvatarStateFlag(target, RebirthTierLevelUpAvatarChangeInfoSort, target.Level2,
            target.StatPoints, target.SkillPoints);

        if (outcome.Zone101TimeBonus > 0)
            target.Session.Send(new AvatarStatUpdateResponse
                { Sort = Zone101TimeStatSort, Value = target.Zone101Time, Value2 = 0 });
    }
}
