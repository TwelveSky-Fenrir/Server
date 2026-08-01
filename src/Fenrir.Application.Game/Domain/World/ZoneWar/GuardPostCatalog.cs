using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public static class GuardPostCatalogFactory
{
    private static readonly ImmutableArray<GuardSlotCoordinate> Zone038WinnerSlots =
    [
        new(-1f, 200f, 6379f, 0),
        new(-30f, 200f, 6344f, 1),
        new(-73f, 200f, 6357f, 2),
        new(-72f, 200f, 6398f, 3),
        new(-31f, 200f, 6414f, 4)
    ];

    public static GuardPostCatalog BuildLive()
    {
        return new GuardPostCatalog(BuildOrdinaryPosts(), BuildZone038WinnerPosts());
    }

    private static ImmutableArray<GuardPostDefinition> BuildOrdinaryPosts()
    {
        return
        [
            new GuardPostDefinition(38, 0, 6, 24,
            [
                new GuardSlotCoordinate(-1767f, 160f, 2511f, 0),
                new GuardSlotCoordinate(-1724f, 160f, 2414f, 1),
                new GuardSlotCoordinate(-1801f, 160f, 2340f, 2),
                new GuardSlotCoordinate(-1902f, 160f, 2388f, 3),
                new GuardSlotCoordinate(-1881f, 160f, 2503f, 4)
            ]),
            new GuardPostDefinition(38, 1, 7, 24,
            [
                new GuardSlotCoordinate(209f, 160f, 311f, 5),
                new GuardSlotCoordinate(155f, 160f, 393f, 6),
                new GuardSlotCoordinate(206f, 160f, 474f, 7),
                new GuardSlotCoordinate(300f, 160f, 445f, 8),
                new GuardSlotCoordinate(298f, 160f, 344f, 9)
            ]),
            new GuardPostDefinition(38, 2, 8, 24,
            [
                new GuardSlotCoordinate(2527f, 160f, 257f, 10),
                new GuardSlotCoordinate(2433f, 160f, 287f, 11),
                new GuardSlotCoordinate(2438f, 160f, 390f, 12),
                new GuardSlotCoordinate(2533f, 160f, 416f, 13),
                new GuardSlotCoordinate(2594f, 160f, 334f, 14)
            ]),
            new GuardPostDefinition(38, 3, 9, 24,
            [
                new GuardSlotCoordinate(4692f, 160f, 2433f, 15),
                new GuardSlotCoordinate(4615f, 160f, 2372f, 16),
                new GuardSlotCoordinate(4531f, 160f, 2442f, 17),
                new GuardSlotCoordinate(4560f, 161f, 2529f, 18),
                new GuardSlotCoordinate(4665f, 161f, 2527f, 19)
            ]),
            new GuardPostDefinition(2, 0, 6, 27,
            [
                new GuardSlotCoordinate(-4154f, -3f, -2856f, 0),
                new GuardSlotCoordinate(-4066f, -1f, -2919f, 1),
                new GuardSlotCoordinate(-4089f, -2f, -3020f, 2),
                new GuardSlotCoordinate(-4209f, -3f, -3021f, 3),
                new GuardSlotCoordinate(-4241f, 0f, -2921f, 4)
            ]),
            new GuardPostDefinition(2, 0, 6, 27,
            [
                new GuardSlotCoordinate(-1898f, -4f, 3173f, 20),
                new GuardSlotCoordinate(-1826f, -9f, 3241f, 21),
                new GuardSlotCoordinate(-1725f, -4f, 3198f, 22),
                new GuardSlotCoordinate(-1742f, -1f, 3092f, 23),
                new GuardSlotCoordinate(-1854f, -1f, 3077f, 24)
            ], 0),
            new GuardPostDefinition(3, 0, 6, 26,
            [
                new GuardSlotCoordinate(-98f, -9f, 908f, 0),
                new GuardSlotCoordinate(-11f, -9f, 863f, 1),
                new GuardSlotCoordinate(-27f, -9f, 762f, 2),
                new GuardSlotCoordinate(-147f, -9f, 771f, 3),
                new GuardSlotCoordinate(-175f, -9f, 850f, 4)
            ]),
            new GuardPostDefinition(4, 0, 6, 25,
            [
                new GuardSlotCoordinate(-70f, -10f, 1916f, 0),
                new GuardSlotCoordinate(18f, -10f, 1868f, 1),
                new GuardSlotCoordinate(-3f, -10f, 1760f, 2),
                new GuardSlotCoordinate(-95f, -10f, 1748f, 3),
                new GuardSlotCoordinate(-144f, -10f, 1847f, 4)
            ]),
            new GuardPostDefinition(7, 1, 7, 27,
            [
                new GuardSlotCoordinate(3274f, 28f, -4636f, 5),
                new GuardSlotCoordinate(3356f, 29f, -4697f, 6),
                new GuardSlotCoordinate(3330f, 31f, -4795f, 7),
                new GuardSlotCoordinate(3219f, 31f, -4796f, 8),
                new GuardSlotCoordinate(3195f, 32f, -4695f, 9)
            ]),
            new GuardPostDefinition(7, 1, 7, 27,
            [
                new GuardSlotCoordinate(-826f, 11f, -3480f, 20),
                new GuardSlotCoordinate(-914f, 9f, -3410f, 21),
                new GuardSlotCoordinate(-883f, 5f, -3309f, 22),
                new GuardSlotCoordinate(-778f, 7f, -3317f, 23),
                new GuardSlotCoordinate(-743f, 10f, -3409f, 24)
            ], 1),
            new GuardPostDefinition(8, 1, 7, 26,
            [
                new GuardSlotCoordinate(215f, 1f, 1591f, 5),
                new GuardSlotCoordinate(293f, 0f, 1526f, 6),
                new GuardSlotCoordinate(261f, 0f, 1427f, 7),
                new GuardSlotCoordinate(159f, 0f, 1432f, 8),
                new GuardSlotCoordinate(130f, 0f, 1533f, 9)
            ]),
            new GuardPostDefinition(9, 1, 7, 25,
            [
                new GuardSlotCoordinate(-226f, -11f, 1513f, 5),
                new GuardSlotCoordinate(-135f, -14f, 1451f, 6),
                new GuardSlotCoordinate(-160f, -9f, 1352f, 7),
                new GuardSlotCoordinate(-274f, 0f, 1348f, 8),
                new GuardSlotCoordinate(-306f, 0f, 1442f, 9)
            ]),
            new GuardPostDefinition(12, 2, 8, 27,
            [
                new GuardSlotCoordinate(-4606f, -1f, -4211f, 10),
                new GuardSlotCoordinate(-4516f, -11f, -4271f, 11),
                new GuardSlotCoordinate(-4533f, -3f, -4374f, 12),
                new GuardSlotCoordinate(-4654f, 1f, -4384f, 13),
                new GuardSlotCoordinate(-4691f, 1f, -4283f, 14)
            ]),
            new GuardPostDefinition(12, 2, 8, 27,
            [
                new GuardSlotCoordinate(-4053f, 0f, 1743f, 20),
                new GuardSlotCoordinate(-3962f, 0f, 1681f, 21),
                new GuardSlotCoordinate(-3986f, -5f, 1575f, 22),
                new GuardSlotCoordinate(-4091f, -4f, 1566f, 23),
                new GuardSlotCoordinate(-4135f, 0f, 1664f, 24)
            ], 2),
            new GuardPostDefinition(13, 2, 8, 26,
            [
                new GuardSlotCoordinate(33f, 13f, 1569f, 10),
                new GuardSlotCoordinate(105f, 9f, 1486f, 11),
                new GuardSlotCoordinate(46f, 13f, 1386f, 12),
                new GuardSlotCoordinate(-61f, 13f, 1412f, 13),
                new GuardSlotCoordinate(-62f, 13f, 1528f, 14)
            ]),
            new GuardPostDefinition(14, 2, 8, 25,
            [
                new GuardSlotCoordinate(9f, 12f, 1905f, 10),
                new GuardSlotCoordinate(115f, 10f, 1863f, 11),
                new GuardSlotCoordinate(111f, 12f, 1752f, 12),
                new GuardSlotCoordinate(10f, 12f, 1724f, 13),
                new GuardSlotCoordinate(-56f, 12f, 1808f, 14)
            ]),
            new GuardPostDefinition(141, 3, 9, 27,
            [
                new GuardSlotCoordinate(-1f, -16f, 1540f, 15),
                new GuardSlotCoordinate(75f, -19f, 1464f, 16),
                new GuardSlotCoordinate(25f, -19f, 1362f, 17),
                new GuardSlotCoordinate(-73f, -19f, 1375f, 18),
                new GuardSlotCoordinate(-98f, -19f, 1484f, 19)
            ]),
            new GuardPostDefinition(141, 3, 9, 27,
            [
                new GuardSlotCoordinate(-1164f, 0f, 3568f, 20),
                new GuardSlotCoordinate(-1060f, 0f, 3542f, 21),
                new GuardSlotCoordinate(-1060f, 2f, 3429f, 22),
                new GuardSlotCoordinate(-1149f, 3f, 3395f, 23),
                new GuardSlotCoordinate(-1224f, 0f, 3480f, 24)
            ], 3),
            new GuardPostDefinition(142, 3, 9, 26,
            [
                new GuardSlotCoordinate(-44f, -13f, 940f, 15),
                new GuardSlotCoordinate(40f, -14f, 872f, 16),
                new GuardSlotCoordinate(4f, -14f, 763f, 17),
                new GuardSlotCoordinate(-104f, -14f, 769f, 18),
                new GuardSlotCoordinate(-138f, -14f, 879f, 19)
            ]),
            new GuardPostDefinition(143, 3, 9, 25,
            [
                new GuardSlotCoordinate(16f, -13f, 976f, 15),
                new GuardSlotCoordinate(96f, -14f, 913f, 16),
                new GuardSlotCoordinate(60f, -14f, 804f, 17),
                new GuardSlotCoordinate(-38f, -14f, 811f, 18),
                new GuardSlotCoordinate(-70f, -14f, 921f, 19)
            ])
        ];
    }

    private static ImmutableArray<GuardPostDefinition> BuildZone038WinnerPosts()
    {
        var builder = ImmutableArray.CreateBuilder<GuardPostDefinition>(4);

        for (byte tribeId = 0; tribeId < 4; tribeId++)
        {
            var slotBase = tribeId * GuardPostDefinition.SlotsPerPost;
            var slots = ImmutableArray.CreateBuilder<GuardSlotCoordinate>(GuardPostDefinition.SlotsPerPost);
            foreach (var slot in Zone038WinnerSlots)
                slots.Add(slot with { ReservedSlotIndex = slotBase + slot.ReservedSlotIndex });

            builder.Add(new GuardPostDefinition(TribeGuardSpawner.Zone038MapId, tribeId, (byte)(6 + tribeId), 30,
                slots.MoveToImmutable()));
        }

        return builder.MoveToImmutable();
    }
}
