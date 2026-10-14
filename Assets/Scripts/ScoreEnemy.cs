using System.Collections.Generic;
using UnityEngine;

// Put on any enemy (placed or spawned) so the ScoreManager counts it.
// The enemy/damage code calls Defeat() when the enemy dies.
public class ScoreEnemy : MonoBehaviour
{
    public string enemyType = "Floaty"; // looked up in ScoreManager's enemy points
    [Tooltip("Placeholder until enemies have health: the player touching it defeats it")]
    public bool defeatOnPlayerContact = false;
    public bool destroyOnDefeat = true;

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

    void TryPlayerContact(Rigidbody2D body, float speed)
    {
      if (defeatOnPlayerContact && body.GetComponent<player_movement>() != null) Defeat(speed);
    }
}
