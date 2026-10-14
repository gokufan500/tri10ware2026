using System.Collections.Generic;
using UnityEngine;

// Put on any enemy (placed or spawned) so the ScoreManager counts it.
// The enemy/damage code calls Defeat() when the enemy dies.
public class ScoreEnemy : MonoBehaviour
{
    public string enemyType = "Floaty"; // looked up in ScoreManager's enemy points
    [Tooltip("Placeholder until enemies have health: the player touching it counts as a hit (see health below)")]
    public bool defeatOnPlayerContact = false;
    public bool destroyOnDefeat = true;
    [Tooltip("Placeholder until enemies have their own health system: player hits subtract PlayerDamage from this")]
    public float health = 1f;
    public bool logHits = true;

    public bool IsDefeated { get; private set; }

    static readonly List<ScoreEnemy> alive = new List<ScoreEnemy>();
    public static int AliveCount => alive.Count;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => alive.Clear();

    void OnEnable()
    {
      if (!IsDefeated) alive.Add(this);
    }

    void OnDisable()
    {
      alive.Remove(this);
    }

    public void Defeat(float impactSpeed = 0f)
    {
      if (IsDefeated) return;
      IsDefeated = true;
      alive.Remove(this);

      ScoreManager.Instance.EnemyDefeated(enemyType, impactSpeed);
      if (destroyOnDefeat) Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
      Rigidbody2D body = other.attachedRigidbody;
      if (body != null) TryPlayerContact(body, body.linearVelocity.magnitude);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
      if (collision.rigidbody != null) TryPlayerContact(collision.rigidbody, collision.relativeVelocity.magnitude);
    }

    // Subtracts damage from the placeholder health and defeats the enemy when it runs out
    public void TakeDamage(float damage, float impactSpeed = 0f)
    {
      if (IsDefeated) return;
      health -= damage;
      if (logHits) Debug.Log($"{name} took {damage:0.##} damage at speed {impactSpeed:0.#} ({Mathf.Max(0f, health):0.##} health left)");
      if (health <= 0f) Defeat(impactSpeed);
    }

    void TryPlayerContact(Rigidbody2D body, float speed)
    {
      if (!defeatOnPlayerContact || body.GetComponent<player_movement>() == null) return;

      PlayerDamage damage = PlayerDamage.For(body);
      if (damage != null) TakeDamage(damage.DealHit(speed), speed);
      else Defeat(speed);
    }
}
