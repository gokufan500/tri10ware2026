using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tracks score for the current level and the whole run.
// Any level (hand-made or generated) only needs to call StartLevel, EnemyDefeated and CompleteLevel.
// All point values below are PLACEHOLDERS: the design doc doesn't define scoring rules yet.
public class ScoreManager : MonoBehaviour
{
    [Serializable]
    public struct EnemyPoints
    {
        public string enemyType;
        public int points;
    }

    [Header("Enemy Points (placeholders)")]
    public int defaultEnemyPoints = 100;
    public EnemyPoints[] enemyPoints =
    {
        new EnemyPoints { enemyType = "Floaty", points = 100 },
        new EnemyPoints { enemyType = "Shooty", points = 150 },
        new EnemyPoints { enemyType = "Splodey", points = 150 },
        new EnemyPoints { enemyType = "Chasey", points = 200 },
        new EnemyPoints { enemyType = "Iron", points = 300 },
    };
    public float pointsPerImpactSpeed = 5f; // velocity = damage in the design, so faster hits score more

    [Header("Level Completion (placeholders)")]
    public int levelCompletePoints = 500;
    public int allEnemiesBonus = 500; // every ScoreEnemy in the level defeated
    public float parTimeSeconds = 60f;
    public float timeBonusPerSecondUnderPar = 10f;

    [Header("Flow")]
    public bool startLevelOnSceneLoad = true; // a level generator can turn this off and call StartLevel itself

    public static event Action<ScoreManager> ScoreChanged;
    public static event Action<ScoreManager> LevelStarted;
    public static event Action<ScoreManager> RunStarted; // a brand new run (upgrades reset on this)
    public static event Action<LevelResult> LevelCompleted;
    public static event Action NextLevelRequested; // if nothing listens, the current scene is reloaded

    public string LevelId { get; private set; }
    public bool IsLevelActive { get; private set; }
    public float LevelTime { get; private set; }
    public int LevelScore { get; private set; } // points earned in the level so far
    public int RunScore { get; private set; } // final scores of all completed levels
    public int EnemiesDefeated { get; private set; }
    public int TotalEnemies => EnemiesDefeated + ScoreEnemy.AliveCount;
    public int CurrentScore => RunScore + LevelScore;
    public LevelResult LastResult { get; private set; }

    static ScoreManager instance;
    public static ScoreManager Current => instance; // may be null

    // Always returns a ScoreManager, creating one if the scene doesn't have it
    public static ScoreManager Instance
    {
        get
        {
            if (instance == null) instance = FindAnyObjectByType<ScoreManager>();
            if (instance == null) instance = new GameObject("ScoreManager").AddComponent<ScoreManager>();
            return instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
        ScoreChanged = null;
        LevelStarted = null;
        RunStarted = null;
        LevelCompleted = null;
        NextLevelRequested = null;
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject); // the run score survives level loads
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start()
    {
        if (instance == this && startLevelOnSceneLoad && !IsLevelActive) StartLevel();
    }

    void OnDestroy()
    {
        if (instance != this) return;
        instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
      if (startLevelOnSceneLoad && mode == LoadSceneMode.Single) StartLevel();
    }

    void Update()
    {
      if (IsLevelActive) LevelTime += Time.deltaTime;
    }

    // Resets everything level-specific and starts the timer. The run score is kept.
    public void StartLevel(string levelId = null)
    {
      LevelId = string.IsNullOrEmpty(levelId) ? SceneManager.GetActiveScene().name : levelId;
      LevelScore = 0;
      EnemiesDefeated = 0;
      LevelTime = 0f;
      IsLevelActive = true;

      LevelStarted?.Invoke(this);
      ScoreChanged?.Invoke(this);
    }

    // Starts a brand new run: clears the run score too
    public void StartRun(string levelId = null)
    {
      RunScore = 0;
      RunStarted?.Invoke(this);
      StartLevel(levelId);
    }

    public int EnemyDefeated(string enemyType = null, float impactSpeed = 0f)
    {
      if (!IsLevelActive) return 0;

      int points = PointsFor(enemyType) + Mathf.RoundToInt(Mathf.Max(0f, impactSpeed) * pointsPerImpactSpeed);
      EnemiesDefeated++;
      LevelScore += points;
      ScoreChanged?.Invoke(this);
      return points;
    }

    // For anything else worth points later (bosses, pickups, upgrades)
    public void AddPoints(int points)
    {
      if (!IsLevelActive) return;

      LevelScore += points;
      ScoreChanged?.Invoke(this);
    }

    public LevelResult CompleteLevel()
    {
      if (!IsLevelActive) return LastResult;
      IsLevelActive = false;

      var result = new LevelResult
      {
        levelId = LevelId,
        enemiesDefeated = EnemiesDefeated,
        totalEnemies = TotalEnemies,
        timeSeconds = LevelTime,
        pointsEarned = LevelScore,
        completionPoints = levelCompletePoints,
        allEnemiesBonus = EnemiesDefeated > 0 && ScoreEnemy.AliveCount == 0 ? allEnemiesBonus : 0,
        timeBonus = Mathf.RoundToInt(Mathf.Max(0f, parTimeSeconds - LevelTime) * timeBonusPerSecondUnderPar),
      };
      result.finalScore = result.pointsEarned + result.completionPoints + result.allEnemiesBonus + result.timeBonus;

      RunScore += result.finalScore;
      LevelScore = 0;
      result.runScore = RunScore;
      LastResult = result;

      LevelCompleted?.Invoke(result);
      ScoreChanged?.Invoke(this);
      return result;
    }

    public void ContinueToNextLevel()
    {
      // An earned upgrade reward is picked first, then this runs again
      UpgradeManager upgrades = UpgradeManager.Current;
      if (upgrades != null && upgrades.HasUnclaimedReward)
      {
        upgrades.ShowReward(ContinueToNextLevel);
        return;
      }

      if (NextLevelRequested != null) NextLevelRequested();
      else SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public int PointsFor(string enemyType)
    {
      if (!string.IsNullOrEmpty(enemyType))
      {
        foreach (EnemyPoints entry in enemyPoints)
        {
          if (string.Equals(entry.enemyType, enemyType, StringComparison.OrdinalIgnoreCase)) return entry.points;
        }
      }
      return defaultEnemyPoints;
    }
}

[Serializable]
public struct LevelResult
{
    public string levelId;
    public int enemiesDefeated;
    public int totalEnemies;
    public float timeSeconds;
    public int pointsEarned; // enemies + AddPoints
    public int completionPoints;
    public int allEnemiesBonus;
    public int timeBonus;
    public int finalScore; // this level's total
    public int runScore; // run total including this level
}
