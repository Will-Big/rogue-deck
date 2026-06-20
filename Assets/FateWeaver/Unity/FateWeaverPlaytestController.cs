using System;
using System.Collections.Generic;
using UnityEngine;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Events;
using FateWeaver.Core.Fate;
using FateWeaver.Simulation;

namespace FateWeaver.Unity
{
    public sealed class FateWeaverPlaytestController : MonoBehaviour
    {
        private PlaytestSession _session;
        private string _primaryCardId;
        private string _secondaryCardId;
        private string _message;
        private IReadOnlyList<ResolutionEvent> _timeline;
        private Vector2 _scroll;

        private void Awake()
        {
            LoadScenario(SampleScenarios.All[0].Build());
        }

        private void OnGUI()
        {
            GUI.backgroundColor = new Color(0.12f, 0.14f, 0.18f, 1f);
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none);
            GUI.backgroundColor = Color.white;

            GUILayout.BeginArea(new Rect(20, 20, Screen.width - 40, Screen.height - 40));
            _scroll = GUILayout.BeginScrollView(_scroll);

            GUILayout.Label("FATE WEAVER - CORE PLAYTEST", HeaderStyle());
            GUILayout.Label("Select cards, manipulate the future, then resolve the turn.");
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
            foreach (var entry in SampleScenarios.All)
            {
                if (GUILayout.Button(entry.Id, GUILayout.Height(30)))
                {
                    LoadScenario(entry.Build());
                }
            }

            GUILayout.EndHorizontal();
            GUILayout.Label("Current: " + _session.Scenario.Name);
        }

        private void DrawState()
        {
            GUILayout.Label("COMBAT STATE", SectionStyle());
            GUILayout.Label("Player HP: " + _session.State.PlayerHp
                + "    Fate Energy: " + _session.State.FateEnergy);
            foreach (var enemy in _session.State.Enemies)
            {
                GUILayout.Label(enemy.Id + " HP: " + enemy.Hp);
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
            GUI.enabled = !_session.IsResolved;
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
            if (GUILayout.Button("RESOLVE TURN", GUILayout.Height(42)))
            {
                _timeline = _session.Resolve();
                _message = "Turn resolved.";
            }

            GUI.enabled = true;
            if (GUILayout.Button("RESET SCENARIO", GUILayout.Height(42)))
            {
                LoadScenario(SampleScenarios.Find(_session.Scenario.Id));
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

            GUILayout.Label("RESOLUTION", SectionStyle());
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

        private void LoadScenario(ScenarioDefinition scenario)
        {
            _session = new PlaytestSession(scenario);
            _primaryCardId = null;
            _secondaryCardId = null;
            _timeline = null;
            _message = "Scenario loaded.";
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
