using FateWeaver.Core.Status;

namespace FateWeaver.Core.Combat
{
    public sealed class Enemy : IStatusHolder
    {
        /// <summary>전투 안 식별자. 대상 지정·사망 이벤트·카드 소유가 이것을 쓴다. 한 전투 안에서
        /// 유일하며, 편성에서 만들 때는 "{SpecId}#{편성 내 순번}"이다.</summary>
        public string Id { get; }

        /// <summary>적 정의 식별자. 이름 표시·저작 조회가 이것을 쓴다. 같은 적이 여럿 나와도 같다.</summary>
        public string SpecId { get; }

        public int Hp { get; set; }
        public StatusBag Statuses { get; } = new();

        public Enemy(string id, string specId, int hp)
        {
            Id = id;
            SpecId = specId;
            Hp = hp;
        }

        /// <summary>정의와 전투 안 식별자를 가르지 않는 기존 호출부용. SpecId = id.</summary>
        public Enemy(string id, int hp)
            : this(id, id, hp)
        {
        }
    }
}
