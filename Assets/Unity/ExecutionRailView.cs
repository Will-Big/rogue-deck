using System;
using System.Collections.Generic;
using DG.Tweening;
using FateWeaver.Simulation.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace FateWeaver.Unity
{
    /// <summary>The execution rail: a horizontally scrollable strip of RailCardViews in resolution order
    /// (spec §2 — the rail can hold many cards). Hovering a card shows the full CardView preview on the
    /// overlay layer since mini cards carry no rules text (spec §3).</summary>
    public sealed class ExecutionRailView : MonoBehaviour
    {
        [SerializeField] private ScrollRect _scrollRect;
        [SerializeField] private RectTransform _content;
        [SerializeField] private CardView _previewPrefab;
        [SerializeField] private RailCardView _cardPrefab;
        [SerializeField] private RectTransform _previewLayer;
        [SerializeField] private Image _backdrop;
        [SerializeField] private float _placementFlightDuration = 0.45f;
        [SerializeField, Range(0.1f, 0.95f)]
        private float _placementFlightRiseRatio = 0.7f;
        [SerializeField, Min(0f)]
        private float _placementFlightOvershootRatio = 0.9f;
        [SerializeField, Min(0.1f)]
        private float _placementFlightApproachWidthRatio = 1.25f;
        [SerializeField, Range(0.05f, 0.45f)]
        private float _placementFlightApproachDropRatio = 0.3f;
        [SerializeField, Range(0.5f, 0.9f)]
        private float _placementFlightCurveSplit = 0.72f;

        private static readonly Vector2 CardSize = new Vector2(96f, 132f);
        private static readonly Vector2 PreviewSize = new Vector2(200f, 280f);
        private static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.25f);
        private static readonly Color DropHintColor = new Color(0.95f, 0.72f, 0.25f, 0.14f);
        private const float PlacementPreviewAlpha = 0.5f;
        private const float PlacementPulseScale = 1.06f;
        private const float PlacementPulseHalfDuration = 0.45f;

        private readonly List<RailCardView> _views = new List<RailCardView>();
        private CardView _preview;
        private RailCardView _placementPreview;
        private CanvasGroup _placementPreviewGroup;
        private CardPresentation? _placementPreviewCard;
        private int _placementPreviewIndex = -1;
        private Tween _placementPreviewTween;
        private Sequence _placementFlightSequence;

        public void SetDropHint(bool value)
        {
            if (_backdrop != null)
            {
                _backdrop.color = value ? DropHintColor : BackdropColor;
            }
        }

        public void SetPickedTargets(IReadOnlyList<int> picked)
        {
            for (int i = 0; i < _views.Count; i++)
            {
                bool isPicked = false;
                if (picked != null)
                {
                    for (int p = 0; p < picked.Count; p++)
                    {
                        if (picked[p] == i)
                        {
                            isPicked = true;
                            break;
                        }
                    }
                }

                _views[i].SetSelection(
                    isPicked ? CardView.SelectionKind.Secondary : CardView.SelectionKind.None);
            }
        }

        public void SetTargetSelection(
            bool active,
            IReadOnlyCollection<SelectionTargetRef> candidates,
            IReadOnlyList<SelectionTargetRef> pickedTargets)
        {
            for (int i = 0; i < _views.Count; i++)
            {
                var target = SelectionTargetRef.ExecutionCard(i);
                bool candidate = Contains(candidates, target);
                bool selected = active && IndexOf(pickedTargets, target) >= 0;
                _views[i].SetTargetSelection(active, candidate, selected);
                _views[i].SetInteractable(!active || candidate);
            }
        }

        /// <summary>Editor-time construction (called by BattleSceneBuilder); the built children and
        /// references serialize into the scene.</summary>
        public void EditorBuild(CardView previewPrefab, RailCardView cardPrefab, RectTransform previewLayer)
        {
            _previewPrefab = previewPrefab;
            _cardPrefab = cardPrefab;
            _previewLayer = previewLayer;

            var rect = (RectTransform)transform;
            _scrollRect = gameObject.AddComponent<ScrollRect>();

            var viewport = BattleUiKit.Rect(rect, "Viewport");
            BattleUiKit.Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            _backdrop = viewport.gameObject.AddComponent<Image>();
            _backdrop.color = BackdropColor;

            var content = BattleUiKit.Rect(viewport, "Content");
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 0.5f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            var layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.padding = new RectOffset(16, 16, 10, 10);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            _scrollRect.viewport = viewport;
            _scrollRect.content = content;
            _scrollRect.horizontal = true;
            _scrollRect.vertical = false;
            _scrollRect.movementType = ScrollRect.MovementType.Clamped;
            _scrollRect.scrollSensitivity = 30f;

            _content = content;
        }

        public void SetCards(IReadOnlyList<CardPresentation> cards, Action<int> onClick)
        {
            ClearPlacementPreview();
            HidePreview();
            foreach (var view in _views)
            {
                Destroy(view.gameObject);
            }

            _views.Clear();
            for (int i = 0; i < cards.Count; i++)
            {
                var view = Instantiate(_cardPrefab, _content);
                ((RectTransform)view.transform).sizeDelta = CardSize;
                int captured = i;
                var data = cards[i];
                view.Bind(data, () => onClick?.Invoke(captured), hovering => OnHover(view, data, hovering));
                _views.Add(view);
            }
        }

        public void ShowPlacementHover(CardPresentation card, int insertionIndex)
        {
            if (insertionIndex < 0 || insertionIndex > _views.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(insertionIndex));
            }

            _placementPreviewCard = card;
            _placementPreviewIndex = insertionIndex;
            EnsurePlacementPreview();
            StopPlacementPulse();
            BindPlacementPreview(null, interactable: false);
            ShowPlacementPreview();
            HidePreview();
        }

        public void ArmPlacementPreview(Action onClick)
        {
            if (!_placementPreviewCard.HasValue || _placementPreview == null)
            {
                throw new InvalidOperationException(
                    "Placement hover preview must exist before it can be armed.");
            }

            BindPlacementPreview(onClick, interactable: true);
            StartPlacementPulse();
        }

        public bool TryGetPlacementFlightLayer(out RectTransform layer)
        {
            layer = _previewLayer;
            return layer != null
                && _placementPreview != null
                && _placementPreview.gameObject.activeSelf;
        }

        public bool StartPlacementFlight(RectTransform flight, Action onComplete)
        {
            if (flight == null
                || _placementPreview == null
                || !_placementPreview.gameObject.activeSelf
                || _previewLayer == null)
            {
                return false;
            }

            StopPlacementPulse();
            StopPlacementFlight();
            _placementPreview.SetInteractable(false);
            _placementPreviewGroup.interactable = false;
            _placementPreviewGroup.blocksRaycasts = false;
            _placementPreviewGroup.alpha = 0f;

            var target = (RectTransform)_placementPreview.transform;
            Vector3 startLocal = flight.localPosition;
            Vector3 targetLocal3 = _previewLayer.InverseTransformPoint(target.position);
            Vector2 targetSize = SizeInLayer(target, _previewLayer);
            var settings = new PlacementFlightPath.Settings(
                _placementFlightRiseRatio,
                _placementFlightOvershootRatio,
                _placementFlightApproachWidthRatio,
                _placementFlightApproachDropRatio);
            var path = PlacementFlightPath.Create(
                new Vector2(startLocal.x, startLocal.y),
                new Vector2(targetLocal3.x, targetLocal3.y),
                targetSize,
                settings);
            RailCardView miniFace = CreateFlightMiniFace(flight);
            Vector3 endScale = ScaleForTarget(flight, target, _previewLayer.lossyScale);
            float progress = 0f;
            bool completionSent = false;
            Action finish = () =>
            {
                if (completionSent)
                {
                    return;
                }

                completionSent = true;
                _placementFlightSequence = null;
                onComplete?.Invoke();
            };

            var movement = DOTween.To(
                    () => progress,
                    value =>
                    {
                        progress = value;
                        var sample = PlacementFlightPath.Evaluate(
                            path, value, _placementFlightCurveSplit);
                        flight.localPosition = new Vector3(
                            sample.Position.x, sample.Position.y, startLocal.z);
                        float settleT = PlacementFlightPath.SettleProgress(
                            value, _placementFlightCurveSplit);
                        if (settleT >= PlacementFlightPath.FlipSwapProgress
                            && !miniFace.gameObject.activeSelf)
                        {
                            miniFace.gameObject.SetActive(true);
                        }

                        flight.localRotation = Quaternion.Euler(
                            0f,
                            PlacementFlightPath.FlipAngle(settleT),
                            sample.AngleDegrees);
                    },
                    1f,
                    _placementFlightDuration)
                .SetEase(Ease.InOutCubic);

            _placementFlightSequence = DOTween.Sequence()
                .Append(movement)
                .Join(flight.DOScale(endScale, _placementFlightDuration)
                    .SetEase(Ease.InOutCubic))
                .AppendCallback(() =>
                {
                    flight.SetPositionAndRotation(target.position, target.rotation);
                    flight.localScale = endScale;
                })
                .SetUpdate(true)
                .SetLink(flight.gameObject, LinkBehaviour.KillOnDestroy)
                .OnComplete(() => finish())
                .OnKill(() => finish());
            return true;
        }

        public void ClearPlacementPreview()
        {
            StopPlacementFlight();
            StopPlacementPulse();
            _placementPreviewCard = null;
            _placementPreviewIndex = -1;
            if (_placementPreview != null)
            {
                _placementPreviewGroup.alpha = PlacementPreviewAlpha;
                _placementPreviewGroup.interactable = false;
                _placementPreviewGroup.blocksRaycasts = false;
                _placementPreview.SetInteractable(false);
                _placementPreview.gameObject.SetActive(false);
            }
        }

        public void SetSelection(int index, CardView.SelectionKind kind)
        {
            for (int i = 0; i < _views.Count; i++)
            {
                _views[i].SetSelection(i == index ? kind : CardView.SelectionKind.None);
            }
        }

        public void SetInputEnabled(bool value)
        {
            if (_scrollRect != null)
            {
                _scrollRect.enabled = value;
            }

            if (!value)
            {
                HidePreview();
            }

            foreach (var view in _views)
            {
                view.SetInteractable(value);
            }
        }

        private void OnHover(RailCardView source, CardPresentation data, bool hovering)
        {
            if (!hovering)
            {
                HidePreview();
                return;
            }

            if (_previewPrefab == null || _previewLayer == null)
            {
                return;
            }

            if (_preview == null)
            {
                _preview = Instantiate(_previewPrefab, _previewLayer);
                var previewRect = (RectTransform)_preview.transform;
                previewRect.anchorMin = previewRect.anchorMax = new Vector2(0.5f, 0.5f);
                previewRect.sizeDelta = PreviewSize;
                foreach (var graphic in _preview.GetComponentsInChildren<Graphic>(true))
                {
                    graphic.raycastTarget = false;
                }
            }

            _preview.gameObject.SetActive(true);
            _preview.Bind(data, null);

            var screen = RectTransformUtility.WorldToScreenPoint(null, source.transform.position);
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_previewLayer, screen, null, out local);
            local.y += CardSize.y * 0.5f + PreviewSize.y * 0.5f + 14f;
            float maxX = _previewLayer.rect.width * 0.5f - PreviewSize.x * 0.5f - 8f;
            local.x = Mathf.Clamp(local.x, -maxX, maxX);
            ((RectTransform)_preview.transform).anchoredPosition = local;
        }

        private void EnsurePlacementPreview()
        {
            if (_placementPreview != null)
            {
                return;
            }

            _placementPreview = Instantiate(_cardPrefab, _content);
            ((RectTransform)_placementPreview.transform).sizeDelta = CardSize;
            _placementPreviewGroup = _placementPreview.gameObject.AddComponent<CanvasGroup>();
            _placementPreviewGroup.alpha = PlacementPreviewAlpha;
            _placementPreview.gameObject.SetActive(false);
        }

        private void BindPlacementPreview(Action onClick, bool interactable)
        {
            _placementPreview.Bind(_placementPreviewCard.Value, onClick, null);
            _placementPreview.SetSelection(CardView.SelectionKind.Secondary);
            _placementPreview.SetInteractable(interactable);
            _placementPreviewGroup.interactable = interactable;
            _placementPreviewGroup.blocksRaycasts = interactable;
        }

        private void ShowPlacementPreview()
        {
            _placementPreview.transform.SetSiblingIndex(_placementPreviewIndex);
            _placementPreview.gameObject.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
        }

        private void StartPlacementPulse()
        {
            StopPlacementPulse();
            _placementPreviewTween = _placementPreview.transform
                .DOScale(Vector3.one * PlacementPulseScale, PlacementPulseHalfDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true)
                .SetLink(_placementPreview.gameObject, LinkBehaviour.KillOnDestroy);
        }

        private void StopPlacementPulse()
        {
            if (_placementPreviewTween != null)
            {
                _placementPreviewTween.Kill();
                _placementPreviewTween = null;
            }

            if (_placementPreview != null)
            {
                _placementPreview.transform.localScale = Vector3.one;
            }
        }

        private void StopPlacementFlight()
        {
            if (_placementFlightSequence == null)
            {
                return;
            }

            _placementFlightSequence.Kill();
            _placementFlightSequence = null;
        }

        private static Vector3 ScaleForTarget(
            RectTransform flight,
            RectTransform target,
            Vector3 parentScale)
            => new Vector3(
                target.rect.width * target.lossyScale.x /
                    (flight.rect.width * parentScale.x),
                target.rect.height * target.lossyScale.y /
                    (flight.rect.height * parentScale.y),
                1f);

        /// <summary>The mini card face shown after the flip passes edge-on. A child of the
        /// flight rect, so ClearPlacementFlight destroys it with the flight visual.</summary>
        private RailCardView CreateFlightMiniFace(RectTransform flight)
        {
            var face = Instantiate(_cardPrefab, flight);
            var faceRect = (RectTransform)face.transform;
            faceRect.anchorMin = Vector2.zero;
            faceRect.anchorMax = Vector2.one;
            faceRect.offsetMin = Vector2.zero;
            faceRect.offsetMax = Vector2.zero;
            face.Bind(_placementPreviewCard.Value, null, null);
            face.SetInteractable(false);
            foreach (var graphic in face.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }

            face.gameObject.SetActive(false);
            return face;
        }

        private static Vector2 SizeInLayer(
            RectTransform target,
            RectTransform layer)
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector3 bottomLeft = layer.InverseTransformPoint(corners[0]);
            Vector3 topRight = layer.InverseTransformPoint(corners[2]);
            return new Vector2(
                Mathf.Abs(topRight.x - bottomLeft.x),
                Mathf.Abs(topRight.y - bottomLeft.y));
        }

        private void HidePreview()
        {
            if (_preview != null)
            {
                _preview.gameObject.SetActive(false);
            }
        }

        private static bool Contains(
            IReadOnlyCollection<SelectionTargetRef> targets,
            SelectionTargetRef target)
        {
            if (targets == null)
            {
                return false;
            }

            foreach (var candidate in targets)
            {
                if (candidate.Equals(target))
                {
                    return true;
                }
            }

            return false;
        }

        private static int IndexOf(
            IReadOnlyList<SelectionTargetRef> targets,
            SelectionTargetRef target)
        {
            if (targets == null)
            {
                return -1;
            }

            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i].Equals(target))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
