using UnityEngine;

// How much damage the player deals when hitting something, with damage upgrades and combos applied.
// The design says velocity = damage. The exact formula isn't defined yet, so ALL values here are PLACEHOLDERS.
// Enemy/boss code calls DealHit(impactSpeed) once per hit and subtracts the result from its health.
public class PlayerDamage : MonoBehaviour
{
    [Header("Base Damage (placeholders)")]
    public float baseDamage = 1f; // any hit deals at least this
    public float damagePerSpeed = 0.1f; // extra damage per unit of impact speed (10 u/s = +1)
    public float comboWindow = 2f; // seconds between hits before the combo resets

    public int ComboCount { get; private set; } // hits in the current combo, including the last one
    public float LastHitDamage { get; private set; }

    float lastHitTime = float.NegativeInfinity;

    // Damage a hit at this speed would deal right now, without counting it as a hit
    public float PreviewDamage(float impactSpeed) => CalculateDamage(impactSpeed, CurrentComboBefore());

    // Counts a hit (advancing the combo) and returns its damage
    public float DealHit(float impactSpeed)
    {
      int comboBefore = CurrentComboBefore();
      LastHitDamage = CalculateDamage(impactSpeed, comboBefore);
      ComboCount = comboBefore + 1;
      lastHitTime = Time.time;
      return LastHitDamage;
    }

    public float CalculateDamage(float impactSpeed, int comboHitsBefore)
    {
      float perSpeed = UpgradeManager.Stat(UpgradeStat.VelocityDamage, damagePerSpeed);
      float multiplier = UpgradeManager.Stat(UpgradeStat.DamageMultiplier, 1f);
      float comboBonus = UpgradeManager.Stat(UpgradeStat.ComboDamage, 0f) * comboHitsBefore;
      return (baseDamage + perSpeed * Mathf.Max(0f, impactSpeed)) * multiplier * (1f + comboBonus);
    }

    int CurrentComboBefore() => Time.time - lastHitTime <= comboWindow ? ComboCount : 0;

    // The PlayerDamage on a hitting body, or null if it isn't the player
    public static PlayerDamage For(Rigidbody2D body) => body != null ? body.GetComponent<PlayerDamage>() : null;
}
