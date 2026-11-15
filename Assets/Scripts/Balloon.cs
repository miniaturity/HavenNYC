using System;
using System.Collections.Generic;
using UnityEngine;

public enum Touch
{
    Red,
    Blue,
    None
}

public interface IBalloonHitter
{
    Touch Team { get; }
    bool IsSliding { get; }
}

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class Balloon : MonoBehaviour
{
    private const float SLIDE_MULTIPLIER = 2f;
    private const float DEFAULT_MULTIPLIER = 1.5f;

    [SerializeField] private BoxCollider2D c;
    [SerializeField] private SpriteRenderer sr;
    [SerializeField] private Rigidbody2D rb;

    [SerializeField] private Touch lastPlayerHit = Touch.None;
    [SerializeField] private float height = 2f;
    [SerializeField] private float heightVelocity = 0f;

    [SerializeField] private float gravity = 4f;
    [SerializeField] private float maxFallSpeed = 1.2f;
    [SerializeField] private float maxRiseSpeed = 14f;
    [SerializeField] private float maxHeight = 12f;
    [SerializeField] private float verticalFactor = 0.8f;
    [SerializeField] private float minHitLift = 2.5f;
    [SerializeField] private float maxHittableHeight = 1.5f;

    [SerializeField] private float horizontalFactor = 0.9f;
    [SerializeField] private float maxHorizontalSpeed = 15f;
    [Range(0f, 1f)] [SerializeField] private float velocityDirWeight = 0.75f;
    [SerializeField] private float minPlayerSpeed = 0.1f;
    [SerializeField] private float airDrag = 0.6f;

    [SerializeField] private float baseScale = 1f;
    [SerializeField] private float scalePerHeight = 0.15f;

    [SerializeField] private float hitCooldown = 0.15f;

    private readonly List<Contact> contacts = new List<Contact>();
    private float lastHitTime = -999f;
    private bool gameOverSent = false;

    private float scale => Math.Max(baseScale + height * scalePerHeight, 0.1f);

    private Vector2 Velocity
    {
        get => rb.velocity;
        set => rb.velocity = value;
    }

    void Start()
    {
        c = GetComponent<BoxCollider2D>();
        sr = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();

        rb.gravityScale = 0f;

        rb.drag = airDrag;
        rb.freezeRotation = true;

        UpdateScale();
    }

    void Update()
    {
        if (sr == null || c == null || rb == null)
        {
            Debug.LogError("Balloon SR, C or RB is not defined!");
            return;
        }

        UpdateScale();
    }

    void FixedUpdate()
    {
        if (gameOverSent) return;

        foreach (Contact contact in contacts)
            ApplyContact(contact);
        contacts.Clear();

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
            OnHitFloor();
        }
    }

    void OnCollisionEnter2D(Collision2D collision) => TryRegisterHit(collision);
    void OnCollisionStay2D(Collision2D collision) => TryRegisterHit(collision);

    void TryRegisterHit(Collision2D collision)
    {
        if (gameOverSent) return;
        if (Time.time - lastHitTime < hitCooldown) return;
        if (height > maxHittableHeight) return;

        IBalloonHitter hitter = collision.collider.GetComponentInParent<Player>().hitter;
        if (hitter == null) return;

        Vector2 playerVel = collision.rigidbody != null
            ? collision.rigidbody.velocity
            : -collision.relativeVelocity;

        float speed = playerVel.magnitude;

        Vector2 away = ((Vector2)transform.position - (Vector2)collision.transform.position).normalized;
        if (away == Vector2.zero) away = Vector2.up;

        Vector2 dir = away;
        if (speed > minPlayerSpeed)
        {
            Vector2 moveDir = playerVel / speed;
            dir = Vector2.Lerp(away, moveDir, velocityDirWeight).normalized;
            if (dir == Vector2.zero) dir = moveDir;
        }

        contacts.Add(new Contact(hitter.Team, dir, speed, hitter.IsSliding));
        lastHitTime = Time.time;
    }

    void ApplyContact(Contact contact)
    {
        float multiplier = contact.slideHit ? SLIDE_MULTIPLIER : DEFAULT_MULTIPLIER;
        float power = contact.speed * multiplier;

        lastPlayerHit = contact.owner;

        heightVelocity = Mathf.Clamp(power * verticalFactor, minHitLift, maxRiseSpeed);

        Vector2 horizontal = contact.dir * Mathf.Min(power * horizontalFactor, maxHorizontalSpeed);
        Velocity = horizontal;
    }


    void OnHitFloor()
    {
        if (gameOverSent) return;
        gameOverSent = true;

        Velocity = Vector2.zero;
        heightVelocity = 0f;

        if (GameManager.Instance != null)
            GameManager.Instance.OnBalloonHitFloor(lastPlayerHit);
        else
            Debug.LogWarning("Balloon hit the floor but no GameManager.Instance exists.");
    }


    void UpdateScale()
    {
        transform.localScale = new Vector3(scale, scale, 1f);
    }

    public void ResetBalloon(Vector2 position, float startHeight = 2f)
    {
        transform.position = new Vector3(position.x, position.y, transform.position.z);
        height = startHeight;
        heightVelocity = 0f;
        Velocity = Vector2.zero;
        lastPlayerHit = Touch.None;
        gameOverSent = false;
        contacts.Clear();
        UpdateScale();
    }
}

class Contact
{
    public Touch owner { get; }
    public Vector2 dir { get; }
    public float speed { get; }
    public bool slideHit { get; }

    public Contact(Touch owner_, Vector2 dir_, float speed_, bool slideHit_ = false)
    {
        owner = owner_;
        dir = dir_;
        speed = speed_;
        slideHit = slideHit_;
    }
}