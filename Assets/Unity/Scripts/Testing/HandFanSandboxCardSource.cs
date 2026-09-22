using System;
using System.Collections.Generic;
using FateWeaver.Core.Authoring;
using FateWeaver.Simulation.Descriptions;
using UnityEngine;

namespace FateWeaver.Unity
{
    /// <summary>인스펙터의 콘텐츠 키를 기존 JSON과 설명 컴포저로 표시 샘플로 변환한다.</summary>
    public sealed class HandFanSandboxCardSource : MonoBehaviour
    {
        [SerializeField] private string[] _cardIds = Array.Empty<string>();
        [SerializeField] private CardArtCatalog _artCatalog;

        public bool TryLoad(out IReadOnlyList<CardPresentation> samples, out string error)
        {
            samples = Array.Empty<CardPresentation>();
            error = string.Empty;
            if (_cardIds == null || _cardIds.Length == 0)
            {
                error = "테스트 카드 ID 목록을 인스펙터에서 지정하세요.";
                return false;
            }

            var loaded = ContentBootstrap.Load(UnityContentRoot.Path);
            if (!loaded.Succeeded)
            {
                error = "콘텐츠 로드 실패:\n" + string.Join("\n", loaded.Errors);
                return false;
            }

            foreach (var id in _cardIds)
            {
                if (string.IsNullOrWhiteSpace(id) || !loaded.Content.Cards.Cards.ContainsKey(id))
                {
                    error = "테스트 카드 ID를 찾을 수 없습니다: " + (id ?? "(비어 있음)");
                    return false;
                }
            }

            var korean = KoreanDescriptionCatalog.CreateDefault(loaded.Content.Statuses);
            Func<string, Sprite> art = _artCatalog != null ? _artCatalog.ArtFor : (Func<string, Sprite>)null;
            var result = new CardPresentation[_cardIds.Length];
            for (int i = 0; i < _cardIds.Length; i++)
                result[i] = CardPresentation.FromDefinition(loaded.Content.Cards.Get(_cardIds[i]), korean, art);
            samples = Array.AsReadOnly(result);
            return true;
        }
    }
}
