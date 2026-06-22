using System;
using System.Collections.Generic;
using UnityEngine;
using FateWeaver.Core.Events;
using FateWeaver.Core.Fate;
using FateWeaver.Core.Status;
using FateWeaver.Simulation;

namespace FateWeaver.Unity
{
    public sealed class FateWeaverPlaytestController : MonoBehaviour
    {
        private MultiTurnPlaytestSession _session;
        private MultiTurnScenario _currentScenario;
        private string _primaryCardId;
        private string _secondaryCardId;
        private string _message;
        private IReadOnlyList<ResolutionEvent> _timeline;
        private Vector2 _scroll;
        private Font _runtimeFont;

        private void Awake()
        {
            _runtimeFont = RuntimeOsFontLoader.LoadMalgunGothic(fontSize: 16);
            LoadScenario(SampleMultiTurnScenarios.All[0].Build());
        }

        private void OnDestroy()
        {
            if (_runtimeFont != null)
            {
                Destroy(_runtimeFont);
            }
        }

        private void OnGUI()
        {
            if (_runtimeFont != null)
            {
                GUI.skin.font = _runtimeFont;
            }

            GUI.backgroundColor = new Color(0.12f, 0.14f, 0.18f, 1f);
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none);
            GUI.backgroundColor = Color.white;

            GUILayout.BeginArea(new Rect(20, 20, Screen.width - 40, Screen.height - 40));
            _scroll = GUILayout.BeginScrollView(_scroll);

            GUILayout.Label("페이트 위버 - 다중 턴 플레이테스트", HeaderStyle());
            GUILayout.Label("카드를 선택해 미래를 조작하고, 턴을 실행한 뒤 다음 턴으로 진행하세요.");
            GUILayout.Space(8);

            DrawScenarioSelection();
            GUILayout.Space(8);
            DrawState();
            GUILayout.Space(8);
            DrawCards();
            GUILayout.Space(8);
            DrawActions();
            GUILayout.Space(8);
            DrawTimeline();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawScenarioSelection()
        {
            GUILayout.Label("시나리오", SectionStyle());
            GUILayout.BeginHorizontal();
            foreach (var entry in SampleMultiTurnScenarios.All)
            {
                if (GUILayout.Button(
                    PlaytestKoreanText.ScenarioName(entry.Id, entry.Id),
                    GUILayout.Height(30)))
                {
                    LoadScenario(entry.Build());
                }
            }

            GUILayout.EndHorizontal();
            GUILayout.Label("현재: "
                + PlaytestKoreanText.ScenarioName(_currentScenario.Id, _session.Name)
                + "    턴 " + (_session.TurnIndex + 1) + " / " + _session.TurnCount);
        }

        private void DrawState()
        {
            GUILayout.Label("전투 상태", SectionStyle());
            GUILayout.Label("플레이어 HP: " + _session.State.PlayerHp
                + "    운명력: " + _session.State.FateEnergy
                + "    " + StatusText(_session.State.PlayerStatuses));
            foreach (var enemy in _session.State.Enemies)
            {
                var enemyName = enemy.Id == "goblin" ? "고블린" : enemy.Id;
                GUILayout.Label(enemyName + " HP: " + enemy.Hp + "    " + StatusText(enemy.Statuses));
            }

            if (_session.IsComplete)
            {
                GUILayout.Label("결과: " + PlaytestKoreanText.OutcomeName(_session.Outcome), MessageStyle());
            }
        }

        private void DrawCards()
        {
            GUILayout.Label("미래 영역 발동 순서", SectionStyle());
            var index = 1;
            foreach (var card in _session.CurrentOrder)
            {
                var previousColor = GUI.backgroundColor;
                if (card.Def.Id == _primaryCardId)
                {
                    GUI.backgroundColor = new Color(0.95f, 0.72f, 0.25f);
                }
                else if (card.Def.Id == _secondaryCardId)
                {
                    GUI.backgroundColor = new Color(0.35f, 0.75f, 0.95f);
                }

                var label = index + ". " + PlaytestKoreanText.CardName(card.Def.Id, card.Def.Name)
                    + " [" + PlaytestKoreanText.SideName(card.Def.Side) + "]"
                    + "  주도력 " + card.Initiative
                    + (card.IsLocked ? "  고정됨" : string.Empty);
                if (GUILayout.Button(label, GUILayout.Height(36)))
                {
                    SelectCard(card.Def.Id);
                }

                GUI.backgroundColor = previousColor;
                index++;
            }

            GUILayout.Label("주 대상: " + SelectedCardName(_primaryCardId)
                + "    보조 대상: " + SelectedCardName(_secondaryCardId));
        }

        private void DrawActions()
        {
            GUILayout.Label("운명 액션 (비용 1)", SectionStyle());
            GUI.enabled = !_session.CurrentTurnResolved;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("주도력 -2", GUILayout.Height(32)))
            {
                Apply(new FateActionData(FateActionKeys.ChangeInitiative, 1, -2));
            }

            if (GUILayout.Button("주도력 +2", GUILayout.Height(32)))
            {
                Apply(new FateActionData(FateActionKeys.ChangeInitiative, 1, 2));
            }

            if (GUILayout.Button("선택 카드 주도력 교환", GUILayout.Height(32)))
            {
                Apply(new FateActionData(FateActionKeys.SwapInitiative, 1, 0), needsSecondary: true);
            }

            if (GUILayout.Button("주 대상 고정", GUILayout.Height(32)))
            {
                Apply(new FateActionData(FateActionKeys.Lock, 1, 0));
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUI.enabled = !_session.CurrentTurnResolved;
            if (GUILayout.Button("턴 실행", GUILayout.Height(42)))
            {
                _timeline = _session.ResolveTurn();
                _message = "턴 실행 완료.";
            }

            GUI.enabled = _session.CurrentTurnResolved && !_session.IsComplete;
            if (GUILayout.Button("다음 턴", GUILayout.Height(42)))
            {
                _session.AdvanceTurn();
                _primaryCardId = null;
                _secondaryCardId = null;
                _timeline = null;
                _message = (_session.TurnIndex + 1) + "턴 준비 완료.";
            }

            GUI.enabled = true;
            if (GUILayout.Button("시나리오 초기화", GUILayout.Height(42)))
            {
                LoadScenario(_currentScenario);
            }

            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_message))
            {
                GUILayout.Label(_message, MessageStyle());
            }
        }

        private void DrawTimeline()
        {
            if (_timeline == null)
            {
                return;
            }

            GUILayout.Label("해석 결과 (" + (_session.TurnIndex + 1) + "턴)", SectionStyle());
            foreach (var evt in _timeline)
            {
                if (evt is CardResolved card)
                {
                    GUILayout.Label("- " + PlaytestKoreanText.CardName(card.CardId, card.CardId)
                        + " | " + PlaytestKoreanText.ConditionName(card.ConditionTier)
                        + " | 피해 " + card.DamageDealt);
                }
                else if (evt is TurnEnded ended)
                {
                    GUILayout.Label("전투 결과: " + PlaytestKoreanText.OutcomeName(ended.Outcome));
                }
            }
        }

        private void SelectCard(string cardId)
        {
            if (_primaryCardId == cardId)
            {
                _primaryCardId = null;
                _secondaryCardId = null;
                return;
            }

            if (_primaryCardId == null || _secondaryCardId != null)
            {
                _primaryCardId = cardId;
                _secondaryCardId = null;
                return;
            }

            _secondaryCardId = cardId;
        }

        private void Apply(FateActionData action, bool needsSecondary = false)
        {
            if (_primaryCardId == null || (needsSecondary && _secondaryCardId == null))
            {
                _message = needsSecondary
                    ? "주 대상과 보조 대상을 선택하세요."
                    : "주 대상을 선택하세요.";
                return;
            }

            try
            {
                var result = _session.ApplyFateAction(
                    action,
                    _primaryCardId,
                    needsSecondary ? _secondaryCardId : null);
                _message = result.AppliedCount == 1
                    ? PlaytestKoreanText.FateActionName(action.Key) + " 적용 완료."
                    : "액션을 적용할 수 없습니다. 운명력, 고정 또는 대상 규칙을 확인하세요.";
            }
            catch (Exception exception)
            {
                _message = "오류: " + exception.Message;
            }
        }

        private void LoadScenario(MultiTurnScenario scenario)
        {
            _currentScenario = scenario;
            _session = new MultiTurnPlaytestSession(scenario);
            _primaryCardId = null;
            _secondaryCardId = null;
            _timeline = null;
            _message = "시나리오를 불러왔습니다.";
        }

        private static string StatusText(StatusBag bag)
        {
            var parts = new List<string>();
            foreach (var status in bag.All)
            {
                var amount = status.Magnitude > 0 ? status.Magnitude : status.Count;
                var statusName = PlaytestKoreanText.StatusName(status.Key);
                parts.Add(amount > 0 ? statusName + "(" + amount + ")" : statusName);
            }

            return parts.Count == 0 ? string.Empty : "[" + string.Join(", ", parts) + "]";
        }

        private string SelectedCardName(string cardId)
        {
            if (cardId == null)
            {
                return "-";
            }

            foreach (var card in _session.CurrentOrder)
            {
                if (card.Def.Id == cardId)
                {
                    return PlaytestKoreanText.CardName(card.Def.Id, card.Def.Name);
                }
            }

            return cardId;
        }

        private static GUIStyle HeaderStyle()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold
            };
            style.normal.textColor = Color.white;
            return style;
        }

        private static GUIStyle SectionStyle()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold
            };
            style.normal.textColor = new Color(0.4f, 0.85f, 1f);
            return style;
        }

        private static GUIStyle MessageStyle()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold
            };
            style.normal.textColor = new Color(1f, 0.82f, 0.3f);
            return style;
        }
    }
}
