using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public static class KnownTSortRegistry
{
    public static readonly FrozenSet<int> KnownSorts = Build();

    public static bool IsKnown(int sort)
    {
        return KnownSorts.Contains(sort);
    }

    private static FrozenSet<int> Build()
    {
        var set = new HashSet<int>();

        AddRange(set, 1, 115);

        AddRange(set, 200, 208);

        set.Add(301);
        set.Add(302);

        // Emis par ZoneEventBroadcaster.AnnounceMonsterSymbolAttackWindow : sans cette entree le routeur
        // jetterait un event que Fenrir produit lui-meme (Server/ts25zone/S07_MyGame01.cpp:2526).
        set.Add(401);

        AddRange(set, 402, 415);

        set.Add(416);
        AddRange(set, 418, 428);
        set.Add(500);

        AddRange(set, 601, 615);

        // Cycle ZONE088, compile hors de la build livree (garde ZONE088 commentee,
        // Server/ts25zone/S07_MyGame01.cpp:6260). Porte au titre de la decision "tout porter, mort ou vif".
        set.Add(620);
        set.Add(621);
        set.Add(622);
        set.Add(626);
        set.Add(628);

        // ZONE200 : 659/660/662 appartiennent au meme bloc que 663 et 666-669 deja presents, et sont deja
        // emis par ZoneEventBroadcaster. Leur absence etait une asymetrie accidentelle.
        set.Add(659);
        set.Add(660);
        set.Add(661);
        set.Add(662);
        AddRange(set, 663, 669);
        set.Add(671);
        set.Add(672);

        // Annonce de raffinage : USE_REFINE est #undef dans les deux builds livres (DEFINE.h:106).
        set.Add(673);
        set.Add(674);
        set.Add(675);

        set.Add(751);

        // Jumeau de 753, emis a la ligne precedente du meme bloc (Server/ts25zone/S07_MyGame01.cpp:12833).
        set.Add(752);
        AddRange(set, 753, 763);
        AddRange(set, 771, 774);

        // Mise a jour du logo de guilde (Server/ts25zone/S04_MyWork02.cpp:10411, USE_GUILD_LOGO actif).
        set.Add(1001);

        set.Add(1133);

        // Codes origine-Fenrir (origine-center dans le legacy) : synchro des points de tribu, reset quotidien,
        // ping de reset du drapeau de recompense HSB. Ils sont emis en direct vers les clients et ne
        // traversent pas ce filtre aujourd'hui, mais les allowlister evite qu'un relais cross-shard les jette
        // en silence si l'un d'eux devient un jour relayable.
        set.Add(1234);
        set.Add(1600);
        set.Add(1601);

        AddRange(set, 1501, 1507);
        AddRange(set, 1510, 1514);
        set.Add(1520);

        // Notices Den of Rebirth, compilees hors de la build livree (gardes ZONE241/ZONE241_NOTICE).
        set.Add(1611);
        set.Add(1612);
        set.Add(1614);
        set.Add(1615);

        // Notices monde cross-zone. 2000 est le tSort le plus emis du legacy (10 sites vivants, dont
        // Server/ts25zone/S04_MyWork02.cpp:487 et S07_MyGame03.cpp:708).
        set.Add(2000);
        set.Add(2001);

        // Inscription de tournoi : TOURNAMENT_REGISTER est commente (DEFINE.h:41).
        set.Add(2002);
        set.Add(2003);

        // Annonce d'enchantement de costume (USE_ENCHANT_COSTUME_V2 actif via DEFINE.h:95 -> :120).
        set.Add(2101);

        // ZONE194 : garde commentee dans H07_MyGame.h:34.
        set.Add(3001);

        set.Add(4000);

        return set.ToFrozenSet();
    }

    private static void AddRange(HashSet<int> set, int inclusiveLow, int inclusiveHigh)
    {
        for (var value = inclusiveLow; value <= inclusiveHigh; value++)
            set.Add(value);
    }
}
