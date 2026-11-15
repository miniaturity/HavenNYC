using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class Balloon : MonoBehaviour
{
    [Header("References (auto-filled)")]
    [SerializeField] private BoxCollider2D c;
    [SerializeField] private SpriteRenderer sr;
    [SerializeField] private Rigidbody2D rb;
    private Arena arena;

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

    [Header("Invisible walls")]
    [Tooltip("Bounce off the edge of the Arena instead of flying out over the water.")]
    [SerializeField] private bool bounceOffWalls = true;
    [Range(0f, 1f)] [SerializeField] private float wallBounciness = 0.8f;

    [Header("Look")]
    [SerializeField] private float baseScale = 1f;
    [SerializeField] private float scalePerHeight = 0.12f;
    [Tooltip("World units the balloon is drawn above the ground per unit of height.")]
    [SerializeField] private float visualLiftPerHeight = 0.3f;
    [Tooltip("How far from the server (toward the middle) the balloon hovers at the start of a round.")]
    [SerializeField] private float serveDistance = 1.5f;

    [Header("Shadow")]
    [SerializeField] private float shadowWidth = 0.9f;
    [Range(0.2f, 1f)] [SerializeField] private float shadowSquash = 0.5f;
    [SerializeField] private float shadowShrinkPerHeight = 0.1f;
    [Range(0f, 1f)] [SerializeField] private float shadowMaxAlpha = 0.35f;
    [Range(0f, 1f)] [SerializeField] private float shadowMinAlpha = 0.12f;
    [SerializeField] private Vector2 shadowOffset = new Vector2(0f, -0.1f);

    public Team NextHitter { get; private set; } = Team.Red;

    private Vector2 groundVelocity;
    private float lastHitTime = -999f;
    private bool served = false;
    private bool roundEnded = false;

    private Transform visual;
    private SpriteRenderer visualSr;
    private Transform shadow;
    private SpriteRenderer shadowSr;

    private float Scale => Mathf.Max(baseScale + height * scalePerHeight, 0.1f);

    void Awake()
    {
        c = GetComponent<BoxCollider2D>();
        sr = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();

        c.isTrigger = true;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        transform.localScale = Vector3.one * baseScale;
        BuildVisuals();
    }

    void Start()
    {
        arena = Arena.Get();

        GameManager gm = GameManager.Instance;
        NextHitter = gm != null ? gm.Server : Team.Red;
        if (gm != null) gm.RegisterBalloon(this);

        PlaceForServe();
        height = serveHeight;
        heightVelocity = 0f;
        UpdateVisuals();
    }

    void LateUpdate()
    {
        UpdateVisuals();
    }

    void FixedUpdate()
    {
        if (roundEnded) return;

        groundVelocity *= Mathf.Max(0f, 1f - airDrag * Time.fixedDeltaTime);
        Vector2 next = rb.position + groundVelocity * Time.fixedDeltaTime;

        if (bounceOffWalls && arena != null && !arena.Contains(next))
            next = BounceOffWall(next);

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

    Vector2 BounceOffWall(Vector2 outsidePos)
    {
        arena.Clamp(outsidePos, out Vector2 edge, out Vector2 normal);

        if (Vector2.Dot(groundVelocity, normal) > 0f)
            groundVelocity = Vector2.Reflect(groundVelocity, normal) * wallBounciness;

        return edge - normal * 0.02f;
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

        bool inBounds = arena == null || arena.Contains(landingPos);

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

    void BuildVisuals()
    {
        if (sr == null)
        {
            Debug.LogWarning("Balloon needs a SpriteRenderer.");
            return;
        }

        GameObject sg = new GameObject("Shadow");
        shadow = sg.transform;
        shadow.SetParent(transform, false);
        shadowSr = sg.AddComponent<SpriteRenderer>();
        shadowSr.sprite = MakeShadowSprite();
        shadowSr.sortingLayerID = sr.sortingLayerID;
        shadowSr.sortingOrder = sr.sortingOrder;

        GameObject vg = new GameObject("Visual");
        visual = vg.transform;
        visual.SetParent(transform, false);
        visualSr = vg.AddComponent<SpriteRenderer>();
        visualSr.sprite = sr.sprite;
        visualSr.color = sr.color;
        visualSr.sharedMaterial = sr.sharedMaterial;
        visualSr.sortingLayerID = sr.sortingLayerID;
        visualSr.sortingOrder = sr.sortingOrder + 1;
        sr.enabled = false;
    }

    Sprite MakeShadowSprite()
    {
        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2(size - 1, size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center) / (size * 0.5f);
                float a = Mathf.Clamp01((1f - d) * 4f);
                tex.SetPixel(x, y, new Color(0f, 0f, 0f, a));
            }
        }
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    void UpdateVisuals()
    {
        if (visual == null || shadow == null) return;

        float inv = 1f / Mathf.Max(baseScale, 0.01f);

        visual.position = transform.position + Vector3.up * (height * visualLiftPerHeight);
        visual.localScale = new Vector3(Scale * inv, Scale * inv, 1f);

        float s = shadowWidth / (1f + height * shadowShrinkPerHeight);
        shadow.position = transform.position + (Vector3)shadowOffset;
        shadow.localScale = new Vector3(s * inv, s * shadowSquash * inv, 1f);

        float t = Mathf.Clamp01(height / Mathf.Max(maxHeight, 0.01f));
        shadowSr.color = new Color(0f, 0f, 0f, Mathf.Lerp(shadowMaxAlpha, shadowMinAlpha, t));
    }
}