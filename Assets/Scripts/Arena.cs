using UnityEngine;

public class Arena : MonoBehaviour
{
    [Header("Exact shape (optional)")]
    [Tooltip("Use this collider as the playable area. If empty, a collider on this object/children is used.")]
    [SerializeField] private Collider2D shapeCollider;

    [Header("Manual size")]
    [SerializeField] private bool useManualSize = false;
    [SerializeField] private Vector2 manualSize = new Vector2(14f, 7f);
    [Tooltip("Offset from this object's position.")]
    [SerializeField] private Vector2 manualOffset = Vector2.zero;

    [Header("Auto-fit (sprite / camera)")]
    [Tooltip("Ellipse suits an iceberg better than a rectangle.")]
    [SerializeField] private bool ellipse = true;
    [Range(0.3f, 1f)] [SerializeField] private float fit = 0.9f;

    private static Arena cached;

    private Collider2D shape;
    private Vector2 center;
    private Vector2 half = Vector2.one;
    private bool ready;

    public static Arena Get()
    {
        if (cached != null) return cached;

        cached = FindFirstObjectByType<Arena>();
        if (cached != null) return cached;

        GameObject g = GameObject.Find("Iceberg");
        if (g == null)
        {
            g = new GameObject("Arena (auto)");
            Debug.LogWarning("No 'Iceberg' object or Arena found, so the camera view is used as the playable area. " +
                             "Add an Arena component to your iceberg to set the real boundary.");
        }

        cached = g.AddComponent<Arena>();
        return cached;
    }

    void Awake()
    {
        Compute();
        ready = true;
    }

    void EnsureReady()
    {
        if (ready) return;
        Compute();
        ready = true;
    }

    void Compute()
    {
        shape = null;

        if (!useManualSize)
        {
            Collider2D col = shapeCollider != null ? shapeCollider : GetComponentInChildren<Collider2D>();
            if (col != null && col.enabled) shape = col;
        }

        if (shape != null)
        {
            center = shape.bounds.center;
            half = shape.bounds.extents;
            return;
        }

        if (useManualSize)
        {
            center = (Vector2)transform.position + manualOffset;
            half = manualSize * 0.5f;
        }
        else
        {
            SpriteRenderer spr = GetComponentInChildren<SpriteRenderer>();
            Camera cam = Camera.main;

            if (spr != null && spr.sprite != null)
            {
                center = spr.bounds.center;
                half = (Vector2)spr.bounds.extents * fit;
            }
            else if (cam != null && cam.orthographic)
            {
                center = cam.transform.position;
                half = new Vector2(cam.orthographicSize * cam.aspect, cam.orthographicSize) * fit;
            }
            else
            {
                center = transform.position;
                half = manualSize * 0.5f;
            }
        }

        half = new Vector2(Mathf.Max(half.x, 0.01f), Mathf.Max(half.y, 0.01f));
    }

    public bool Contains(Vector2 p)
    {
        EnsureReady();

        if (shape != null) return shape.OverlapPoint(p);

        Vector2 d = p - center;
        if (ellipse)
        {
            float x = d.x / half.x;
            float y = d.y / half.y;
            return x * x + y * y <= 1f;
        }
        return Mathf.Abs(d.x) <= half.x && Mathf.Abs(d.y) <= half.y;
    }

    public void Clamp(Vector2 p, out Vector2 edge, out Vector2 normal)
    {
        EnsureReady();

        if (shape != null)
        {
            edge = shape.ClosestPoint(p);
            normal = p - edge;
            if (normal.sqrMagnitude < 0.000001f) normal = p - center;
        }
        else if (ellipse)
        {
            Vector2 d = p - center;
            Vector2 q = new Vector2(d.x / half.x, d.y / half.y);
            Vector2 u = q.sqrMagnitude < 0.000001f ? Vector2.right : q.normalized;
            Vector2 offset = new Vector2(u.x * half.x, u.y * half.y);
            edge = center + offset;
            normal = new Vector2(offset.x / (half.x * half.x), offset.y / (half.y * half.y)); // ellipse gradient
        }
        else
        {
            edge = new Vector2(
                Mathf.Clamp(p.x, center.x - half.x, center.x + half.x),
                Mathf.Clamp(p.y, center.y - half.y, center.y + half.y));
            normal = p - edge;
        }

        if (normal.sqrMagnitude < 0.000001f) normal = Vector2.up;
        normal.Normalize();
    }

    void OnDrawGizmos()
    {
        Compute();
        if (shape != null) return; 

        Gizmos.color = Color.cyan;
        if (ellipse)
        {
            const int segments = 64;
            Vector3 prev = center + new Vector2(half.x, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                Vector3 next = center + new Vector2(Mathf.Cos(a) * half.x, Mathf.Sin(a) * half.y);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
        else
        {
            Gizmos.DrawWireCube(center, half * 2f);
        }
    }
}