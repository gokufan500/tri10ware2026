using UnityEngine;

// Trigger at the end of a level (e.g. a room exit). The player entering it completes the level.
[RequireComponent(typeof(Collider2D))]
public class LevelGoal : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D other)
    {
      Rigidbody2D body = other.attachedRigidbody;
      if (body != null && body.GetComponent<player_movement>() != null) ScoreManager.Instance.CompleteLevel();
    }
}
