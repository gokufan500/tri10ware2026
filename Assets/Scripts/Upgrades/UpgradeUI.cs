using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Placeholder reward screen: one button per upgrade choice, built in code with Unity's built-in UI.
// It only listens to UpgradeManager.RewardOffered and calls UpgradeManager.ClaimReward, so artists can
// replace this whole component with their own UI that does the same. Put it next to the UpgradeManager.
public class UpgradeUI : MonoBehaviour
{
    public bool pauseWhileChoosing = true;
    public int fontSize = 32;
    public Color textColor = Color.white;
    public Color buttonColor = new Color(0.25f, 0.25f, 0.3f);
    public Color movementColor = new Color(0.4f, 0.8f, 1f);
    public Color damageColor = new Color(1f, 0.55f, 0.4f);

    public bool IsOpen => panel != null && panel.activeSelf;
    public IReadOnlyList<Button> ChoiceButtons => buttons;

    Font font;
    GameObject panel;
    Text ownedText;
    readonly List<Button> buttons = new List<Button>();
    UpgradeOffer shownOffer;
    float timeScaleBeforeOpen = 1f;

    static readonly Key[] NumberKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5 };

    void Awake()
    {
        BuildUI();
    }

    void Start()
    {
        // The buttons need an EventSystem; ScoreUI usually adds one, only add it if nothing did
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.transform.SetParent(transform, false);
        }
    }

    void OnEnable()
    {
        UpgradeManager.RewardOffered += Show;
        ScoreManager.LevelStarted += OnLevelStarted;
    }

    void OnDisable()
    {
        UpgradeManager.RewardOffered -= Show;
        ScoreManager.LevelStarted -= OnLevelStarted;
    }

    void Update()
    {
      if (!IsOpen || Keyboard.current == null) return;

      // Number keys pick a choice too
      for (int i = 0; i < buttons.Count && i < NumberKeys.Length; i++)
      {
        if (buttons[i].gameObject.activeSelf && Keyboard.current[NumberKeys[i]].wasPressedThisFrame) Choose(i);
      }
    }

    void OnLevelStarted(ScoreManager score) => Hide();

    void Show(UpgradeOffer offer)
    {
      shownOffer = offer;
      UpgradeManager upgrades = UpgradeManager.Current;

      for (int i = 0; i < buttons.Count; i++)
      {
        bool used = i < offer.Choices.Count;
        buttons[i].gameObject.SetActive(used);
        buttons[i].interactable = used;
        if (used) FillButton(buttons[i], offer.Choices[i], i, upgrades);
      }

      ownedText.text = upgrades == null || upgrades.Owned.Count == 0 ? "Current upgrades: none" :
        "Current upgrades: " + string.Join(", ", upgrades.Owned.Distinct().Select(u => StackLabel(u, upgrades.StackCount(u))));

      if (!IsOpen && pauseWhileChoosing)
      {
        timeScaleBeforeOpen = Time.timeScale;
        Time.timeScale = 0f;
      }
      panel.SetActive(true);
    }

    void Hide()
    {
      if (!IsOpen) return;
      panel.SetActive(false);
      shownOffer = null;
      if (pauseWhileChoosing) Time.timeScale = timeScaleBeforeOpen;
    }

    public void Choose(int index)
    {
      if (!IsOpen || shownOffer == null || shownOffer.IsClaimed) return;
      UpgradeManager upgrades = UpgradeManager.Current;
      if (upgrades == null || upgrades.CurrentOffer != shownOffer) return;

      UpgradeOffer offer = shownOffer;
      foreach (Button button in buttons) button.interactable = false; // only one pick per reward
      Hide(); // before claiming, since claiming can load the next level
      if (!upgrades.ClaimReward(index))
      {
        Debug.LogWarning($"UpgradeUI: couldn't claim choice {index + 1}");
        Show(offer);
      }
    }

    void FillButton(Button button, UpgradeDefinition upgrade, int index, UpgradeManager upgrades)
    {
      Transform t = button.transform;
      t.Find("Name").GetComponent<Text>().text = $"{index + 1}. {upgrade.DisplayName}";

      int owned = upgrades != null ? upgrades.StackCount(upgrade) : 0;
      string stacks = upgrade.maxStacks > 1 ? $"   (have {owned}/{upgrade.maxStacks})" : "";
      Text category = t.Find("Category").GetComponent<Text>();
      category.text = upgrade.category + stacks;
      category.color = upgrade.category == UpgradeCategory.Movement ? movementColor : damageColor;

      t.Find("Description").GetComponent<Text>().text = upgrade.description;

      Image icon = t.Find("Icon").GetComponent<Image>();
      icon.sprite = upgrade.icon;
      icon.gameObject.SetActive(upgrade.icon != null);
    }

    static string StackLabel(UpgradeDefinition upgrade, int stacks) => stacks > 1 ? $"{upgrade.DisplayName} x{stacks}" : upgrade.DisplayName;

    void BuildUI()
    {
      var canvasObject = new GameObject("UpgradeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
      canvasObject.transform.SetParent(transform, false);
      var canvas = canvasObject.GetComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      canvas.sortingOrder = 110; // above the score UI
      var scaler = canvasObject.GetComponent<CanvasScaler>();
      scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
      scaler.referenceResolution = new Vector2(1920f, 1080f);
      scaler.matchWidthOrHeight = 0.5f;

      font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

      panel = new GameObject("RewardPanel", typeof(RectTransform), typeof(Image));
      panel.transform.SetParent(canvas.transform, false);
      var panelRect = (RectTransform)panel.transform;
      panelRect.anchorMin = Vector2.zero;
      panelRect.anchorMax = Vector2.one;
      panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
      panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);

      Text title = CreateText("Title", panel.transform, fontSize + 16, TextAnchor.MiddleCenter);
      title.text = "CHOOSE AN UPGRADE";
      SetRect(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 340f), new Vector2(1200f, 80f));

      const int maxChoices = 3;
      const float width = 480f, spacing = 40f;
      for (int i = 0; i < maxChoices; i++)
      {
        float x = (i - (maxChoices - 1) / 2f) * (width + spacing);
        buttons.Add(CreateChoiceButton(i, new Vector2(x, -20f), new Vector2(width, 520f)));
      }

      ownedText = CreateText("OwnedUpgrades", panel.transform, fontSize - 6, TextAnchor.MiddleCenter);
      ownedText.horizontalOverflow = HorizontalWrapMode.Wrap;
      SetRect(ownedText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -360f), new Vector2(1500f, 80f));

      Text hint = CreateText("Hint", panel.transform, fontSize - 8, TextAnchor.MiddleCenter);
      hint.text = "Click a choice or press 1 / 2 / 3";
      hint.color = new Color(1f, 1f, 1f, 0.6f);
      SetRect(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -430f), new Vector2(1200f, 50f));

      panel.SetActive(false);
    }

    Button CreateChoiceButton(int index, Vector2 position, Vector2 size)
    {
      var buttonObject = new GameObject($"Choice{index + 1}", typeof(RectTransform), typeof(Image), typeof(Button));
      buttonObject.transform.SetParent(panel.transform, false);
      SetRect((RectTransform)buttonObject.transform, new Vector2(0.5f, 0.5f), position, size);
      buttonObject.GetComponent<Image>().color = buttonColor;
      Button button = buttonObject.GetComponent<Button>();
      button.onClick.AddListener(() => Choose(index));

      var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
      icon.transform.SetParent(buttonObject.transform, false);
      SetRect((RectTransform)icon.transform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(128f, 128f));
      icon.GetComponent<Image>().preserveAspect = true;
      icon.GetComponent<Image>().raycastTarget = false;
      icon.SetActive(false);

      Text label = CreateText("Name", buttonObject.transform, fontSize + 4, TextAnchor.MiddleCenter);
      SetRect(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(size.x - 40f, 60f));

      Text category = CreateText("Category", buttonObject.transform, fontSize - 6, TextAnchor.MiddleCenter);
      SetRect(category.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -230f), new Vector2(size.x - 40f, 40f));

      Text description = CreateText("Description", buttonObject.transform, fontSize - 4, TextAnchor.UpperCenter);
      description.horizontalOverflow = HorizontalWrapMode.Wrap;
      SetRect(description.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -290f), new Vector2(size.x - 50f, 210f));

      return button;
    }

    Text CreateText(string name, Transform parent, int size, TextAnchor alignment)
    {
      var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
      textObject.transform.SetParent(parent, false);
      Text text = textObject.GetComponent<Text>();
      text.font = font;
      text.fontSize = size;
      text.alignment = alignment;
      text.color = textColor;
      text.raycastTarget = false; // clicks go to the button
      text.horizontalOverflow = HorizontalWrapMode.Overflow;
      text.verticalOverflow = VerticalWrapMode.Overflow;
      return text;
    }

    static void SetRect(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
      rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
      rect.anchoredPosition = position;
      rect.sizeDelta = size;
    }
}
