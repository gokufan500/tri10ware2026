using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Upgrades earned during a run, and the reward choices between levels.
// Gameplay code reads the upgraded values with GetStat. Upgrades reset when ScoreManager.StartRun is called.
public class UpgradeManager : MonoBehaviour
{
    [Tooltip("Upgrades that can be offered. Leave empty to use every UpgradeDefinition in Resources/Upgrades.")]
    public List<UpgradeDefinition> upgradePool = new List<UpgradeDefinition>();
    [Min(1)] public int choicesPerReward = 3;
    [Tooltip("PLACEHOLDER: the design doc puts rewards after bosses, which don't exist yet, so every completed level gives one")]
    public bool rewardAfterEveryLevel = true;

    public static event Action<UpgradeManager> UpgradesChanged;
    public static event Action<UpgradeDefinition> UpgradeAcquired;
    public static event Action<UpgradeOffer> RewardOffered; // the reward UI listens to this
    public static event Action<UpgradeOffer> RewardClaimed;

    public IReadOnlyList<UpgradeDefinition> Owned => owned; // one entry per pick, so stacks repeat
    public UpgradeOffer CurrentOffer { get; private set; }
    public bool HasUnclaimedReward => CurrentOffer != null && !CurrentOffer.IsClaimed && CurrentOffer.Choices.Count > 0;

    readonly List<UpgradeDefinition> owned = new List<UpgradeDefinition>();
    Action afterRewardClaimed;

    static UpgradeManager instance;
    public static UpgradeManager Current => instance; // may be null

    // Always returns an UpgradeManager, creating one if the scene doesn't have it
    public static UpgradeManager Instance
    {
        get
        {
            if (instance == null) instance = FindAnyObjectByType<UpgradeManager>();
            if (instance == null) instance = new GameObject("UpgradeManager").AddComponent<UpgradeManager>();
            return instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
        UpgradesChanged = null;
        UpgradeAcquired = null;
        RewardOffered = null;
        RewardClaimed = null;
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
        DontDestroyOnLoad(gameObject); // upgrades last for the whole run

        if (upgradePool.Count == 0) upgradePool.AddRange(Resources.LoadAll<UpgradeDefinition>("Upgrades"));

        SceneManager.sceneLoaded += OnSceneLoaded;
        ScoreManager.RunStarted += OnRunStarted;
        ScoreManager.LevelCompleted += OnLevelCompleted;
    }

    void Start()
    {
        if (instance == this) SetUpPlayers();
    }

    void OnDestroy()
    {
        if (instance != this) return;
        instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        ScoreManager.RunStarted -= OnRunStarted;
        ScoreManager.LevelCompleted -= OnLevelCompleted;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => SetUpPlayers();

    void OnRunStarted(ScoreManager score) => ResetRun();

    void OnLevelCompleted(LevelResult result)
    {
      if (rewardAfterEveryLevel) CreateOffer();
    }

    // Makes sure every player picks up upgrades, including players in generated levels
    void SetUpPlayers()
    {
      foreach (player_movement player in FindObjectsByType<player_movement>(FindObjectsInactive.Exclude))
      {
        if (player.GetComponent<PlayerUpgrades>() == null) player.gameObject.AddComponent<PlayerUpgrades>();
        if (player.GetComponent<PlayerDamage>() == null) player.gameObject.AddComponent<PlayerDamage>();
      }
    }

    // ---- Owned upgrades ----

    public int StackCount(UpgradeDefinition upgrade)
    {
      int count = 0;
      foreach (UpgradeDefinition o in owned) if (o == upgrade) count++;
      return count;
    }

    public bool CanAcquire(UpgradeDefinition upgrade)
    {
      if (upgrade == null || StackCount(upgrade) >= upgrade.maxStacks) return false;
      foreach (UpgradeDefinition o in owned) if (upgrade.IsIncompatibleWith(o)) return false;
      return true;
    }

    public bool Acquire(UpgradeDefinition upgrade)
    {
      if (!CanAcquire(upgrade)) return false;

      owned.Add(upgrade);
      UpgradeAcquired?.Invoke(upgrade);
      UpgradesChanged?.Invoke(this);
      return true;
    }

    // Clears all upgrades and any open reward
    public void ResetRun()
    {
      owned.Clear();
      CurrentOffer = null;
      afterRewardClaimed = null;
      UpgradesChanged?.Invoke(this);
    }

    // The base value with every owned upgrade applied: (base + all flat) * (1 + all percent)
    public float GetStat(UpgradeStat stat, float baseValue)
    {
      float flat = 0f, percent = 0f;
      foreach (UpgradeDefinition upgrade in owned)
      {
        foreach (StatModifier modifier in upgrade.modifiers)
        {
          if (modifier.stat != stat) continue;
          flat += modifier.flat;
          percent += modifier.percent;
        }
      }
      return (baseValue + flat) * (1f + percent);
    }

    // Same as GetStat, but works when no UpgradeManager exists (returns the base value)
    public static float Stat(UpgradeStat stat, float baseValue) => instance != null ? instance.GetStat(stat, baseValue) : baseValue;

    // ---- Rewards ----

    // Rolls new choices from the upgrades that can still be acquired. Replaces any unclaimed offer.
    public UpgradeOffer CreateOffer()
    {
      var candidates = new List<UpgradeDefinition>();
      foreach (UpgradeDefinition upgrade in upgradePool)
      {
        if (CanAcquire(upgrade) && upgrade.weight > 0f && !candidates.Contains(upgrade)) candidates.Add(upgrade);
      }

      var choices = new List<UpgradeDefinition>();
      while (choices.Count < choicesPerReward && candidates.Count > 0)
      {
        int index = PickWeighted(candidates);
        choices.Add(candidates[index]);
        candidates.RemoveAt(index);
      }

      CurrentOffer = new UpgradeOffer(choices);
      return CurrentOffer;
    }

    static int PickWeighted(List<UpgradeDefinition> candidates)
    {
      float total = 0f;
      foreach (UpgradeDefinition c in candidates) total += c.weight;
      float roll = UnityEngine.Random.value * total;
      for (int i = 0; i < candidates.Count; i++)
      {
        roll -= candidates[i].weight;
        if (roll < 0f) return i;
      }
      return candidates.Count - 1;
    }

    // Shows the current reward (rolling one if needed). onClaimed runs once a choice is made.
    // A reward room or boss exit can call this directly. If nothing can be offered, onClaimed runs right away.
    public void ShowReward(Action onClaimed = null)
    {
      if (!HasUnclaimedReward) CreateOffer();

      if (!HasUnclaimedReward || RewardOffered == null)
      {
        if (RewardOffered == null && CurrentOffer.Choices.Count > 0) Debug.LogWarning("UpgradeManager: no reward UI in the scene, skipping the upgrade reward");
        CurrentOffer = null;
        onClaimed?.Invoke();
        return;
      }

      afterRewardClaimed = onClaimed;
      RewardOffered.Invoke(CurrentOffer);
    }

    // Picks one choice from the current reward. Returns false if the reward was already claimed or the index is invalid.
    public bool ClaimReward(int choiceIndex)
    {
      UpgradeOffer offer = CurrentOffer;
      if (offer == null || offer.IsClaimed || choiceIndex < 0 || choiceIndex >= offer.Choices.Count) return false;
      if (!Acquire(offer.Choices[choiceIndex])) return false;

      offer.Claim(choiceIndex);
      CurrentOffer = null;
      RewardClaimed?.Invoke(offer);

      Action next = afterRewardClaimed;
      afterRewardClaimed = null;
      next?.Invoke();
      return true;
    }
}

// One reward: a few upgrade choices, of which only one can be taken
public class UpgradeOffer
{
    public IReadOnlyList<UpgradeDefinition> Choices { get; }
    public bool IsClaimed => ChosenIndex >= 0;
    public int ChosenIndex { get; private set; } = -1;
    public UpgradeDefinition Chosen => IsClaimed ? Choices[ChosenIndex] : null;

    public UpgradeOffer(List<UpgradeDefinition> choices)
    {
      Choices = choices;
    }

    internal void Claim(int index) => ChosenIndex = index;
}
