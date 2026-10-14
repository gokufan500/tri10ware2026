using UnityEngine;
using UnityEngine.InputSystem;

// Left click fires a hook toward the cursor; holding keeps you attached, releasing lets go.
// The rope is a DistanceJoint2D with maxDistanceOnly, so it can never stretch past its length but can go slack.
public class grappling_hook : MonoBehaviour
{
    public Rigidbody2D rb;

    [Header("Hook")]
    [Range(0f, 1f)] public float rangeScreenDiagonals = 0.3f; // max range as a fraction of the screen's diagonal
    public float speedScreenWidths = 2f; // hook travel speed in screen widths per second
    public LayerMask hookableLayers = ~0; // any collider that isn't a trigger counts as a valid surface

    [Header("Rope Visual")]
    public LineRenderer rope; // created at runtime if left empty
    public Material ropeMaterial;
    public float ropeWidth = 0.05f;
    public Color ropeColor = Color.white;

    public bool IsFiring { get; private set; }
    public bool IsAttached { get; private set; }
    public float RopeLength => joint.distance;
    public Vector2 HookPoint => attachedBody != null ? (Vector2)attachedBody.transform.TransformPoint(joint.connectedAnchor) : joint.connectedAnchor;

    DistanceJoint2D joint;
    Rigidbody2D attachedBody;
    Vector2 hookTip, hookDir;
    float hookRange, hookSpeed, hookTraveled;
    ContactFilter2D hookFilter;
    readonly RaycastHit2D[] hits = new RaycastHit2D[8];

    void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();

        joint = gameObject.AddComponent<DistanceJoint2D>();
        joint.enabled = false;
        joint.autoConfigureDistance = false;
        joint.autoConfigureConnectedAnchor = false;
        joint.maxDistanceOnly = true;
        joint.enableCollision = true;
        joint.anchor = Vector2.zero;

        hookFilter = new ContactFilter2D { useTriggers = false, useLayerMask = true, layerMask = hookableLayers };

        if (rope == null) rope = CreateRope();
        rope.enabled = false;
    }

    // Wire to PlayerInput > Player > Attack. Press fires, release lets go.
    public void Hook(InputAction.CallbackContext context)
    {
      if (context.performed) Fire(CursorWorldPoint());
      else if (context.canceled) Release();
    }

    public void Fire(Vector2 target)
    {
      Release();

      Vector2 toTarget = target - rb.position;
      if (toTarget.sqrMagnitude < 0.0001f) return;

      GetScreenSizeInWorld(out float screenWidth, out float screenDiagonal);
      hookRange = rangeScreenDiagonals * screenDiagonal;
      hookSpeed = speedScreenWidths * screenWidth;
      hookDir = toTarget.normalized;
      hookTip = rb.position;
      hookTraveled = 0f;
      IsFiring = true;
    }

    // Letting go doesn't touch the velocity, so momentum carries straight on
    public void Release()
    {
      IsFiring = false;
      if (!IsAttached) return;

      IsAttached = false;
      joint.enabled = false;
      joint.connectedBody = null;
      attachedBody = null;
    }

    void FixedUpdate()
    {
      if (IsFiring) MoveHookTip();
    }

    void LateUpdate()
    {
      bool visible = IsFiring || IsAttached;
      rope.enabled = visible;
      if (!visible) return;

      rope.SetPosition(0, transform.position);
      rope.SetPosition(1, IsAttached ? HookPoint : hookTip);
    }

    void MoveHookTip()
    {
      float step = Mathf.Min(hookSpeed * Time.fixedDeltaTime, hookRange - hookTraveled);
      Vector2 nextTip = hookTip + hookDir * step;

      if (FindNearestHit(hookTip, nextTip, out RaycastHit2D hit))
      {
        Attach(hit);
        return;
      }

      hookTip = nextTip;
      hookTraveled += step;
      if (hookTraveled >= hookRange - 0.0001f) IsFiring = false; // out of range, nothing hit
    }

    bool FindNearestHit(Vector2 from, Vector2 to, out RaycastHit2D nearest)
    {
      nearest = default;
      float nearestDistance = float.PositiveInfinity;

      int count = Physics2D.Linecast(from, to, hookFilter, hits);
      for (int i = 0; i < count; i++)
      {
        if (hits[i].collider.attachedRigidbody == rb) continue; // don't hook yourself
        if (hits[i].distance < nearestDistance)
        {
          nearestDistance = hits[i].distance;
          nearest = hits[i];
        }
      }
      return nearestDistance < float.PositiveInfinity;
    }

    void Attach(RaycastHit2D hit)
    {
      IsFiring = false;
      IsAttached = true;

      // Hooking something with a Rigidbody2D (e.g. a moving object) follows it, otherwise the hook point is fixed in the world
      attachedBody = hit.rigidbody;
      joint.connectedBody = attachedBody;
      joint.connectedAnchor = attachedBody != null ? (Vector2)attachedBody.transform.InverseTransformPoint(hit.point) : hit.point;

      // Rope length is locked to the distance at the moment the hook lands
      joint.distance = Vector2.Distance(rb.position, hit.point);
      joint.enabled = true;
    }

    Vector2 CursorWorldPoint()
    {
      Camera cam = Camera.main;
      if (cam == null || Pointer.current == null) return rb.position + Vector2.up;

      Vector3 screenPoint = Pointer.current.position.ReadValue();
      screenPoint.z = -cam.transform.position.z;
      return cam.ScreenToWorldPoint(screenPoint);
    }

    void GetScreenSizeInWorld(out float width, out float diagonal)
    {
      Camera cam = Camera.main;
      if (cam == null)
      {
        width = 16f;
        diagonal = 18f;
        return;
      }

      float depth = -cam.transform.position.z;
      Vector2 bottomLeft = cam.ViewportToWorldPoint(new Vector3(0f, 0f, depth));
      Vector2 topRight = cam.ViewportToWorldPoint(new Vector3(1f, 1f, depth));
      width = topRight.x - bottomLeft.x;
      diagonal = (topRight - bottomLeft).magnitude;
    }

    LineRenderer CreateRope()
    {
      var ropeObject = new GameObject("Rope");
      ropeObject.transform.SetParent(transform, false);

      var line = ropeObject.AddComponent<LineRenderer>();
      line.positionCount = 2;
      line.useWorldSpace = true;
      line.startWidth = line.endWidth = ropeWidth;
      line.startColor = line.endColor = ropeColor;
      line.sortingOrder = -1; // behind the cat

      if (ropeMaterial == null)
      {
        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader != null) ropeMaterial = new Material(shader);
      }
      if (ropeMaterial != null) line.material = ropeMaterial;
      return line;
    }
}
