using System;
using UnityEngine;

// The gameplay values upgrades can change. Add a new entry here (and read it with UpgradeManager.GetStat)
// to support a new kind of upgrade.
public enum UpgradeStat
{
    MoveAcceleration, // player_movement ground and air move force
    JumpStrength, // player_movement jump impulse
    GrappleMomentum, // player_movement swing force (A/D pumping while hooked)
    DamageMultiplier, // PlayerDamage: multiplies every hit
    VelocityDamage, // PlayerDamage: extra damage per unit of impact speed
    ComboDamage, // PlayerDamage: extra damage fraction per hit in the current combo
}

[Serializable]
public struct StatModifier
{
    public UpgradeStat stat;
    [Tooltip("Added to the base value")]
    public float flat;
    [Tooltip("Fraction of the (base + flat) value added on top, e.g. 0.25 = +25%")]
    public float percent;
}

public enum UpgradeCategory
{
    Movement,
    Damage,
}

// One upgrade the player can earn. Create new ones with Assets > Create > Cat Cannonball > Upgrade.
// Upgrades in a Resources/Upgrades folder are offered automatically.
[CreateAssetMenu(fileName = "NewUpgrade", menuName = "Cat Cannonball/Upgrade")]
public class UpgradeDefinition : ScriptableObject
{
    public string displayName = "New Upgrade";
    [TextArea(2, 4)] public string description;
    public UpgradeCategory category;
    public Sprite icon; // optional, shown on the reward button when set

    public StatModifier[] modifiers = new StatModifier[0];

    [Header("Rules")]
    [Min(1)] public int maxStacks = 1; // how many times it can be picked in one run
    [Min(0f)] public float weight = 1f; // relative chance of being offered
    [Tooltip("Can't be owned together with any of these")]
    public UpgradeDefinition[] incompatibleWith = new UpgradeDefinition[0];
    [Tooltip("Only one upgrade with the same non-empty group can be owned per run")]
    public string exclusiveGroup = "";

    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;

    public bool IsIncompatibleWith(UpgradeDefinition other)
    {
      if (other == null || other == this) return false;
      if (!string.IsNullOrEmpty(exclusiveGroup) && exclusiveGroup == other.exclusiveGroup) return true;
      return Array.IndexOf(incompatibleWith, other) >= 0 || Array.IndexOf(other.incompatibleWith, this) >= 0;
    }
}
