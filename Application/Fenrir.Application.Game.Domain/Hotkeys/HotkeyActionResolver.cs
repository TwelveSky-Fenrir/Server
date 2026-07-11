using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Skills;

namespace Fenrir.Application.Game.Domain.Hotkeys;

public static class HotkeyActionResolver
{
    public enum BindEmoticonFailure
    {
        None,
        InvalidDestinationPage,
        InvalidDestinationIndex,
        InvalidCode,
        DestinationOccupied
    }

    public enum BindItemFailure
    {
        None,
        InvalidDestinationPage,
        InvalidDestinationIndex,
        InvalidSourcePage,
        InvalidSourceIndex,
        SourceEmpty,
        NotStackable,
        ExcludedPotionSubtype,
        InvalidQuantity,
        InsufficientSourceQuantity,
        DestinationItemMismatch,
        DestinationOverCap
    }

    public enum BindSkillFailure
    {
        None,
        InvalidDestinationPage,
        InvalidDestinationIndex,
        InvalidSkillSlot,
        SkillSlotEmpty,
        InvalidGrade,
        DestinationOccupied
    }

    public enum RearrangeFailure
    {
        None,
        InvalidSourcePage,
        InvalidSourceIndex,
        InvalidDestinationPage,
        InvalidDestinationIndex,
        SourceEmpty,
        DestinationOccupied,
        InvalidQuantity,
        InsufficientSourceQuantity,
        DestinationItemMismatch,
        DestinationOverCap
    }

    public enum UnbindFailure
    {
        None,
        InvalidPage,
        InvalidIndex,
        AlreadyEmpty,
        ItemBindingNotSupported
    }

    public enum WithdrawItemFailure
    {
        None,
        InvalidSourcePage,
        InvalidSourceIndex,
        SourceEmpty,
        SourceNotItem,
        InvalidQuantity,
        InsufficientSourceQuantity,
        InvalidDestinationPage,
        InvalidDestinationIndex,
        InvalidDestinationX,
        InvalidDestinationY,
        DestinationItemMismatch,
        DestinationOverCap
    }

    public const int PageCount = 3;

    public const int SlotsPerPage = 14;

    public const int MinEmoticonCode = 1;

    public const int MaxEmoticonCode = 9;

    public const int MinSkillGrade = 1;

    public const int MaxItemQuantity = 999;

    public const int MinItemQuantity = 1;

    public static bool IsValidPage(int page)
    {
        return page is >= 0 and < PageCount;
    }

    public static bool IsValidIndex(int index)
    {
        return index is >= 0 and < SlotsPerPage;
    }

    public static BindSkillResult ResolveBindSkill(
        HotkeySlot destination, int destinationPage, int destinationIndex,
        int skillSlotIndex, int requestedGrade,
        IReadOnlyDictionary<byte, LearnedSkill> learnedSkills)
    {
        if (!IsValidPage(destinationPage))
            return BindSkillResult.Fail(BindSkillFailure.InvalidDestinationPage);

        if (!IsValidIndex(destinationIndex))
            return BindSkillResult.Fail(BindSkillFailure.InvalidDestinationIndex);

        if (skillSlotIndex < 0 || skillSlotIndex >= SkillLearnResolver.MaxSlots)
            return BindSkillResult.Fail(BindSkillFailure.InvalidSkillSlot);

        if (!learnedSkills.TryGetValue((byte)skillSlotIndex, out var learned))
            return BindSkillResult.Fail(BindSkillFailure.SkillSlotEmpty);

        if (requestedGrade < MinSkillGrade || requestedGrade > learned.Grade)
            return BindSkillResult.Fail(BindSkillFailure.InvalidGrade);

        if (!destination.IsEmpty)
            return BindSkillResult.Fail(BindSkillFailure.DestinationOccupied);

        var newDestination = new HotkeySlot(HotkeyBindingKind.Skill, learned.SkillId, requestedGrade);
        return new BindSkillResult(true, BindSkillFailure.None, newDestination);
    }

    public static BindEmoticonResult ResolveBindEmoticon(
        HotkeySlot destination, int destinationPage, int destinationIndex, int emoticonCode)
    {
        if (!IsValidPage(destinationPage))
            return BindEmoticonResult.Fail(BindEmoticonFailure.InvalidDestinationPage);

        if (!IsValidIndex(destinationIndex))
            return BindEmoticonResult.Fail(BindEmoticonFailure.InvalidDestinationIndex);

        if (emoticonCode < MinEmoticonCode || emoticonCode > MaxEmoticonCode)
            return BindEmoticonResult.Fail(BindEmoticonFailure.InvalidCode);

        if (!destination.IsEmpty)
            return BindEmoticonResult.Fail(BindEmoticonFailure.DestinationOccupied);

        var newDestination = new HotkeySlot(HotkeyBindingKind.Emoticon, emoticonCode, 0);
        return new BindEmoticonResult(true, BindEmoticonFailure.None, newDestination);
    }

    public static UnbindResult ResolveUnbind(HotkeySlot slot, int page, int index)
    {
        if (!IsValidPage(page))
            return UnbindResult.Fail(UnbindFailure.InvalidPage);

        if (!IsValidIndex(index))
            return UnbindResult.Fail(UnbindFailure.InvalidIndex);

        if (slot.IsEmpty)
            return UnbindResult.Fail(UnbindFailure.AlreadyEmpty);

        if (slot.Kind == HotkeyBindingKind.Item)
            return UnbindResult.Fail(UnbindFailure.ItemBindingNotSupported);

        return UnbindResult.Succeeded;
    }

    public static BindItemResult ResolveBindItem(
        HotkeySlot destination, int destinationPage, int destinationIndex,
        ItemStack? sourceItem, int sourcePage, int sourceIndex, int requestedQuantity,
        bool sourceItemIsStackable, bool sourceItemIsExcludedPotionSubtype)
    {
        if (!IsValidPage(destinationPage))
            return BindItemResult.Fail(BindItemFailure.InvalidDestinationPage);

        if (!IsValidIndex(destinationIndex))
            return BindItemResult.Fail(BindItemFailure.InvalidDestinationIndex);

        if (sourcePage is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1))
            return BindItemResult.Fail(BindItemFailure.InvalidSourcePage);

        if (!ContainerMatrix.IsValidSlot((byte)sourcePage, sourceIndex))
            return BindItemResult.Fail(BindItemFailure.InvalidSourceIndex);

        if (sourceItem is not { } source)
            return BindItemResult.Fail(BindItemFailure.SourceEmpty);

        if (!sourceItemIsStackable)
            return BindItemResult.Fail(BindItemFailure.NotStackable);

        if (sourceItemIsExcludedPotionSubtype)
            return BindItemResult.Fail(BindItemFailure.ExcludedPotionSubtype);

        if (requestedQuantity < MinItemQuantity || requestedQuantity > MaxItemQuantity)
            return BindItemResult.Fail(BindItemFailure.InvalidQuantity);

        if (requestedQuantity > source.Quantity)
            return BindItemResult.Fail(BindItemFailure.InsufficientSourceQuantity);

        int newQuantity;
        if (destination.Kind == HotkeyBindingKind.Item)
        {
            if (destination.Value1 != source.ItemId)
                return BindItemResult.Fail(BindItemFailure.DestinationItemMismatch);

            newQuantity = destination.Value2 + requestedQuantity;
            if (newQuantity > MaxItemQuantity)
                return BindItemResult.Fail(BindItemFailure.DestinationOverCap);
        }
        else
        {
            newQuantity = requestedQuantity;
        }

        var newDestination = new HotkeySlot(HotkeyBindingKind.Item, source.ItemId, newQuantity);
        var remainingSourceQuantity = source.Quantity - requestedQuantity;

        return new BindItemResult(true, BindItemFailure.None, newDestination, remainingSourceQuantity);
    }

    public static WithdrawItemResult ResolveWithdrawItem(
        HotkeySlot source, int sourcePage, int sourceIndex, int requestedQuantity,
        ItemStack? destinationItem, int destinationPage, int destinationIndex,
        int destinationX, int destinationY)
    {
        if (!IsValidPage(sourcePage))
            return WithdrawItemResult.Fail(WithdrawItemFailure.InvalidSourcePage);

        if (!IsValidIndex(sourceIndex))
            return WithdrawItemResult.Fail(WithdrawItemFailure.InvalidSourceIndex);

        if (source.IsEmpty)
            return WithdrawItemResult.Fail(WithdrawItemFailure.SourceEmpty);

        if (source.Kind != HotkeyBindingKind.Item)
            return WithdrawItemResult.Fail(WithdrawItemFailure.SourceNotItem);

        if (requestedQuantity < MinItemQuantity || requestedQuantity > MaxItemQuantity)
            return WithdrawItemResult.Fail(WithdrawItemFailure.InvalidQuantity);

        if (requestedQuantity > source.Value2)
            return WithdrawItemResult.Fail(WithdrawItemFailure.InsufficientSourceQuantity);

        if (destinationPage is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1))
            return WithdrawItemResult.Fail(WithdrawItemFailure.InvalidDestinationPage);

        if (!ContainerMatrix.IsValidSlot((byte)destinationPage, destinationIndex))
            return WithdrawItemResult.Fail(WithdrawItemFailure.InvalidDestinationIndex);

        if (destinationX < 0 || destinationX > 7)
            return WithdrawItemResult.Fail(WithdrawItemFailure.InvalidDestinationX);

        if (destinationY < 0 || destinationY > 7)
            return WithdrawItemResult.Fail(WithdrawItemFailure.InvalidDestinationY);

        int newDestinationQuantity;
        if (destinationItem is { } destination)
        {
            if (destination.ItemId != source.Value1)
                return WithdrawItemResult.Fail(WithdrawItemFailure.DestinationItemMismatch);

            newDestinationQuantity = destination.Quantity + requestedQuantity;
            if (newDestinationQuantity > MaxItemQuantity)
                return WithdrawItemResult.Fail(WithdrawItemFailure.DestinationOverCap);
        }
        else
        {
            newDestinationQuantity = requestedQuantity;
        }

        var remaining = source.Value2 - requestedQuantity;
        var newSource = remaining > 0 ? source with { Value2 = remaining } : HotkeySlot.Empty;

        return new WithdrawItemResult(true, WithdrawItemFailure.None, newSource, source.Value1,
            newDestinationQuantity);
    }

    public static RearrangeResult ResolveRearrange(
        HotkeySlot source, int sourcePage, int sourceIndex,
        HotkeySlot destination, int destinationPage, int destinationIndex,
        int requestedQuantity)
    {
        if (!IsValidPage(sourcePage))
            return RearrangeResult.Fail(RearrangeFailure.InvalidSourcePage);

        if (!IsValidIndex(sourceIndex))
            return RearrangeResult.Fail(RearrangeFailure.InvalidSourceIndex);

        if (!IsValidPage(destinationPage))
            return RearrangeResult.Fail(RearrangeFailure.InvalidDestinationPage);

        if (!IsValidIndex(destinationIndex))
            return RearrangeResult.Fail(RearrangeFailure.InvalidDestinationIndex);

        if (source.IsEmpty)
            return RearrangeResult.Fail(RearrangeFailure.SourceEmpty);

        if (sourcePage == destinationPage && sourceIndex == destinationIndex)
            return new RearrangeResult(true, RearrangeFailure.None, source, destination);

        if (source.Kind is HotkeyBindingKind.Skill or HotkeyBindingKind.Emoticon)
        {
            if (!destination.IsEmpty)
                return RearrangeResult.Fail(RearrangeFailure.DestinationOccupied);

            return new RearrangeResult(true, RearrangeFailure.None, HotkeySlot.Empty, source);
        }

        if (requestedQuantity < MinItemQuantity || requestedQuantity > MaxItemQuantity)
            return RearrangeResult.Fail(RearrangeFailure.InvalidQuantity);

        if (requestedQuantity > source.Value2)
            return RearrangeResult.Fail(RearrangeFailure.InsufficientSourceQuantity);

        int newQuantity;
        if (destination.Kind == HotkeyBindingKind.Item)
        {
            if (destination.Value1 != source.Value1)
                return RearrangeResult.Fail(RearrangeFailure.DestinationItemMismatch);

            newQuantity = destination.Value2 + requestedQuantity;
            if (newQuantity > MaxItemQuantity)
                return RearrangeResult.Fail(RearrangeFailure.DestinationOverCap);
        }
        else
        {
            newQuantity = requestedQuantity;
        }

        var remaining = source.Value2 - requestedQuantity;
        var newSource = remaining > 0 ? source with { Value2 = remaining } : HotkeySlot.Empty;
        var newDestination = new HotkeySlot(HotkeyBindingKind.Item, source.Value1, newQuantity);

        return new RearrangeResult(true, RearrangeFailure.None, newSource, newDestination);
    }

    public readonly record struct BindSkillResult(bool Success, BindSkillFailure Failure, HotkeySlot NewDestination)
    {
        public static BindSkillResult Fail(BindSkillFailure failure)
        {
            return new BindSkillResult(false, failure, default);
        }
    }

    public readonly record struct BindEmoticonResult(
        bool Success,
        BindEmoticonFailure Failure,
        HotkeySlot NewDestination)
    {
        public static BindEmoticonResult Fail(BindEmoticonFailure failure)
        {
            return new BindEmoticonResult(false, failure, default);
        }
    }

    public readonly record struct UnbindResult(bool Success, UnbindFailure Failure)
    {
        public static readonly UnbindResult Succeeded = new(true, UnbindFailure.None);

        public static UnbindResult Fail(UnbindFailure failure)
        {
            return new UnbindResult(false, failure);
        }
    }

    public readonly record struct BindItemResult(
        bool Success,
        BindItemFailure Failure,
        HotkeySlot NewDestination,
        int RemainingSourceQuantity)
    {
        public static BindItemResult Fail(BindItemFailure failure)
        {
            return new BindItemResult(false, failure, default, 0);
        }
    }

    public readonly record struct WithdrawItemResult(
        bool Success,
        WithdrawItemFailure Failure,
        HotkeySlot NewSource,
        int NewDestinationItemId,
        int NewDestinationQuantity)
    {
        public static WithdrawItemResult Fail(WithdrawItemFailure failure)
        {
            return new WithdrawItemResult(false, failure, default, 0, 0);
        }
    }

    public readonly record struct RearrangeResult(
        bool Success,
        RearrangeFailure Failure,
        HotkeySlot NewSource,
        HotkeySlot NewDestination)
    {
        public static RearrangeResult Fail(RearrangeFailure failure)
        {
            return new RearrangeResult(false, failure, default, default);
        }
    }
}
