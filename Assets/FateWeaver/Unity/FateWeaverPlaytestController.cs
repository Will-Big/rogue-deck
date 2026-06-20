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

        private void Awake()
        {
            LoadScenario(SampleMultiTurnScenarios.All[0].Build());
        }

        private void OnGUI()
        {
            GUI.backgroundColor = new Color(0.12f, 0.14f, 0.18f, 1f);
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none);
            GUI.backgroundColor = Color.white;

            GUILayout.BeginArea(new Rect(20, 20, Screen.width - 40, Screen.height - 40));
            _scroll = GUILayout.BeginScrollView(_scroll);

            GUILayout.Label("FATE WEAVER - MULTI-TURN PLAYTEST", HeaderStyle());
            GUILayout.Label("Select cards, manipulate the future, resolve, then advance the turn.");
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
            GUILayout.Label("SCENARIO", SectionStyle());
            GUILayout.BeginHorizontal();
            foreach (var entry in SampleMultiTurnScenarios.All)
            {
                if (GUILayout.Button(entry.Id, GUILayout.Height(30)))
                {
                    LoadScenario(entry.Build());
                }
            }

            GUILayout.EndHorizontal();
            GUILayout.Label("Current: " + _session.Name
                + "    Turn " + (_session.TurnIndex + 1) + " / " + _session.TurnCount);
        }

        private void DrawState()
        {
            GUILayout.Label("COMBAT STATE", SectionStyle());
            GUILayout.Label("Player HP: " + _session.State.PlayerHp
                + "    Fate Energy: " + _session.State.FateEnergy
                + "    " + StatusText(_session.State.PlayerStatuses));
            foreach (var enemy in _session.State.Enemies)
            {
                GUILayout.Label(enemy.Id + " HP: " + enemy.Hp + "    " + StatusText(enemy.Statuses));
            }

            if (_session.IsComplete)
            {
                GUILayout.Label("RESULT: " + _session.Outcome, MessageStyle());
            }
        }

        private void DrawCards()
        {
            GUILayout.Label("FUTURE ORDER", SectionStyle());
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

                var label = index + ". " + card.Def.Name
                    + " [" + card.Def.Side + "]"
                    + "  initiative " + card.Initiative
                    + (card.IsLocked ? "  LOCKED" : string.Empty);
                if (GUILayout.Button(label, GUILayout.Height(36)))
                {
                    SelectCard(card.Def.Id);
                }

                GUI.backgroundColor = previousColor;
                index++;
            }

            GUILayout.Label("Primary: " + (_primaryCardId ?? "-")
                + "    Secondary: " + (_secondaryCardId ?? "-"));
        }

        private void DrawActions()
        {
            GUILayout.Label("FATE ACTIONS (cost 1)", SectionStyle());
            GUI.enabled = !_session.CurrentTurnResolved;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Initiative -2", GUILayout.Height(32)))
            {
                Apply(new FateActionData(FateActionKeys.ChangeInitiative, 1, -2));
            }

            if (GUILayout.Button("Initiative +2", GUILayout.Height(32)))
            {
                Apply(new FateActionData(FateActionKeys.ChangeInitiative, 1, 2));
            }

            if (GUILayout.Button("Swap Selected", GUILayout.Height(32)))
            {
                Apply(new FateActionData(FateActionKeys.SwapInitiative, 1, 0), needsSecondary: true);
            }

            if (GUILayout.Button("Lock Primary", GUILayout.Height(32)))
            {
                Apply(new FateActionData(FateActionKeys.Lock, 1, 0));
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUI.enabled = !_session.CurrentTurnResolved;
            if (GUILayout.Button("RESOLVE TURN", GUILayout.Height(42)))
            {
                _timeline = _session.ResolveTurn();
                _message = "Turn resolved.";
            }

            GUI.enabled = _session.CurrentTurnResolved && !_session.IsComplete;
            if (GUILayout.Button("NEXT TURN", GUILayout.Height(42)))
            {
                _session.AdvanceTurn();
                _primaryCardId = null;
                _secondaryCardId = null;
                _timeline = null;
                _message = "Turn " + (_session.TurnIndex + 1) + " ready.";
            }

            GUI.enabled = true;
            if (GUILayout.Button("RESET SCENARIO", GUILayout.Height(42)))
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

            GUILayout.Label("RESOLUTION (Turn " + (_session.TurnIndex + 1) + ")", SectionStyle());
            foreach (var evt in _timeline)
            {
                if (evt is CardResolved card)
                {
                    GUILayout.Label("- " + card.CardId
                        + " | " + card.ConditionTier
                        + " | damage " + card.DamageDealt);
                }
                else if (evt is TurnEnded ended)
                {
                    GUILayout.Label("Outcome: " + ended.Outcome);
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
                    ? "Select a primary and secondary card."
                    : "Select a primary card.";
                return;
            }

            try
            {
                var result = _session.ApplyFateAction(
                    action,
                    _primaryCardId,
                    needsSecondary ? _secondaryCardId : null);
                _message = result.AppliedCount == 1
                    ? "Applied " + action.Key + "."
                    : "Action rejected (energy, lock, or target rule).";
            }
            catch (Exception exception)
            {
                _message = exception.Message;
            }
        }

        private void LoadScenario(MultiTurnScenario scenario)
        {
            _currentScenario = scenario;
            _session = new MultiTurnPlaytestSession(scenario);
            _primaryCardId = null;
            _secondaryCardId = null;
            _timeline = null;
            _message = "Scenario loaded.";
        }

        private static string StatusText(StatusBag bag)
        {
            var parts = new List<string>();
            foreach (var status in bag.All)
            {
                var amount = status.Magnitude > 0 ? status.Magnitude : status.Count;
                parts.Add(amount > 0 ? status.Key + "(" + amount + ")" : status.Key.ToString());
            }

            return parts.Count == 0 ? string.Empty : "[" + string.Join(", ", parts) + "]";
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
