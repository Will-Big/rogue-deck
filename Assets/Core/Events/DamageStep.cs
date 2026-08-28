namespace FateWeaver.Core.Events
{
    /// <summary>피해가 한 단계에서 어떻게 바뀌었는지. HolderId는 그 단계를 만든 상태의 보유자
    /// (약화는 공격자, 취약·방어는 대상), StatusId는 그 상태의 키다. 값을 바꾸지 않은 상태는
    /// 단계를 남기지 않는다.</summary>
    public sealed record DamageStep(string HolderId, string StatusId, int Before, int After);
}
