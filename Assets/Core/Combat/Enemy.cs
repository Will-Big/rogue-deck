using FateWeaver.Core.Status;

namespace FateWeaver.Core.Combat
{
    public sealed class Enemy : IStatusHolder
    {
        public string Id { get; }
        public int Hp { get; set; }
        public StatusBag Statuses { get; } = new();

        public Enemy(string id, int hp)
        {
            Id = id;
            Hp = hp;
        }
    }
}
