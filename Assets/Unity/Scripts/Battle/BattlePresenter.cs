using System;
using FateWeaver.Core.Authoring.Statuses;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Simulation.Descriptions;
using UnityEngine;

namespace FateWeaver.Unity
{
    /// <summary>카드를 표현 모델로 옮긴다. 세션 타입을 모르고, 표시 문구는 주입된 현지화에서만
    /// 읽는다 — 그래야 테스트가 세션 없이 전 분기를 돌린다.</summary>
    public sealed class BattlePresenter : MonoBehaviour
    {
        [Tooltip("카드 앞면 아트. 비어 있으면 아트 없이 그린다.")]
        [SerializeField] private CardArtCatalog _cardArt;

        [Tooltip("캐릭터 색 원본. 표시명은 콘텐츠(JSON)에서 온다.")]
        [SerializeField] private CharacterAsset[] _party = Array.Empty<CharacterAsset>();

        private static readonly Color PartyOwnerColor = new Color(0.55f, 0.48f, 0.75f, 1f);

        private DescriptionRequestCatalog _requests;
        [SerializeField] private GameLocalization _localization;
        public GameLocalization Localization => _localization;
        public string EnemyName(string id) => Text.Name("enemy." + id + ".name");
        public string StatusName(FateWeaver.Core.Status.StatusKey key) => Text.Name("status." + key.Id + ".name");

        /// <summary>부팅 콘텐츠의 상태 카탈로그로 설명 요청 카탈로그를 만든다. 카드 본문의 상태
        /// 수명 판정이 전투 규칙과 같은 JSON에서 오게 하는 지점이다.</summary>
        public void Initialize(StatusContentCatalog statuses)
        {
            _requests = DescriptionRequestCatalog.CreateDefault(statuses);
        }

        public CardPresentation For(OwnedCard card)
        {
            Resolve(card.OwnerId, card.Def.Side, out var color, out var isPartyOwned);
            return CardPresentation.FromLocalized(card.Def, _requests, Text, ArtFor(card.Def.Id),
                card.Def.BaseExecutionOrder, false, card.OwnerId, color, isPartyOwned);
        }

        public CardPresentation For(ExecutionCardInstance card)
        {
            Resolve(card.OwnerId, card.Def.Side, out var color, out var isPartyOwned);
            return CardPresentation.FromLocalized(card.Def, _requests, Text, ArtFor(card.Def.Id),
                card.ExecutionOrder, card.IsLocked, card.OwnerId, color, isPartyOwned, card.InstanceId);
        }

        private GameLocalization Text => _localization != null ? _localization
            : throw new InvalidOperationException("BattlePresenter requires an authored GameLocalization.");

        /// <summary>유닛 틴트. 카드 표현과 폴백이 다르다 — 원본 SpawnUnits는 캐릭터를 못 찾으면
        /// 공용 색을 썼고, 원본 OwnerPresentation은 색을 건드리지 않았다.</summary>
        public Color OwnerColor(string ownerId)
        {
            var character = Find(ownerId);
            return character != null ? character.Color : PartyOwnerColor;
        }

        /// <summary>아트는 표현이므로 카탈로그가 갖는다. 플레이어 카드는 아트가 없고(색상 틴트
        /// 아트 방향) 적 카드 셋만 항목을 갖는다.</summary>
        private Sprite ArtFor(string id) => _cardArt != null ? _cardArt.ArtFor(id) : null;

        /// <summary>소유자 분기 셋. 원본 OwnerPresentation의 동작을 그대로 옮긴 것이다:
        /// 적은 표현 없음, 소유자 없는 파티 카드만 isPartyOwned=true, 개별 소유는 색만 채우고
        /// isPartyOwned는 false로 남는다(카드 테두리 표현이 갈린다). 이름은 FromLocalized가 번역한다.</summary>
        private void Resolve(
            string ownerId, Side side, out Color color, out bool isPartyOwned)
        {
            color = default;
            isPartyOwned = false;
            if (side == Side.Enemy)
            {
                return;
            }

            if (ownerId == null)
            {
                color = PartyOwnerColor;
                isPartyOwned = true;
                return;
            }

            var character = Find(ownerId);
            if (character != null)
            {
                color = character.Color;
            }
        }

        private CharacterAsset Find(string ownerId)
        {
            foreach (var character in _party)
            {
                if (character != null && character.Id == ownerId)
                {
                    return character;
                }
            }

            return null;
        }
    }
}
