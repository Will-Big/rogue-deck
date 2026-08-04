using System;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using UnityEngine;

namespace FateWeaver.Unity
{
    /// <summary>카드를 표현 모델로 옮긴다. 세션 타입도 콘텐츠 타입도 모르고, 이름 조회만
    /// 델리게이트로 받는다(설계 §4.3) — 그래야 테스트가 세션 없이 전 분기를 돌린다.</summary>
    public sealed class BattlePresenter : MonoBehaviour
    {
        [Tooltip("카드 앞면 아트. 비어 있으면 아트 없이 그린다.")]
        [SerializeField] private CardArtCatalog _cardArt;

        [Tooltip("캐릭터 색 원본. 표시명은 콘텐츠(JSON)에서 온다.")]
        [SerializeField] private CharacterAsset[] _party = Array.Empty<CharacterAsset>();

        private static readonly Color PartyOwnerColor = new Color(0.55f, 0.48f, 0.75f, 1f);

        private Func<string, string> _ownerName;

        /// <summary>세션의 파티에서 표시명을 읽는 델리게이트를 주입한다. 없으면 소유자 이름이
        /// 비어 있는 표현이 나온다.</summary>
        public void Initialize(Func<string, string> ownerName) => _ownerName = ownerName;

        public CardPresentation For(OwnedCard card)
        {
            Resolve(card.OwnerId, card.Def.Side, out var name, out var color, out var isPartyOwned);
            return CardPresentation.FromDefinition(card.Def, ArtFor, name, color, isPartyOwned);
        }

        public CardPresentation For(ExecutionCardInstance card)
        {
            Resolve(card.OwnerId, card.Def.Side, out var name, out var color, out var isPartyOwned);
            return CardPresentation.From(card, ArtFor, name, color, isPartyOwned);
        }

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
        /// 적은 표현 없음, 소유자 없는 파티 카드만 isPartyOwned=true, 개별 소유는 이름은 채우되
        /// isPartyOwned는 false로 남는다(카드 테두리 표현이 갈린다).</summary>
        private void Resolve(
            string ownerId, Side side, out string name, out Color color, out bool isPartyOwned)
        {
            name = null;
            color = default;
            isPartyOwned = false;
            if (side == Side.Enemy)
            {
                return;
            }

            if (ownerId == null)
            {
                name = PlaytestKoreanText.PartyOwnerName();
                color = PartyOwnerColor;
                isPartyOwned = true;
                return;
            }

            name = _ownerName != null ? _ownerName(ownerId) : null;
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
