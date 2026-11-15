using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.XR;

public class Player : MonoBehaviour
{
    public Touch playerType;
    public GameManager gm;
    public IBalloonHitter hitter => playerType == Touch.Red ? new Red(this) : new Blue(this);

    [Header("Movement")]
    public float acceleration;
    public float maxSpeed = 20;
    public Rigidbody2D rb;
    private Vector2 dir = Vector2.zero;

    [Header("Sliding")]
    public bool isSliding;
    public float slideSpeed;
    public float maxSlidePower;
    private float slidePower;
    private bool isCharging = false;

    private void Start()
    {
        gm = GameObject.Find("Game Manager").GetComponent<GameManager>();
    }
    // Update is called once per frame
    void Update()
    {
        if (playerType == Touch.Red)
            Player1Movement();
        else if (playerType == Touch.Blue)
            Player2Movement();

        if (Mathf.Abs(transform.position.x) > 8.5 || Mathf.Abs(transform.position.y) > 9.5)
        {
            gm.OffBoard(playerType);
        }
    }

    void Player1Movement()
    {
        if (Input.GetKey(KeyCode.E))
        {
            rb.velocity /= 2;
            isCharging = true;
            slidePower += Time.deltaTime;
            if (slidePower >= maxSlidePower)
                Slide(slidePower);
        }
        else if (isCharging)
        {
            isCharging = false;
            if (slidePower != 0)
                Slide(slidePower);
            slidePower = 0;
        }
        else if (!isSliding)
        {
            Vector2 inputDir = Vector2.zero;

            if (Input.GetKey(KeyCode.W))
                inputDir.y += 1;
            if (Input.GetKey(KeyCode.S))
                inputDir.y += -1;
            if (Input.GetKey(KeyCode.A))
                inputDir.x += -1;
            if (Input.GetKey(KeyCode.D))
                inputDir.x += 1;

            if (inputDir != Vector2.zero)
            {
                dir = inputDir;
            }

            if (!(Mathf.Abs(rb.velocity.x) > maxSpeed) || !(Mathf.Abs(rb.velocity.y) > maxSpeed))
                rb.AddForce(inputDir.normalized * acceleration);
        }
        else if (Mathf.Abs(rb.velocity.x) < 5 && Mathf.Abs(rb.velocity.y) < 5)
        {
            isSliding = false;
        }
    }

    void Player2Movement()
    {
        if (Input.GetKey(KeyCode.RightShift))
        {
            rb.velocity /= 2;
            isCharging = true;
            slidePower += Time.deltaTime;
            if (slidePower >= maxSlidePower)
                Slide(slidePower);
        }
        else if (isCharging)
        {
            isCharging = false;
            if (slidePower != 0)
                Slide(slidePower);
            slidePower = 0;
        }
        else if (!isSliding)
        {
            Vector2 inputDir = Vector2.zero;

            if (Input.GetKey(KeyCode.UpArrow))
                inputDir.y += 1;
            if (Input.GetKey(KeyCode.DownArrow))
                inputDir.y += -1;
            if (Input.GetKey(KeyCode.LeftArrow))
                inputDir.x += -1;
            if (Input.GetKey(KeyCode.RightArrow))
                inputDir.x += 1;

            if (inputDir != Vector2.zero)
            {
                dir = inputDir;
            }

            if (!(Mathf.Abs(rb.velocity.x) > maxSpeed) || !(Mathf.Abs(rb.velocity.y) > maxSpeed))
                rb.AddForce(inputDir.normalized * acceleration);
        }
        else if (Mathf.Abs(rb.velocity.x) < 5 && Mathf.Abs(rb.velocity.y) < 5)
        {
            isSliding = false;
        }
    }

    void Slide(float power)
    {
        Debug.Log($"Slid {power} leads to {dir * power * slideSpeed}");
        rb.velocity = Vector2.zero;
        rb.AddForce(dir * power * slideSpeed, ForceMode2D.Impulse);
        isSliding = true;
    }
}


class Red : IBalloonHitter
{
    public Player player;
    public Touch Team => Touch.Red;
    public bool IsSliding => player.isSliding;

    public Red(Player p)
    {
        player = p;
    }
}

class Blue : IBalloonHitter
{
    public Player player;
    public Touch Team => Touch.Blue;
    public bool IsSliding => player.isSliding;

    public Blue(Player p)
    {
        player = p;
    }
}