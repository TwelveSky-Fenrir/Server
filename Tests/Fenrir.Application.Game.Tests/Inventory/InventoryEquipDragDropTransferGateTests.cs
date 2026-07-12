using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Tests.Inventory;

public class InventoryEquipDragDropTransferGateTests
{
    private static DefaultPData Move(int page1 = 0, int index1 = 0, int quantity1 = 0, int page2 = 0,
        int index2 = 0, int xPost2 = 0, int yPost2 = 0)
    {
        return new DefaultPData
        {
            Page1 = page1, Index1 = index1, Quantity1 = quantity1, Page2 = page2, Index2 = index2,
            XPost2 = xPost2, YPost2 = yPost2
        };
    }

    [Fact]
    public void Evaluate_UnrelatedSort_AlwaysValid()
    {
        var outcome = InventoryEquipDragDropTransferGate.Evaluate(208, Move(page1: 99, index1: 999), GameDate.Today());

        Assert.Equal(InventoryEquipDragDropTransferGate.Outcome.Valid, outcome);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(0, 63, 0)]
    [InlineData(1, 0, 12)]
    [InlineData(1, 63, 12)]
    public void Evaluate_Sort210_InRangeFields_IsValid(int page1, int index1, int index2)
    {
        var today = GameDate.Today();
        var outcome = InventoryEquipDragDropTransferGate.Evaluate(210,
            Move(page1: page1, index1: index1, index2: index2), today);

        Assert.Equal(InventoryEquipDragDropTransferGate.Outcome.Valid, outcome);
    }

    [Theory]
    [InlineData(2, 0, 0)]
    [InlineData(-1, 0, 0)]
    [InlineData(0, 64, 0)]
    [InlineData(0, -1, 0)]
    [InlineData(0, 0, 13)]
    [InlineData(0, 0, -1)]
    public void Evaluate_Sort210_OutOfRangeField_IsIndexOutOfRange(int page1, int index1, int index2)
    {
        var outcome = InventoryEquipDragDropTransferGate.Evaluate(210,
            Move(page1: page1, index1: index1, index2: index2), GameDate.Today());

        Assert.Equal(InventoryEquipDragDropTransferGate.Outcome.IndexOutOfRange, outcome);
    }

    [Fact]
    public void Evaluate_Sort210_SourcePage0_InventoryDateIrrelevant_IsValid()
    {
        var outcome = InventoryEquipDragDropTransferGate.Evaluate(210,
            Move(page1: ContainerMatrix.InventoryPage0), inventoryDate: 0);

        Assert.Equal(InventoryEquipDragDropTransferGate.Outcome.Valid, outcome);
    }

    [Fact]
    public void Evaluate_Sort210_SourcePage1_ExpiredInventoryDate_IsDatedStoragePageExpired()
    {
        var today = GameDate.Today();
        GameDate.TryAddDays(today, -1, out var yesterday);

        var outcome = InventoryEquipDragDropTransferGate.Evaluate(210,
            Move(page1: ContainerMatrix.InventoryPage1), yesterday);

        Assert.Equal(InventoryEquipDragDropTransferGate.Outcome.DatedStoragePageExpired, outcome);
    }

    [Fact]
    public void Evaluate_Sort210_SourcePage1_CurrentInventoryDate_IsValid()
    {
        var today = GameDate.Today();

        var outcome = InventoryEquipDragDropTransferGate.Evaluate(210,
            Move(page1: ContainerMatrix.InventoryPage1), today);

        Assert.Equal(InventoryEquipDragDropTransferGate.Outcome.Valid, outcome);
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0)]
    [InlineData(12, 1, 63, 7, 7)]
    [InlineData(0, 0, 63, 0, 0)]
    public void Evaluate_Sort213_InRangeFields_IsValid(int index1, int page2, int index2, int xPost2, int yPost2)
    {
        var today = GameDate.Today();
        var outcome = InventoryEquipDragDropTransferGate.Evaluate(213,
            Move(index1: index1, page2: page2, index2: index2, xPost2: xPost2, yPost2: yPost2), today);

        Assert.Equal(InventoryEquipDragDropTransferGate.Outcome.Valid, outcome);
    }

    [Theory]
    [InlineData(13, 0, 0, 0, 0)]
    [InlineData(-1, 0, 0, 0, 0)]
    [InlineData(0, 2, 0, 0, 0)]
    [InlineData(0, 0, 64, 0, 0)]
    [InlineData(0, 0, 0, 8, 0)]
    [InlineData(0, 0, 0, -1, 0)]
    [InlineData(0, 0, 0, 0, 8)]
    [InlineData(0, 0, 0, 0, -1)]
    public void Evaluate_Sort213_OutOfRangeField_IsIndexOutOfRange(int index1, int page2, int index2, int xPost2,
        int yPost2)
    {
        var outcome = InventoryEquipDragDropTransferGate.Evaluate(213,
            Move(index1: index1, page2: page2, index2: index2, xPost2: xPost2, yPost2: yPost2), GameDate.Today());

        Assert.Equal(InventoryEquipDragDropTransferGate.Outcome.IndexOutOfRange, outcome);
    }

    [Fact]
    public void Evaluate_Sort213_DestinationPage1_ExpiredInventoryDate_IsDatedStoragePageExpired()
    {
        var today = GameDate.Today();
        GameDate.TryAddDays(today, -1, out var yesterday);

        var outcome = InventoryEquipDragDropTransferGate.Evaluate(213,
            Move(page2: ContainerMatrix.InventoryPage1), yesterday);

        Assert.Equal(InventoryEquipDragDropTransferGate.Outcome.DatedStoragePageExpired, outcome);
    }

    [Fact]
    public void Evaluate_Sort213_DestinationPage0_InventoryDateIrrelevant_IsValid()
    {
        var outcome = InventoryEquipDragDropTransferGate.Evaluate(213,
            Move(page2: ContainerMatrix.InventoryPage0), inventoryDate: 0);

        Assert.Equal(InventoryEquipDragDropTransferGate.Outcome.Valid, outcome);
    }
}
