namespace Fenrir.Domain.Game.Primitives;

/// <summary>
/// Identifiant d'entité simulée dans une zone (avatar, monstre, objet au sol). Type valeur fortement typé
/// pour éviter les confusions d'index brut (le legacy manipulait des <c>int</c> nus, source de bugs).
/// </summary>
public readonly record struct EntityId(int Value)
{
    /// <summary>Entité absente / non résolue.</summary>
    public static EntityId None => new(0);

    /// <summary>Vrai si l'identifiant ne référence aucune entité.</summary>
    public bool IsNone => Value == 0;
}
