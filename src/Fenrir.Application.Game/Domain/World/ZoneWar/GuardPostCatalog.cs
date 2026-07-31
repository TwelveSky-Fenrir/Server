using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

// ReservedSlotIndex mirrors the legacy index03 walk over a 5x5 pool: postGroup * 5 + slot, where postGroup
// is the tribe index and group 4 holds the symbol-conditional post (Server/ts25zone/S10_MySummon.cpp:1692-1725).

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
            new(38, 0, 6, 24,
            [
                new(-1767f, 160f, 2511f, 0),
                new(-1724f, 160f, 2414f, 1),
                new(-1801f, 160f, 2340f, 2),
                new(-1902f, 160f, 2388f, 3),
                new(-1881f, 160f, 2503f, 4)
            ]),
            new(38, 1, 7, 24,
            [
                new(209f, 160f, 311f, 5),
                new(155f, 160f, 393f, 6),
                new(206f, 160f, 474f, 7),
                new(300f, 160f, 445f, 8),
                new(298f, 160f, 344f, 9)
            ]),
            new(38, 2, 8, 24,
            [
                new(2527f, 160f, 257f, 10),
                new(2433f, 160f, 287f, 11),
                new(2438f, 160f, 390f, 12),
                new(2533f, 160f, 416f, 13),
                new(2594f, 160f, 334f, 14)
            ]),
            new(38, 3, 9, 24,
            [
                new(4692f, 160f, 2433f, 15),
                new(4615f, 160f, 2372f, 16),
                new(4531f, 160f, 2442f, 17),
                new(4560f, 161f, 2529f, 18),
                new(4665f, 161f, 2527f, 19)
            ]),
            new(2, 0, 6, 27,
            [
                new(-4154f, -3f, -2856f, 0),
                new(-4066f, -1f, -2919f, 1),
                new(-4089f, -2f, -3020f, 2),
                new(-4209f, -3f, -3021f, 3),
                new(-4241f, 0f, -2921f, 4)
            ]),
            new(2, 0, 6, 27,
            [
                new(-1898f, -4f, 3173f, 20),
                new(-1826f, -9f, 3241f, 21),
                new(-1725f, -4f, 3198f, 22),
                new(-1742f, -1f, 3092f, 23),
                new(-1854f, -1f, 3077f, 24)
            ], (byte)0),
            new(3, 0, 6, 26,
            [
                new(-98f, -9f, 908f, 0),
                new(-11f, -9f, 863f, 1),
                new(-27f, -9f, 762f, 2),
                new(-147f, -9f, 771f, 3),
                new(-175f, -9f, 850f, 4)
            ]),
            new(4, 0, 6, 25,
            [
                new(-70f, -10f, 1916f, 0),
                new(18f, -10f, 1868f, 1),
                new(-3f, -10f, 1760f, 2),
                new(-95f, -10f, 1748f, 3),
                new(-144f, -10f, 1847f, 4)
            ]),
            new(7, 1, 7, 27,
            [
                new(3274f, 28f, -4636f, 5),
                new(3356f, 29f, -4697f, 6),
                new(3330f, 31f, -4795f, 7),
                new(3219f, 31f, -4796f, 8),
                new(3195f, 32f, -4695f, 9)
            ]),
            new(7, 1, 7, 27,
            [
                new(-826f, 11f, -3480f, 20),
                new(-914f, 9f, -3410f, 21),
                new(-883f, 5f, -3309f, 22),
                new(-778f, 7f, -3317f, 23),
                new(-743f, 10f, -3409f, 24)
            ], (byte)1),
            new(8, 1, 7, 26,
            [
                new(215f, 1f, 1591f, 5),
                new(293f, 0f, 1526f, 6),
                new(261f, 0f, 1427f, 7),
                new(159f, 0f, 1432f, 8),
                new(130f, 0f, 1533f, 9)
            ]),
            new(9, 1, 7, 25,
            [
                new(-226f, -11f, 1513f, 5),
                new(-135f, -14f, 1451f, 6),
                new(-160f, -9f, 1352f, 7),
                new(-274f, 0f, 1348f, 8),
                new(-306f, 0f, 1442f, 9)
            ]),
            new(12, 2, 8, 27,
            [
                new(-4606f, -1f, -4211f, 10),
                new(-4516f, -11f, -4271f, 11),
                new(-4533f, -3f, -4374f, 12),
                new(-4654f, 1f, -4384f, 13),
                new(-4691f, 1f, -4283f, 14)
            ]),
            new(12, 2, 8, 27,
            [
                new(-4053f, 0f, 1743f, 20),
                new(-3962f, 0f, 1681f, 21),
                new(-3986f, -5f, 1575f, 22),
                new(-4091f, -4f, 1566f, 23),
                new(-4135f, 0f, 1664f, 24)
            ], (byte)2),
            new(13, 2, 8, 26,
            [
                new(33f, 13f, 1569f, 10),
                new(105f, 9f, 1486f, 11),
                new(46f, 13f, 1386f, 12),
                new(-61f, 13f, 1412f, 13),
                new(-62f, 13f, 1528f, 14)
            ]),
            new(14, 2, 8, 25,
            [
                new(9f, 12f, 1905f, 10),
                new(115f, 10f, 1863f, 11),
                new(111f, 12f, 1752f, 12),
                new(10f, 12f, 1724f, 13),
                new(-56f, 12f, 1808f, 14)
            ]),
            new(141, 3, 9, 27,
            [
                new(-1f, -16f, 1540f, 15),
                new(75f, -19f, 1464f, 16),
                new(25f, -19f, 1362f, 17),
                new(-73f, -19f, 1375f, 18),
                new(-98f, -19f, 1484f, 19)
            ]),
            new(141, 3, 9, 27,
            [
                new(-1164f, 0f, 3568f, 20),
                new(-1060f, 0f, 3542f, 21),
                new(-1060f, 2f, 3429f, 22),
                new(-1149f, 3f, 3395f, 23),
                new(-1224f, 0f, 3480f, 24)
            ], (byte)3),
            new(142, 3, 9, 26,
            [
                new(-44f, -13f, 940f, 15),
                new(40f, -14f, 872f, 16),
                new(4f, -14f, 763f, 17),
                new(-104f, -14f, 769f, 18),
                new(-138f, -14f, 879f, 19)
            ]),
            new(143, 3, 9, 25,
            [
                new(16f, -13f, 976f, 15),
                new(96f, -14f, 913f, 16),
                new(60f, -14f, 804f, 17),
                new(-38f, -14f, 811f, 18),
                new(-70f, -14f, 921f, 19)
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
