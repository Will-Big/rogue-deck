using System;
using System.Collections.Generic;
using UnityEngine;

namespace FateWeaver.Unity
{
    /// <summary>덱 파일 셋. 내용은 지연 평가 제공자로 한 번 꽂고(Bind), 이후에는 개수만
    /// 갱신한다(Refresh). 선택 중에는 입력을 막는다.</summary>
    public sealed class BattlePilesView : MonoBehaviour
    {
        [SerializeField] private PileView _drawPile;
        [SerializeField] private PileView _discardPile;
        [SerializeField] private PileView _fullDeck;

        public bool IsBound => _drawPile != null && _discardPile != null && _fullDeck != null;

        public void Bind(
            Func<IReadOnlyList<CardPresentation>> draw,
            Func<IReadOnlyList<CardPresentation>> discard,
            Func<IReadOnlyList<CardPresentation>> full)
        {
            _drawPile.Bind(draw);
            _discardPile.Bind(discard);
            _fullDeck.Bind(full);
        }

        public void Refresh(int drawCount, int discardCount, int fullCount)
        {
            _drawPile.SetCount(drawCount);
            _discardPile.SetCount(discardCount);
            _fullDeck.SetCount(fullCount);
        }

        public void SetInputEnabled(bool value)
        {
            _drawPile.SetInputEnabled(value);
            _discardPile.SetInputEnabled(value);
            _fullDeck.SetInputEnabled(value);
        }
    }
}
