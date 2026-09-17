namespace FateWeaver.Core.Authoring.Enemies
{
    /// <summary>저작된 적 하나. 묶음은 적이 한 턴에 통째로 존에 올리는 카드 id 목록이다
    /// (적 카드 묶음 설계). 정책은 키만 들고, 인스턴스는 쓰는 쪽이 레지스트리로 만든다.</summary>
    public sealed class EnemySpec
    {
        public string Id;
        public string DisplayName;
        public int MaxHp;
        public string Policy;
        public string[][] Bundles;
    }
}
