namespace FateWeaver.Core.Combat
{
    public sealed class Enemy
    {
        public string Id { get; }
        public int Hp { get; set; }

        public Enemy(string id, int hp)
        {
            Id = id;
            Hp = hp;
        }
    }
}
