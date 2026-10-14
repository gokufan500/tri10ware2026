using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Simple score HUD and level results screen, built in code so no scene or prefab setup is needed.
// Put it next to the ScoreManager.
public class ScoreUI : MonoBehaviour
{
    public bool pauseOnResults = true;
    public int fontSize = 36;
    public Color textColor = Color.white;

    public bool ResultsVisible => resultsPanel != null && resultsPanel.activeSelf;
    public string HudText => hudText.text;
    public string ResultsText => resultsText.text;

    Canvas canvas;
    Text hudText;
    Text resultsText;
    GameObject resultsPanel;
    float timeScaleBeforeResults = 1f;

    void Awake()
    {
        BuildUI();
    }

    void OnEnable()
    {
        ScoreManager.LevelStarted += OnLevelStarted;
        ScoreManager.LevelCompleted += ShowResults;
    }

    void OnDisable()
    {
        ScoreManager.LevelStarted -= OnLevelStarted;
        ScoreManager.LevelCompleted -= ShowResults;
    }

    void Update()
    {
      ScoreManager score = ScoreManager.Current;
      if (score == null) return;

      string enemies = score.TotalEnemies > 0 ? $"{score.EnemiesDefeated} / {score.TotalEnemies}" : score.EnemiesDefeated.ToString();
      hudText.text = $"Score  {score.CurrentScore:N0}\nEnemies  {enemies}\nTime  {FormatTime(score.LevelTime)}";

      if (ResultsVisible && Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame) Continue();
    }

    void OnLevelStarted(ScoreManager score)
    {
      HideResults();
    }

    void ShowResults(LevelResult result)
    {
      string enemies = result.totalEnemies > 0 ? $"{result.enemiesDefeated} / {result.totalEnemies}" : result.enemiesDefeated.ToString();
      resultsText.text =
        "LEVEL COMPLETE\n\n" +
        $"Enemies defeated:  {enemies}\n" +
        $"Time:  {FormatTime(result.timeSeconds)}\n\n" +
        $"Points earned:  {result.pointsEarned:N0}\n" +
        $"Level complete:  +{result.completionPoints:N0}\n" +
        $"All enemies bonus:  +{result.allEnemiesBonus:N0}\n" +
        $"Time bonus:  +{result.timeBonus:N0}\n\n" +
        $"Level score:  {result.finalScore:N0}\n" +
        $"Total score:  {result.runScore:N0}";
      resultsPanel.SetActive(true);

      if (pauseOnResults)
      {
        timeScaleBeforeResults = Time.timeScale;
        Time.timeScale = 0f;
      }
    }

    void HideResults()
    {
      if (!ResultsVisible) return;
      resultsPanel.SetActive(false);
      if (pauseOnResults) Time.timeScale = timeScaleBeforeResults;
    }

    public void Continue()
    {
      HideResults();
      ScoreManager.Instance.ContinueToNextLevel();
    }

    static string FormatTime(float seconds)
    {
      int minutes = (int)(seconds / 60f);
      return $"{minutes}:{seconds - minutes * 60f:00.0}";
    }

    void BuildUI()
    {
      var canvasObject = new GameObject("ScoreCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
      canvasObject.transform.SetParent(transform, false);
      canvas = canvasObject.GetComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      canvas.sortingOrder = 100;
      var scaler = canvasObject.GetComponent<CanvasScaler>();
      scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
      scaler.referenceResolution = new Vector2(1920f, 1080f);
      scaler.matchWidthOrHeight = 0.5f;

      Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

      hudText = CreateText("ScoreHUD", canvas.transform, font, fontSize, TextAnchor.UpperLeft);
      SetRect(hudText.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(600f, 200f));
      hudText.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2f, -2f);

      resultsPanel = new GameObject("ResultsPanel", typeof(RectTransform), typeof(Image));
      resultsPanel.transform.SetParent(canvas.transform, false);
      var panelRect = (RectTransform)resultsPanel.transform;
      panelRect.anchorMin = Vector2.zero;
      panelRect.anchorMax = Vector2.one;
      panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
      resultsPanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.8f);

      resultsText = CreateText("ResultsText", resultsPanel.transform, font, fontSize, TextAnchor.MiddleCenter);
      SetRect(resultsText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(900f, 700f));

      var button = new GameObject("ContinueButton", typeof(RectTransform), typeof(Image), typeof(Button));
      button.transform.SetParent(resultsPanel.transform, false);
      SetRect((RectTransform)button.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -360f), new Vector2(360f, 80f));
      button.GetComponent<Image>().color = new Color(1f, 0.75f, 0.4f);
      button.GetComponent<Button>().onClick.AddListener(Continue);
      Text label = CreateText("Label", button.transform, font, fontSize, TextAnchor.MiddleCenter);
      label.text = "Continue (Enter)";
      label.color = Color.black;
      SetRect(label.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(360f, 80f));

      resultsPanel.SetActive(false);

      // The button needs an EventSystem; only add one if the scene doesn't have it
      if (FindAnyObjectByType<EventSystem>() == null)
      {
        var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventSystem.transform.SetParent(transform, false);
      }
    }

    Text CreateText(string name, Transform parent, Font font, int size, TextAnchor alignment)
    {
      var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
      textObject.transform.SetParent(parent, false);
      Text text = textObject.GetComponent<Text>();
      text.font = font;
      text.fontSize = size;
      text.alignment = alignment;
      text.color = textColor;
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
