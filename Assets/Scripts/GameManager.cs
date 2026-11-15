using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    // public Balloon balloon;
    public int blueScore;
    public int redScore;
    public TMP_Text scoreText;
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
    private void Start()
    {
        scoreText.text = $"Red: {redScore} VS Blue {blueScore}\r\n";
    }
    public void ResetGame()
    {
        if (redScore == 5 || blueScore == 5){
        }    //GameOver
        SceneManager.LoadScene(0); //CHANGE TO 1 LATER
    }

    public void OnBalloonHitFloor(Touch lastPlayerHit)
    {
        if (lastPlayerHit == Touch.Red)
            redScore++;
        else blueScore++;
        ResetGame();
    }

    public void OffBoard(Touch losingPlayer)
    {
        if (losingPlayer == Touch.Red)
            blueScore++;
        else redScore++;
        ResetGame();
    }
}
