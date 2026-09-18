namespace FateWeaver.Core.Authoring.Battles
{
    /// <summary>편성 후보 하나. 이번 노드에 어느 후보가 나올지는 편성 스트림이 정한다(설계 결정 6).</summary>
    public sealed class BattleSpec
    {
        public string Id;
        public string[] Enemies;
    }
}
