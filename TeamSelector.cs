using HarmonyLib;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PvPHelper
{
    internal sealed class TeamSelector : MonoBehaviour
    {
        private CharacterSelectController controller = null!;
        private MPButton button = null!;
        private TMP_Text label = null!;
        private RectTransform source = null!;
        private RectTransform rect = null!;
        private Navigation readyNavigation;
        private Navigation unreadyNavigation;
        private NetworkUser? requestingUser;
        private byte requestedChoice;
        private bool awaitingChoice;
        private static readonly Color[] colors =
        {
            new Color(1f, .4f, .4f), new Color(.4f, .7f, 1f),
            new Color(.4f, 1f, .5f), new Color(1f, .9f, .3f)
        };

        private void Start()
        {
            controller = GetComponent<CharacterSelectController>();
            if (!controller.readyButton) { enabled = false; return; }
            source = (RectTransform)controller.readyButton.transform;
            button = Instantiate(controller.readyButton, source.parent);
            button.name = "PvPHelperTeamSelector";
            button.gameObject.SetActive(false);
            button.onClick = new Button.ButtonClickedEvent();
            button.onFindSelectableLeft = new UnityEvent();
            button.onFindSelectableRight = new UnityEvent();
            button.onSelect = new UnityEvent();
            button.onDeselect = new UnityEvent();
            button.onDebugSelect = new UnityEvent();
            button.defaultFallbackButton = false;
            if (button is HGButton hg) hg.updateTextOnHover = false;
            foreach (LanguageTextMeshController text in button.GetComponentsInChildren<LanguageTextMeshController>(true))
                text.enabled = false;
            label = button.GetComponentInChildren<TMP_Text>(true);
            if (!label) { Destroy(button.gameObject); enabled = false; return; }
            rect = (RectTransform)button.transform;
            var layout = button.GetComponent<LayoutElement>() ?? button.gameObject.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
            button.onClick.AddListener(Cycle);
            readyNavigation = controller.readyButton.navigation;
            unreadyNavigation = controller.unreadyButton ? controller.unreadyButton.navigation : default;
            button.gameObject.SetActive(true);
        }

        private void Cycle()
        {
            NetworkUser? user = GetUser();
            if (user is null || !user) return;
            byte current = awaitingChoice && requestingUser == user ? requestedChoice : PlayerTeams.GetChoice(user);
            requestingUser = user;
            requestedChoice = (byte)((current + 1) % PlayerTeams.Teams.Length);
            awaitingChoice = true;
            PlayerTeams.Select(user, requestedChoice);
        }

        private NetworkUser? GetUser() => GetComponent<MPEventSystemLocator>()?.eventSystem?.localUser?.currentNetworkUser;

        private void LateUpdate()
        {
            if (!button) return;
            NetworkUser? user = GetUser();
            byte choice = user is not null && user ? PlayerTeams.GetChoice(user) : (byte)0;
            if (user != requestingUser || choice == requestedChoice) awaitingChoice = false;
            label.text = "TEAM: " + PlayerTeams.Names[choice].ToUpperInvariant();
            label.color = colors[choice];
            button.interactable = user && !Run.instance && PreGameController.instance;
            rect.anchorMin = source.anchorMin;
            rect.anchorMax = source.anchorMax;
            rect.pivot = source.pivot;
            rect.sizeDelta = source.sizeDelta;
            rect.anchoredPosition = source.anchoredPosition + Vector2.up * (source.rect.height + 8f);

            MPButton next = controller.unreadyButton && controller.unreadyButton.gameObject.activeInHierarchy
                ? controller.unreadyButton : controller.readyButton;
            Navigation nav = readyNavigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnDown = next;
            button.navigation = nav;
            nav = next == controller.readyButton ? readyNavigation : unreadyNavigation;
            nav.selectOnUp = button;
            next.navigation = nav;
        }

        private void OnDestroy()
        {
            if (!button) return;
            if (controller && controller.readyButton) controller.readyButton.navigation = readyNavigation;
            if (controller && controller.unreadyButton) controller.unreadyButton.navigation = unreadyNavigation;
            Destroy(button.gameObject);
        }

        [HarmonyPatch(typeof(CharacterSelectController), "Awake")]
        private static class AddSelector
        {
            private static void Postfix(CharacterSelectController __instance) => __instance.gameObject.AddComponent<TeamSelector>();
        }
    }
}
