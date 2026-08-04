using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using FateWeaver.Unity;
using NUnit.Framework;
using UnityEngine;

namespace FateWeaver.Tests.UnityEditMode
{
    /// <summary>표현 변환이 소유자 분기 셋(적 / 파티 공용 / 개별 캐릭터)을 옳게 가르는지 잠근다.
    /// 이름 조회가 델리게이트라 세션 없이 전 분기를 돌린다.</summary>
    public class BattlePresenterTests
    {
        private const string MemberId = "member_a";
        private static readonly Color MemberColor = new Color(0.2f, 0.4f, 0.6f, 1f);

        private BattlePresenter _presenter;
        private GameObject _go;
        private CharacterAsset _member;

        private static CardDefinition PlayerCard() => new CardDefinition(
            "probing_strike", "견제타", Side.Player, 4,
            new[] { new EffectData(EffectKeys.Damage, 4) })
            { EnergyCost = 1, Category = CardCategory.Execution };

        private static CardDefinition EnemyCard() => new CardDefinition(
            "goblin_jab", "잽", Side.Enemy, 5,
            new[] { new EffectData(EffectKeys.Damage, 1) })
            { EnergyCost = 0, Category = CardCategory.Execution };

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("presenter");
            _presenter = _go.AddComponent<BattlePresenter>();

            _member = ScriptableObject.CreateInstance<CharacterAsset>();
            var memberSo = new UnityEditor.SerializedObject(_member);
            memberSo.FindProperty("_id").stringValue = MemberId;
            memberSo.FindProperty("_color").colorValue = MemberColor;
            memberSo.ApplyModifiedPropertiesWithoutUndo();

            var presenterSo = new UnityEditor.SerializedObject(_presenter);
            var party = presenterSo.FindProperty("_party");
            party.arraySize = 1;
            party.GetArrayElementAtIndex(0).objectReferenceValue = _member;
            presenterSo.ApplyModifiedPropertiesWithoutUndo();

            _presenter.Initialize(id => id == MemberId ? "파티원 A" : null);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_member);
        }

        [Test]
        public void EnemyCardHasNoOwnerPresentation()
        {
            var card = new OwnedCard(EnemyCard(), null);

            var presentation = _presenter.For(card);

            Assert.IsFalse(presentation.IsPartyOwned);
            Assert.IsNull(presentation.OwnerDisplayName);
        }

        [Test]
        public void PartySharedCardUsesTheSharedOwnerName()
        {
            var card = new OwnedCard(PlayerCard(), null);

            var presentation = _presenter.For(card);

            Assert.IsTrue(presentation.IsPartyOwned);
            Assert.AreEqual(
                PlaytestKoreanText.PartyOwnerName(), presentation.OwnerDisplayName);
        }

        [Test]
        public void OwnedCardUsesTheCharacterNameAndColor()
        {
            var card = new OwnedCard(PlayerCard(), MemberId);

            var presentation = _presenter.For(card);

            Assert.AreEqual("파티원 A", presentation.OwnerDisplayName);
            Assert.AreEqual(MemberColor, presentation.OwnerColor);
            Assert.IsFalse(
                presentation.IsPartyOwned,
                "개별 소유 카드는 원본에서 isPartyOwned=false다.");
        }

        [Test]
        public void MissingArtCatalogResolvesToNullSprite()
        {
            var presentation = _presenter.For(new OwnedCard(EnemyCard(), null));

            Assert.IsNull(presentation.Art, "아트 카탈로그가 없으면 조용히 null이어야 한다.");
        }

        [Test]
        public void UnitTintFallsBackToTheSharedPartyColor()
        {
            // 카드 표현과 폴백이 갈린다 — 유닛 틴트는 캐릭터를 못 찾으면 공용 색을 쓴다.
            Assert.AreEqual(MemberColor, _presenter.OwnerColor(MemberId));
            Assert.AreEqual(
                _presenter.For(new OwnedCard(PlayerCard(), null)).OwnerColor,
                _presenter.OwnerColor("unknown_member"));
        }
    }
}
