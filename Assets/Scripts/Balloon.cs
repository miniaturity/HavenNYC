using UnityEngine;


[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class Balloon : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BoxCollider2D c;
    [SerializeField] private SpriteRenderer sr;
    [SerializeField] private Rigidbody2D rb;
    [Tooltip("Optional. If empty, looks for a scene object named 'Iceberg'.")]
    [SerializeField] private Collider2D iceberg;

    [Header("State (read-only)")]
    [SerializeField] private Team lastPlayerHit = Team.None;
    [SerializeField] private float height = 1.2f;
    [SerializeField] private float heightVelocity = 0f;

    [Header("Height physics")]
    [SerializeField] private float gravity = 10f;
    [SerializeField] private float maxFallSpeed = 7f;
    [SerializeField] private float maxRiseSpeed = 12f;
    [SerializeField] private float maxHeight = 8f;
    [SerializeField] private float serveHeight = 1.2f;
    [Tooltip("Balloon can only be hit while at or below this height.")]
    [SerializeField] private float maxHittableHeight = 2f;

    [Header("Hit response")]
    [SerializeField] private float liftFactor = 0.55f;
    [SerializeField] private float minHitLift = 5f;
    [SerializeField] private float horizontalFactor = 0.7f;
    [SerializeField] private float minHorizontalSpeed = 4f;
    [SerializeField] private float maxHorizontalSpeed = 11f;
    [SerializeField] private float slideHitBonus = 1.25f;
    [Range(0f, 1f)] [SerializeField] private float velocityDirWeight = 0.75f;
    [SerializeField] private float minPlayerSpeed = 0.1f;
    [SerializeField] private float airDrag = 0.7f;
    [SerializeField] private float hitCooldown = 0.15f;

    [Header("Look")]
    [SerializeField] private float baseScale = 1f;
    [SerializeField] private float scalePerHeight = 0.12f;
    [Tooltip("How far from the server (toward the middle) the balloon hovers at the start of a round.")]
    [SerializeField] private float serveDistance = 1.5f;

    public Team NextHitter { get; private set; } = Team.Red;

    private Vector2 groundVelocity;
    private float lastHitTime = -999f;
    private bool served = false;  
    private bool roundEnded = false;

    private float Scale => Mathf.Max(baseScale + height * scalePerHeight, 0.1f);

    void Awake()
    {
        c = GetComponent<BoxCollider2D>();
        sr = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();

        c.isTrigger = true;  
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
    }

    void Start()
    {
        if (iceberg == null)
        {
            GameObject g = GameObject.Find("Iceberg");
            if (g != null) iceberg = g.GetComponent<Collider2D>();
        }

        GameManager gm = GameManager.Instance;
        NextHitter = gm != null ? gm.Server : Team.Red;
        if (gm != null) gm.RegisterBalloon(this);

        PlaceForServe();
        height = serveHeight;
        heightVelocity = 0f;
        UpdateScale();
    }

    void Update()
    {
        UpdateScale();
    }

    void FixedUpdate()
    {
        if (roundEnded) return;

        groundVelocity *= Mathf.Max(0f, 1f - airDrag * Time.fixedDeltaTime);
        Vector2 next = rb.position + groundVelocity * Time.fixedDeltaTime;
        rb.MovePosition(next);

        if (!served) return; 
        heightVelocity -= gravity * Time.fixedDeltaTime;
        heightVelocity = Mathf.Clamp(heightVelocity, -maxFallSpeed, maxRiseSpeed);
        height += heightVelocity * Time.fixedDeltaTime;

        if (height > maxHeight)
        {
            height = maxHeight;
            heightVelocity = Mathf.Min(heightVelocity, 0f);
        }

        if (height <= 0f)
        {
            height = 0f;
            OnHitFloor(next);
        }
    }

    void OnTriggerEnter2D(Collider2D other) => TryHit(other);
    void OnTriggerStay2D(Collider2D other) => TryHit(other);

    void TryHit(Collider2D other)
    {
        if (roundEnded) return;
        if (Time.time - lastHitTime < hitCooldown) return;
        if (height > maxHittableHeight) return;

        IBalloonHitter hitter = other.GetComponentInParent<IBalloonHitter>();
        if (hitter == null) return;
        if (hitter.Team != NextHitter) return; 

        Vector2 playerVel = hitter.Velocity;
        float speed = playerVel.magnitude;

        Vector2 away = (Vector2)transform.position - hitter.Position;
        away = away.sqrMagnitude < 0.0001f ? Vector2.up : away.normalized;

        Vector2 dir = away;
        if (speed > minPlayerSpeed)
        {
            Vector2 moveDir = playerVel / speed;
            dir = Vector2.Lerp(away, moveDir, velocityDirWeight).normalized;
            if (dir == Vector2.zero) dir = moveDir;
        }

        float power = speed * (hitter.IsSliding ? slideHitBonus : 1f);

        groundVelocity = dir * Mathf.Clamp(power * horizontalFactor, minHorizontalSpeed, maxHorizontalSpeed);
        heightVelocity = Mathf.Clamp(power * liftFactor, minHitLift, maxRiseSpeed);

        lastPlayerHit = hitter.Team;
        NextHitter = hitter.Team.Opponent();
        lastHitTime = Time.time;
        served = true;
    }

    void OnHitFloor(Vector2 landingPos)
    {
        roundEnded = true;
        groundVelocity = Vector2.zero;
        heightVelocity = 0f;

        bool inBounds = iceberg == null || iceberg.OverlapPoint(landingPos);

        Team winner = inBounds ? NextHitter.Opponent() : NextHitter;
        string reason = inBounds ? "missed the balloon" : "balloon landed in the water";

        if (GameManager.Instance != null)
            GameManager.Instance.PointScored(winner, reason);
        else
            Debug.LogWarning("Balloon hit the floor but there is no GameManager.");
    }

    void PlaceForServe()
    {
        Player[] players = FindObjectsByType<Player>(FindObjectsSortMode.None);
        foreach (Player p in players)
        {
            if (((IBalloonHitter)p).Team != NextHitter) continue;

            Vector2 pos = ((IBalloonHitter)p).Position;
            float towardCenter = pos.x > 0f ? -1f : 1f;
            Vector2 spawn = pos + new Vector2(towardCenter * serveDistance, 0f);
            transform.position = new Vector3(spawn.x, spawn.y, transform.position.z);
            rb.position = spawn;
            return;
        }
    }

    void UpdateScale()
    {
        transform.localScale = new Vector3(Scale, Scale, 1f);
    }
}