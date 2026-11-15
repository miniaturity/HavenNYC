using UnityEngine;

/// <summary>
/// Penguin controls:
///   Walk:   move keys (icy: you accelerate slowly and glide when you let go)
///   Slide:  HOLD the slide key (arrow appears, grows as power charges), aim with the move keys,
///           RELEASE to slide. The arrow length is exactly how far the slide will travel.
///   Red:  WASD + Left Shift     Blue: Arrow keys + Right Shift
/// Setup: SpriteRenderer + Player + a non-trigger Collider2D (e.g. CircleCollider2D). Set "team".
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Player : MonoBehaviour, IBalloonHitter
{
    public Team team = Team.Red;

    [Header("Walking (icy)")]
    [SerializeField] private float walkSpeed = 4.5f;
    [Tooltip("How quickly you speed up / change direction. Lower = more slippery.")]
    [SerializeField] private float walkAcceleration = 12f;
    [Tooltip("How quickly you slow down when you let go. Lower = longer glide.")]
    [SerializeField] private float iceFriction = 4f;

    [Header("Slide")]
    [SerializeField] private float chargeTime = 0.8f;    
    [SerializeField] private float minSlideSpeed = 5f;
    [SerializeField] private float maxSlideSpeed = 14f;
    [SerializeField] private float slideDeceleration = 16f; 
    [SerializeField] private float slideCooldown = 0.35f;
    [Tooltip("How fast the aim arrow swings toward the direction you press. Lower = smoother/slower.")]
    [SerializeField] private float aimSmoothing = 12f;

    [Header("Sprites")]
    [Tooltip("Shown while walking, gliding, standing or charging a slide.")]
    [SerializeField] private Sprite standingSprite;
    [Tooltip("Shown while sliding.")]
    [SerializeField] private Sprite slidingSprite;
    [Tooltip("Tick if the standing sprite is drawn facing right. Untick if it faces left.")]
    [SerializeField] private bool spriteFacesRight = true;
    [Tooltip("Tick if the sliding sprite is drawn facing right. Untick if it faces left.")]
    [SerializeField] private bool slidingSpriteFacesRight = true;

    [Header("Arrow")]
    [SerializeField] private float arrowWidth = 0.18f;
    [SerializeField] private float arrowHeadWidth = 0.55f;
    [SerializeField] private float arrowHeadLength = 0.5f;
    [SerializeField] private int arrowSortingOrder = 50;

    private enum State { Idle, Charging, Sliding }

    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private Arena arena;
    private LineRenderer shaft;
    private LineRenderer head;

    private State state = State.Idle;
    private Vector2 aimDir = Vector2.right;
    private float aimAngle; 
    private Vector2 slideDir;
    private Vector2 velocity;
    private Vector2 moveInput;
    private float charge;  
    private float slideSpeed;
    private float cooldownTimer;
    private bool facingRight = true;
    private bool fell;

    Team IBalloonHitter.Team => team;
    bool IBalloonHitter.IsSliding => state == State.Sliding;
    Vector2 IBalloonHitter.Velocity => velocity;
    Vector2 IBalloonHitter.Position => transform.position;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;  

        sr = GetComponentInChildren<SpriteRenderer>();

        arena = Arena.Get();

        aimDir = transform.position.x > 0f ? Vector2.left : Vector2.right;
        facingRight = aimDir.x > 0f;
        aimAngle = aimDir.x > 0f ? 0f : 180f;

        BuildArrow();
        UpdateFacing();
        UpdateSprite();
    }

    void Update()
    {
        if (fell)
        {
            transform.localScale = Vector3.MoveTowards(transform.localScale, Vector3.zero, 1.5f * Time.deltaTime);
            SetArrowVisible(false);
            return;
        }

        if (GameManager.Instance != null && GameManager.Instance.RoundOver)
        {
            moveInput = Vector2.zero;                       // no input, but you keep gliding
            if (state == State.Charging) state = State.Idle;
            SetArrowVisible(false);
            return;
        }

        ReadInput(out Vector2 input, out bool slideHeld);
        moveInput = Vector2.ClampMagnitude(input, 1f);
        cooldownTimer -= Time.deltaTime;

        if (state != State.Sliding)
        {
            if (slideHeld && cooldownTimer <= 0f)
            {
                bool hasInput = input.sqrMagnitude > 0.01f;
                float targetAngle = hasInput ? Mathf.Atan2(input.y, input.x) * Mathf.Rad2Deg : aimAngle;

                if (state != State.Charging)
                {
                    state = State.Charging;
                    charge = 0f;
                    aimAngle = targetAngle;   // start aiming exactly where you're already pressing
                }
                else if (hasInput)
                {
                    // Ease toward the pressed direction (frame-rate independent), so keyboard
                    // 45-degree steps become a smooth sweep instead of a snap.
                    aimAngle = Mathf.LerpAngle(aimAngle, targetAngle, 1f - Mathf.Exp(-aimSmoothing * Time.deltaTime));
                }

                float rad = aimAngle * Mathf.Deg2Rad;
                aimDir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                charge = Mathf.Clamp01(charge + Time.deltaTime / Mathf.Max(chargeTime, 0.01f));
            }
            else if (state == State.Charging)
            {
                StartSlide();
            }
        }

        UpdateFacing();
        UpdateArrow();
    }

    void FixedUpdate()
    {
        if (fell) return;

        Vector2 step;

        if (state == State.Sliding)
        {
            float oldSpeed = slideSpeed;
            slideSpeed = Mathf.MoveTowards(slideSpeed, 0f, slideDeceleration * Time.fixedDeltaTime);
            velocity = slideDir * slideSpeed;
            step = slideDir * ((oldSpeed + slideSpeed) * 0.5f * Time.fixedDeltaTime); // exact for constant decel

            if (slideSpeed <= 0.01f)
            {
                velocity = Vector2.zero;
                state = State.Idle;
                cooldownTimer = slideCooldown;
            }
        }
        else
        {
            Vector2 target = state == State.Idle ? moveInput * walkSpeed : Vector2.zero;
            float rate = target.sqrMagnitude > 0.0001f ? walkAcceleration : iceFriction;
            velocity = Vector2.MoveTowards(velocity, target, rate * Time.fixedDeltaTime);
            step = velocity * Time.fixedDeltaTime;
        }

        rb.MovePosition(rb.position + step);
        CheckFall();
    }

    void StartSlide()
    {
        slideDir = aimDir;
        slideSpeed = Mathf.Lerp(minSlideSpeed, maxSlideSpeed, charge);
        velocity = slideDir * slideSpeed;
        state = State.Sliding;
        SetArrowVisible(false);
    }

    void CheckFall()
    {
        if (arena == null || arena.Contains(rb.position)) return;

        fell = true;
        velocity = Vector2.zero;
        if (GameManager.Instance != null) GameManager.Instance.OffBoard(team);
    }

    void UpdateFacing()
    {
        float x;
        if (state == State.Sliding) x = slideDir.x;
        else if (state == State.Charging) x = aimDir.x;
        else x = moveInput.x;

        if (Mathf.Abs(x) > 0.1f) facingRight = x > 0f;
    }

    void UpdateSprite()
    {
        if (sr == null) return;

        bool sliding = state == State.Sliding && slidingSprite != null;
        Sprite wanted = sliding ? slidingSprite : standingSprite;
        if (wanted != null && sr.sprite != wanted) sr.sprite = wanted;

        bool spriteRight = sliding ? slidingSpriteFacesRight : spriteFacesRight;
        sr.flipX = facingRight != spriteRight;
    }

    void LateUpdate()
    {
        UpdateSprite();
    }

    void ReadInput(out Vector2 dir, out bool slideHeld)
    {
        dir = Vector2.zero;
        slideHeld = false;
        bool red = team == Team.Red;


        if (Input.GetKey(red ? KeyCode.A : KeyCode.LeftArrow)) dir.x -= 1f;
        if (Input.GetKey(red ? KeyCode.D : KeyCode.RightArrow)) dir.x += 1f;
        if (Input.GetKey(red ? KeyCode.S : KeyCode.DownArrow)) dir.y -= 1f;
        if (Input.GetKey(red ? KeyCode.W : KeyCode.UpArrow)) dir.y += 1f;
        slideHeld = Input.GetKey(red ? KeyCode.LeftShift : KeyCode.RightShift);
    }

    void BuildArrow()
    {
        Material mat = new Material(Shader.Find("Sprites/Default"));
        Color col = team.GetColor();

        shaft = MakeLine("ArrowShaft", mat, col);
        shaft.startWidth = arrowWidth;
        shaft.endWidth = arrowWidth;

        head = MakeLine("ArrowHead", mat, col);
        head.startWidth = arrowHeadWidth;
        head.endWidth = 0f;   // tapers to a point

        SetArrowVisible(false);
    }

    LineRenderer MakeLine(string objName, Material mat, Color col)
    {
        GameObject go = new GameObject(objName);
        go.transform.SetParent(transform, false);

        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.material = mat;
        lr.startColor = col;
        lr.endColor = col;
        lr.sortingOrder = arrowSortingOrder;
        lr.numCapVertices = 0;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        return lr;
    }

    void SetArrowVisible(bool visible)
    {
        if (shaft != null) shaft.enabled = visible;
        if (head != null) head.enabled = visible;
    }

    void UpdateArrow()
    {
        bool show = state == State.Charging;
        SetArrowVisible(show);
        if (!show) return;

        float v = Mathf.Lerp(minSlideSpeed, maxSlideSpeed, charge);
        float length = v * v / (2f * slideDeceleration);   // exact slide distance
        float headLen = Mathf.Min(arrowHeadLength, length);

        Vector3 origin = transform.position;
        Vector3 dir = new Vector3(aimDir.x, aimDir.y, 0f);
        Vector3 shaftEnd = origin + dir * (length - headLen);
        Vector3 tip = origin + dir * length;

        shaft.SetPosition(0, origin);
        shaft.SetPosition(1, shaftEnd);
        head.SetPosition(0, shaftEnd);
        head.SetPosition(1, tip);
    }
}