using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public Balloon balloon;
    public static GameManager Instance { get; private set; }
    public int Score { get; private set; }


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void OnBalloonHitFloor(Touch lastPlayerHit)
    {
        
    }
}
