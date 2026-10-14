using System;
using System.Collections.Generic;
using FateWeaver.Core.Authoring;
using FateWeaver.Simulation.Descriptions;
using UnityEngine;

namespace FateWeaver.Unity
{
    /// <summary>인스펙터의 콘텐츠 키를 기존 JSON과 설명 컴포저, 현지화로 표시 샘플로 변환한다.</summary>
    public sealed class HandFanSandboxCardSource : MonoBehaviour
    {
        [SerializeField] private string[] _cardIds = Array.Empty<string>();
        [SerializeField] private CardArtCatalog _artCatalog;
        [SerializeField] private GameLocalization _localization;

        /// <summary>현지화 테이블을 아직 불러오는 중이면 true. 끝났거나(실패 포함) 배선이 비었으면
        /// false다 — 그 경우는 TryLoad가 오류로 알린다.</summary>
        public bool IsLoadingText => _localization != null && _localization.Renderer == null
            && _localization.Initialization != null && !_localization.Initialization.IsCompleted;

        public bool TryLoad(out IReadOnlyList<CardPresentation> samples, out string error)
        {
            samples = Array.Empty<CardPresentation>();
            error = string.Empty;
            if (_cardIds == null || _cardIds.Length == 0)
            {
                error = "테스트 카드 ID 목록을 인스펙터에서 지정하세요.";
                return false;
            }

            if (_localization == null || _localization.Renderer == null)
            {
                error = "현지화가 준비되지 않았습니다. GameLocalization 참조를 확인하세요.";
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

            var requests = DescriptionRequestCatalog.CreateDefault(loaded.Content.Statuses);
            var result = new CardPresentation[_cardIds.Length];
            for (int i = 0; i < _cardIds.Length; i++)
            {
                var def = loaded.Content.Cards.Get(_cardIds[i]);
                result[i] = CardPresentation.FromLocalized(def, requests, _localization,
                    _artCatalog != null ? _artCatalog.ArtFor(def.Id) : null, def.BaseExecutionOrder,
                    false, null, default, false);
            }
            samples = Array.AsReadOnly(result);
            return true;
        }
    }
}
