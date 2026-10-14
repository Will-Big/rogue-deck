using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using FateWeaver.Simulation;
using FateWeaver.Unity;
using FateWeaver.Core.Status;
using FateWeaver.Core.Cards;
using FateWeaver.Simulation.Descriptions;
using FateWeaver.Simulation.Run;
using UnityEngine.EventSystems;
namespace FateWeaver.Tests.UnityPlayMode
{
    public class LocalizationPlayModeTests
    {
        private const string PreferenceKey = "fateweaver.locale";
        [UnityTest]
        public IEnumerator Language_buttons_update_open_cards_and_preserve_combat_and_random_stream()
        {
            var hadPreference = PlayerPrefs.HasKey(PreferenceKey);
            var originalPreference = PlayerPrefs.GetString(PreferenceKey);
            try
            {
                PlayerPrefs.SetString(PreferenceKey, "ko");
                yield return SceneManager.LoadSceneAsync("FateWeaverBattle");
                var scene = SceneManager.GetActiveScene();
                T Find<T>() where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).First();
                var localization = Find<GameLocalization>();
                yield return new WaitUntil(() => localization.Initialization != null && localization.Initialization.IsCompleted);
                Assert.IsFalse(localization.Initialization.IsFaulted);
                // Localization may already be initialized from an earlier play session's saved choice.
                LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale("ko");
                yield return new WaitUntil(() => localization.Name("status.poison.name") == "독");
                var flow = Find<CombatNodeFlow>(); var battle = Find<BattleScreenController>();
                yield return new WaitUntil(() => Get<DeckCombatSession>(battle, "_session") != null);
                var session = Get<DeckCombatSession>(battle, "_session");
                var hand = session.Hand.Select(c => c.Def.Id).ToArray();
                var energy = session.FateEnergy; var turn = session.TurnIndex;
                var pile = Find<PileView>();
                typeof(PileView).GetMethod("Open", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pile, null);
                var popup = Get<GameObject>(pile, "_popup");
                Assert.IsTrue(popup.activeSelf);
                var card = popup.GetComponentInChildren<CardView>();
                var cardName = Get<TMP_Text>(card, "_nameText");
                var koreanName = cardName.text;
                var unit = Find<UnitView>();
                var displayStatus = new StatusInstance(StatusKeys.Poison, StatusLifetime.Permanent, 1);
                unit.SetStatuses(new[] { displayStatus }, key => localization.Name("status." + key.Id + ".name"));
                displayStatus.Magnitude = 99;
                var reward = Find<RewardChoiceView>();
                reward.Show(new[] { new RewardCandidate(session.Hand[0].Def, "member_a") }, id => id, _ => { }, () => { });
                var definition = session.Hand.First(c => c.Def.Category == CardCategory.Execution).Def;
                var prefabCatalog = Get<CardPrefabCatalog>(pile, "_cardPrefabs");
                var lockedData = CardPresentation.FromLocalized(definition,
                    DescriptionRequestCatalog.CreateDefault(FateWeaver.Core.Authoring.ContentBootstrap.Load(UnityContentRoot.Path).Content.Statuses),
                    localization, null, definition.BaseExecutionOrder, true, "member_a", Color.white, false);
                var lockedView = prefabCatalog.Create(lockedData, (RectTransform)popup.transform);
                lockedView.Bind(lockedData, null);
                var icon = lockedView.GetComponentInChildren<CardStatusIconView>(true);
                icon.OnPointerEnter(new PointerEventData(EventSystem.current) { position = new Vector2(200, 200) });
                var tooltip = Get<CardStatusTooltipView>(icon, "_tooltip");
                Assert.IsTrue(tooltip.gameObject.activeSelf);
                Assert.AreEqual("고정", Get<TMP_Text>(tooltip, "_titleText").text);
                // Rebinding recreates description lines, so read the current heading each time.
                string Heading() => lockedView.GetComponentsInChildren<DescriptionLineView>()
                    .Select(line => Get<TMP_Text>(line, "_headingText")).First(text => text.gameObject.activeSelf).text;
                var koreanHeading = Heading();
                StringAssert.IsMatch("[가-힣]", koreanHeading);
                var languages = Find<LanguageSelectionView>();
                Get<Button>(languages, "_english").onClick.Invoke();
                yield return new WaitUntil(() => localization.Name("status.poison.name") == "Poison");
                Assert.AreEqual("en", PlayerPrefs.GetString(PreferenceKey));
                Assert.AreNotEqual(koreanName, cardName.text);
                Assert.AreEqual("Poison(1)", Get<TMP_Text>(unit, "_statusText").text,
                    "Locale change revealed a mutable future status value.");
                Assert.IsTrue(tooltip.gameObject.activeSelf);
                Assert.AreEqual("Locked", Get<TMP_Text>(tooltip, "_titleText").text);
                Assert.AreNotEqual(koreanHeading, Heading());
                StringAssert.DoesNotMatch("[가-힣]", Heading(), "Target heading stayed Korean in English.");
                Assert.AreEqual("Party Member A", Get<TMP_Text[]>(reward, "_ownerLabels")[0].text);
                StringAssert.Contains("\n", Get<TMP_Text>(pile, "_labelText").text);
                Assert.IsTrue(popup.activeSelf, "Locale change must not close the pile.");
                Assert.AreSame(session, Get<DeckCombatSession>(battle, "_session"));
                CollectionAssert.AreEqual(hand, session.Hand.Select(c => c.Def.Id).ToArray());
                Assert.AreEqual(energy, session.FateEnergy); Assert.AreEqual(turn, session.TurnIndex);
                // Rapid selection must publish only the final selected locale.
                Get<Button>(languages, "_korean").onClick.Invoke();
                Get<Button>(languages, "_english").onClick.Invoke();
                Get<Button>(languages, "_korean").onClick.Invoke();
                yield return new WaitUntil(() => localization.Name("status.poison.name") == "독");
                yield return null;
                Assert.AreEqual("ko", LocalizationSettings.SelectedLocale.Identifier.Code);
                Assert.AreEqual("ko", PlayerPrefs.GetString(PreferenceKey));
                Assert.AreEqual(koreanName, cardName.text);
                Assert.AreEqual(koreanHeading, Heading());
                var afterLanguageChange = session.State.Rng.Next();
                typeof(CombatNodeFlow).GetMethod("NewRun", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(flow, null);
                var fresh = Get<DeckCombatSession>(battle, "_session");
                Assert.AreEqual(afterLanguageChange, fresh.State.Rng.Next(), "Language changes consumed the seeded RNG.");
            }
            finally
            {
                if (hadPreference) PlayerPrefs.SetString(PreferenceKey, originalPreference); else PlayerPrefs.DeleteKey(PreferenceKey);
            }
        }
        private static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }
}
