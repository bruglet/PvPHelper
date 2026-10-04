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
        private RectTransform damagePanel = null!;
        private static readonly string[] damageNames = { "PVP Damage", "DVP Damage", "Engi Turret", "Monster Scaling" };
        private readonly MPButton[] decrease = new MPButton[damageNames.Length];
        private readonly MPButton[] increase = new MPButton[damageNames.Length];
        private readonly TMP_Text[] damageLabels = new TMP_Text[damageNames.Length];
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
            button = CreateButton("PvPHelperTeamSelector", source.parent);
            label = button.GetComponentInChildren<TMP_Text>(true);
            if (!label) { Destroy(button.gameObject); enabled = false; return; }
            rect = (RectTransform)button.transform;
            button.onClick.AddListener(Cycle);
            CreateDamageRows();
            readyNavigation = controller.readyButton.navigation;
            unreadyNavigation = controller.unreadyButton ? controller.unreadyButton.navigation : default;
            button.gameObject.SetActive(true);
        }

        private MPButton CreateButton(string name, Transform parent)
        {
            MPButton result = Instantiate(controller.readyButton, parent);
            result.name = name;
            result.gameObject.SetActive(false);
            // Remove serialized Ready actions as well as runtime listeners from the clone.
            result.onClick = new Button.ButtonClickedEvent();
            result.onFindSelectableLeft = new UnityEvent();
            result.onFindSelectableRight = new UnityEvent();
            result.onSelect = new UnityEvent();
            result.onDeselect = new UnityEvent();
            result.onDebugSelect = new UnityEvent();
            result.defaultFallbackButton = false;
            if (result is HGButton hg) hg.updateTextOnHover = false;
            foreach (LanguageTextMeshController text in result.GetComponentsInChildren<LanguageTextMeshController>(true))
                text.enabled = false;
            var layout = result.GetComponent<LayoutElement>() ?? result.gameObject.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
            return result;
        }

        private void CreateDamageRows()
        {
            damagePanel = (RectTransform)new GameObject("PvPHelperDamageSettings", typeof(RectTransform), typeof(LayoutElement)).transform;
            damagePanel.SetParent(source.parent, false);
            damagePanel.GetComponent<LayoutElement>().ignoreLayout = true;
            for (int i = 0; i < damageNames.Length; i++)
            {
                DamageSetting setting = (DamageSetting)i;
                var row = (RectTransform)new GameObject(damageNames[i], typeof(RectTransform)).transform;
                row.SetParent(damagePanel, false);
                row.anchorMin = new Vector2(0f, 1f - (i + 1f) / damageNames.Length);
                row.anchorMax = new Vector2(1f, 1f - (float)i / damageNames.Length);
                row.offsetMin = new Vector2(0f, 2f);
                row.offsetMax = new Vector2(0f, -2f);
                decrease[i] = CreateArrow(row, "Decrease", "<", 0f, .16f);
                increase[i] = CreateArrow(row, "Increase", ">", .84f, 1f);
                decrease[i].onClick.AddListener(() => Adjust(setting, -DamagePolicy.Step));
                increase[i].onClick.AddListener(() => Adjust(setting, DamagePolicy.Step));

                var text = new GameObject("Value", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                text.transform.SetParent(row, false);
                text.font = label.font;
                text.fontSharedMaterial = label.fontSharedMaterial;
                text.fontSize = label.fontSize * .8f;
                text.enableAutoSizing = true;
                text.fontSizeMin = 10f;
                text.fontSizeMax = label.fontSize;
                text.alignment = TextAlignmentOptions.Center;
                text.color = Color.white;
                text.raycastTarget = false;
                text.rectTransform.anchorMin = new Vector2(.17f, 0f);
                text.rectTransform.anchorMax = new Vector2(.83f, 1f);
                text.rectTransform.offsetMin = Vector2.zero;
                text.rectTransform.offsetMax = Vector2.zero;
                damageLabels[i] = text;
            }
        }

        private MPButton CreateArrow(RectTransform row, string name, string caption, float min, float max)
        {
            MPButton arrow = CreateButton(name, row);
            var arrowRect = (RectTransform)arrow.transform;
            arrowRect.anchorMin = new Vector2(min, 0f);
            arrowRect.anchorMax = new Vector2(max, 1f);
            arrowRect.offsetMin = Vector2.zero;
            arrowRect.offsetMax = Vector2.zero;
            TMP_Text text = arrow.GetComponentInChildren<TMP_Text>(true);
            text.text = caption;
            arrow.gameObject.SetActive(true);
            return arrow;
        }

        private static void Adjust(DamageSetting setting, int change)
        {
            int value = DamageSettings.Get(setting);
            DamageSettings.Set(setting, System.Math.Max(0, System.Math.Min(DamageSettings.Maximum(setting), value + change)));
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
            UpdateDamageRows();

            MPButton next = controller.unreadyButton && controller.unreadyButton.gameObject.activeInHierarchy
                ? controller.unreadyButton : controller.readyButton;
            Navigation nav = readyNavigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnDown = next;
            int last = damageNames.Length - 1;
            if (decrease[last].interactable || increase[last].interactable)
                nav.selectOnUp = decrease[last].interactable ? decrease[last] : increase[last];
            button.navigation = nav;
            nav = next == controller.readyButton ? readyNavigation : unreadyNavigation;
            nav.selectOnUp = button;
            next.navigation = nav;
        }

        private void UpdateDamageRows()
        {
            float height = (Mathf.Min(source.rect.height, 32f) + 4f) * damageNames.Length;
            damagePanel.anchorMin = source.anchorMin;
            damagePanel.anchorMax = source.anchorMax;
            damagePanel.pivot = source.pivot;
            damagePanel.sizeDelta = source.sizeDelta;
            damagePanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            damagePanel.anchoredPosition = rect.anchoredPosition + Vector2.up *
                ((1f - rect.pivot.y) * rect.rect.height + damagePanel.pivot.y * height + 8f);
            for (int i = 0; i < damageNames.Length; i++)
            {
                int value = DamageSettings.Get((DamageSetting)i);
                damageLabels[i].text = damageNames[i] + ": " + value + "%";
                decrease[i].interactable = DamageSettings.CanEdit && value > 0;
                increase[i].interactable = DamageSettings.CanEdit && value < DamageSettings.Maximum((DamageSetting)i);
            }
            for (int i = 0; i < damageNames.Length; i++)
            {
                SetArrowNavigation(decrease[i], increase[i], i, false);
                SetArrowNavigation(increase[i], decrease[i], i, true);
            }
        }

        private void SetArrowNavigation(MPButton arrow, MPButton sibling, int row, bool right)
        {
            var nav = readyNavigation;
            nav.mode = Navigation.Mode.Explicit;
            if (right) nav.selectOnLeft = sibling.interactable ? sibling : null;
            else nav.selectOnRight = sibling.interactable ? sibling : null;
            if (row == 0) nav.selectOnUp = readyNavigation.selectOnUp;
            else
            {
                MPButton up = right ? increase[row - 1] : decrease[row - 1];
                nav.selectOnUp = up.interactable ? up : (right ? decrease[row - 1] : increase[row - 1]);
            }
            if (row == damageNames.Length - 1) nav.selectOnDown = button;
            else
            {
                MPButton down = right ? increase[row + 1] : decrease[row + 1];
                nav.selectOnDown = down.interactable ? down : (right ? decrease[row + 1] : increase[row + 1]);
            }
            arrow.navigation = nav;
        }

        private void OnDestroy()
        {
            if (button)
            {
                if (controller && controller.readyButton) controller.readyButton.navigation = readyNavigation;
                if (controller && controller.unreadyButton) controller.unreadyButton.navigation = unreadyNavigation;
                Destroy(button.gameObject);
            }
            if (damagePanel) Destroy(damagePanel.gameObject);
        }

        [HarmonyPatch(typeof(CharacterSelectController), "Awake")]
        private static class AddSelector
        {
            private static void Postfix(CharacterSelectController __instance) => __instance.gameObject.AddComponent<TeamSelector>();
        }
    }
}
