namespace Fenrir.Application.Login.Domain.Avatars;

public static class AvatarVitalsFloor
{
    private const int MinLife = 1;
    private const int MinMana = 0;

    public static (int Life, int Mana) Clamp(int life, int mana)
    {
        return (Math.Max(life, MinLife), Math.Max(mana, MinMana));
    }
}
