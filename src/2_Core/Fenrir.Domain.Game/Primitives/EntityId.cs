namespace Fenrir.Domain.Game.Primitives;

public readonly record struct EntityId(int Value)
{

        public static EntityId None => new(0);

        public bool IsNone => Value == 0;
}
