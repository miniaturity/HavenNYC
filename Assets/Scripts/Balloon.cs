using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

enum Touch
{
    Red,
    Blue,
    None
}

public class Balloon : MonoBehaviour
{
    private const float SLIDE_MULTIPLIER = 2f;
    private const float DEFAULT_MULTIPLIER = 1.5f;

    [SerializeField] private Touch lastPlayerHit = Touch.None;
    [SerializeField] private BoxCollider2D c;
    [SerializeField] private SpriteRenderer sr;
    

    private Contact[] contacts = new Contact[] {};
    private float scale => Math.Max(transform.position.z * 0.5f, 1f);

    void Start()
    {
        c = GetComponent<BoxCollider2D>();
        sr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        if (sr == null || c == null)
        {
            Debug.LogError("Balloon SR or C is not defined!");
            return;
        }

        UpdateScale();
    }

    void UpdateScale()
    {
        transform.localScale = new Vector3(scale, scale, 1f);
    }
}

class Contact
{
    public Touch owner { get; }
    public Vector2 dir  { get; }
    public bool slideHit { get; }
    public Contact(Touch owner_, Vector2 dir_, bool slideHit_ = false)
    {
        owner = owner_;
        dir = dir_;
        slideHit = slideHit_;
    }
}
