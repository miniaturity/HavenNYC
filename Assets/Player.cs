using UnityEngine;


[RequireComponent(typeof(Rigidbody2D))]
public class Player : MonoBehaviour, IBalloonHitter
{
    public Team team = Team.Red;

    [Header("Walking")]
    [SerializeField] private float walkSpeed = 3.5f;

    [Header("Slide")]
    [SerializeField] private float chargeTime = 0.8f;
    [SerializeField] private float minSlideSpeed = 5f;
    [SerializeField] private float maxSlideSpeed = 14f;
    [SerializeField] private float slideDeceleration = 16f; 
    [SerializeField] private float slideCooldown = 0.35f;

    [Header("Arrow")]
    [SerializeField] private float arrowWidth = 0.18f;
    [SerializeField] private float arrowHeadWidth = 0.55f;
    [SerializeField] private float arrowHeadLength = 0.5f;
    [SerializeField] private int arrowSortingOrder = 50;

    private enum State { Idle, Charging, Sliding }

    private Rigidbody2D rb;
    private Collider2D iceberg;
    private LineRenderer shaft;
    private LineRenderer head;

    private State state = State.Idle;
    private Vector2 aimDir = Vector2.right;
    private Vector2 slideDir;
    private Vector2 velocity;
    private float charge; 
    private float slideSpeed;
    private float cooldownTimer;
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

        GameObject g = GameObject.Find("Iceberg");
        if (g != null) iceberg = g.GetComponent<Collider2D>();

        aimDir = transform.position.x > 0f ? Vector2.left : Vector2.right;

        BuildArrow();
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
            velocity = Vector2.zero;
            state = State.Idle;
            SetArrowVisible(false);
            return;
        }

        ReadInput(out Vector2 input, out bool slideHeld);
        cooldownTimer -= Time.deltaTime;

        if (state != State.Sliding)
        {
            if (slideHeld && cooldownTimer <= 0f)
            {
                if (state != State.Charging)
                {
                    state = State.Charging;
                    charge = 0f;
                }
                if (input.sqrMagnitude > 0.01f) aimDir = input.normalized;
                charge = Mathf.Clamp01(charge + Time.deltaTime / Mathf.Max(chargeTime, 0.01f));
                velocity = Vector2.zero;
            }
            else if (state == State.Charging)
            {
                StartSlide();
            }
            else
            {
                velocity = Vector2.ClampMagnitude(input, 1f) * walkSpeed;
            }
        }

        UpdateArrow();
    }

    void FixedUpdate()
    {
        if (fell) return;

        Vector2 step = velocity * Time.fixedDeltaTime;

        if (state == State.Sliding)
        {
            float oldSpeed = slideSpeed;
            slideSpeed = Mathf.MoveTowards(slideSpeed, 0f, slideDeceleration * Time.fixedDeltaTime);
            velocity = slideDir * slideSpeed;
            step = slideDir * ((oldSpeed + slideSpeed) * 0.5f * Time.fixedDeltaTime);
            if (slideSpeed <= 0.01f)
            {
                velocity = Vector2.zero;
                state = State.Idle;
                cooldownTimer = slideCooldown;
            }
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
        if (iceberg == null || state == State.Sliding) return;
        if (iceberg.OverlapPoint(rb.position)) return;

        fell = true;
        velocity = Vector2.zero;
        if (GameManager.Instance != null) GameManager.Instance.OffBoard(team);
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
        slideHeld = Input.GetKey(red ? KeyCode.Space : KeyCode.RightShift);
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
        head.endWidth = 0f; 

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
        float length = v * v / (2f * slideDeceleration); 
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