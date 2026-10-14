using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
public class player_movement : MonoBehaviour
{
    public Rigidbody2D rb; 
    public float moveSpeed = 5f; // top speed A/D can push you to by itself (other sources can go faster)

    [Header("Walking (A/D)")]
    public float groundMoveForce = 40f;
    public float airMoveForce = 12f; // much weaker than on the ground
    public float groundFriction = 1.5f; // friction force = groundFriction * mass * gravity

    [Header("Jumping (Space)")]
    public float jumpImpulse = 7f;
    public float coyoteTime = 0.1f; // can still jump this long after leaving the ground
    public float jumpBufferTime = 0.1f; // a press this long before landing still jumps

    [Header("Air")]
    public float airDrag = 0.01f; // air resistance force = airDrag * speed^2, no hard speed cap

    [Header("Ground Check")]
    [Range(0f, 1f)] public float minGroundNormalY = 0.7f; // surfaces steeper than ~45 degrees count as walls

    public bool IsGrounded { get; private set; }

    float horizontalMovement; 
    float lastGroundedTime = float.NegativeInfinity;
    float lastJumpPressedTime = float.NegativeInfinity;
    float lastJumpTime = float.NegativeInfinity;
    readonly ContactPoint2D[] contacts = new ContactPoint2D[16];

    void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();

        // Friction is applied in code, so the player doesn't stick to walls when pushing into them mid-air.
        // An Inspector-assigned material is kept.
        if (rb.sharedMaterial == null)
            rb.sharedMaterial = new PhysicsMaterial2D("PlayerFrictionless") { friction = 0f, bounciness = 0f };
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    void FixedUpdate()
    {
      UpdateGrounded();

      ApplyWalkForce();
      TryJump();
      ApplyGroundFriction();
      ApplyAirResistance();
    }

    public void Move(InputAction.CallbackContext context)
    {
      horizontalMovement = context.ReadValue<Vector2>().x;
    }

    public void Jump(InputAction.CallbackContext context)
    {
      if (context.performed) lastJumpPressedTime = Time.time;
    }

    void UpdateGrounded()
    {
      IsGrounded = false;

      // Contacts can linger for a step right after jumping, ignore them
      if (Time.time - lastJumpTime < 0.1f) return;

      int count = rb.GetContacts(contacts);
      for (int i = 0; i < count; i++)
      {
        if (contacts[i].normal.y >= minGroundNormalY)
        {
          IsGrounded = true;
          lastGroundedTime = Time.time;
          return;
        }
      }
    }

    void ApplyWalkForce()
    {
      if (horizontalMovement == 0f) return;

      float dir = Mathf.Sign(horizontalMovement);
      float targetSpeed = moveSpeed * Mathf.Abs(horizontalMovement);
      float speedAlongInput = rb.linearVelocity.x * dir;

      // Already faster than walking: holding the direction keeps momentum instead of braking
      if (speedAlongInput >= targetSpeed) return;

      float force = (IsGrounded ? groundMoveForce : airMoveForce) * Mathf.Abs(horizontalMovement);
      float maxForce = rb.mass * (targetSpeed - speedAlongInput) / Time.fixedDeltaTime; // don't overshoot this step
      rb.AddForce(new Vector2(dir * Mathf.Min(force, maxForce), 0f));
    }

    void TryJump()
    {
      bool jumpBuffered = Time.time - lastJumpPressedTime <= jumpBufferTime;
      bool canJump = Time.time - lastGroundedTime <= coyoteTime;
      if (!jumpBuffered || !canJump) return;

      lastJumpPressedTime = float.NegativeInfinity;
      lastGroundedTime = float.NegativeInfinity;
      lastJumpTime = Time.time;
      IsGrounded = false;

      // Cancel falling speed so a coyote-time jump is as strong as a normal one
      if (rb.linearVelocity.y < 0f) rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
      rb.AddForce(Vector2.up * jumpImpulse, ForceMode2D.Impulse);
    }

    void ApplyGroundFriction()
    {
      if (!IsGrounded) return;

      float vx = rb.linearVelocity.x;
      if (vx == 0f) return;

      // Walking with the motion at or under walk speed: let the walk force hold the speed
      bool walkingWithMotion = horizontalMovement != 0f
        && Mathf.Sign(horizontalMovement) == Mathf.Sign(vx)
        && Mathf.Abs(vx) <= moveSpeed * Mathf.Abs(horizontalMovement) + 0.01f;
      if (walkingWithMotion) return;

      float normalForce = rb.mass * Mathf.Abs(Physics2D.gravity.y * rb.gravityScale);
      float maxFriction = rb.mass * Mathf.Abs(vx) / Time.fixedDeltaTime; // stop, never reverse
      rb.AddForce(new Vector2(-Mathf.Sign(vx) * Mathf.Min(groundFriction * normalForce, maxFriction), 0f));
    }

    void ApplyAirResistance()
    {
      Vector2 v = rb.linearVelocity;
      rb.AddForce(-airDrag * v.magnitude * v);
    }
}
