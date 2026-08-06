using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Intervention;
using Newtonsoft.Json;

namespace FateWeaver.Core.Authoring
{
    /// <summary>저작된 개입 액션 하나. 각 구체 스펙이 자기 파라미터(실타입)와 런타임 페이로드로의
    /// 변환, 검증을 소유한다 — 액션을 더해도 중앙 enum/switch가 자라지 않는다(AGENTS.md 규칙 9).
    /// InterventionSpecCatalog에 명시적으로 등록한다. EffectSpec과 같은 형태다.</summary>
    [Serializable]
    public abstract class InterventionSpec
    {
        [JsonIgnore]
        public abstract InterventionActionKey Key { get; }

        /// <summary>런타임 파라미터로 옮긴다. 파라미터가 없는 액션은 null을 돌려준다.</summary>
        public abstract IInterventionPayload ToPayload();

        public virtual IEnumerable<string> Validate(AuthoringContext context)
        {
            yield break;
        }

        /// <summary>저작 열거형을 코어의 진영으로 옮긴다. Any는 "제한 없음"이라 null이다.</summary>
        protected static Side? ToTargetSide(InterventionTargetSideRef side)
        {
            switch (side)
            {
                case InterventionTargetSideRef.Player: return Side.Player;
                case InterventionTargetSideRef.Enemy: return Side.Enemy;
                default: return null;
            }
        }
    }
}
