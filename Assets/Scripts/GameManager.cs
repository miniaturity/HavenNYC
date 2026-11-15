using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public const int WinScore = 5;

    [SerializeField] private float roundEndDelay = 1.5f;
    [SerializeField] private float matchEndDelay = 3f;

    public int redScore;
    public int blueScore;

    public static GameManager Instance { get; private set; }
    public Team Server { get; private set; } = Team.Red;
    public bool RoundOver { get; private set; }

    private Balloon balloon;
    private string message = "";
    private bool matchOver;
    private GUIStyle style;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance == null)
            new GameObject("GameManager").AddComponent<GameManager>();
    }

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

    public void RegisterBalloon(Balloon b)
    {
        balloon = b;
        RoundOver = false;
        message = "";
    }

    public void PointScored(Team winner, string reason)
    {
        if (RoundOver || winner == Team.None) return;
        RoundOver = true;

        if (winner == Team.Red) redScore++;
        else blueScore++;

        Server = winner.Opponent();
        matchOver = redScore >= WinScore || blueScore >= WinScore;

        message = matchOver
            ? $"{Label(winner)} WINS THE MATCH!"
            : $"{Label(winner)} scores ({Label(winner.Opponent()).ToLower()} {reason})";

        Invoke(nameof(NextRound), matchOver ? matchEndDelay : roundEndDelay);
    }

    public void OffBoard(Team losingPlayer)
    {
        if (losingPlayer == Team.None) return;
        PointScored(losingPlayer.Opponent(), "fell in the water");
    }

    private void NextRound()
    {
        if (matchOver)
        {
            redScore = 0;
            blueScore = 0;
            Server = Team.Red;
            matchOver = false;
        }
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private static string Label(Team t) => t.ToString().ToUpper();

    private void OnGUI()
    {
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
        }

        float cx = Screen.width * 0.5f;

        GUI.color = Team.Red.GetColor();
        GUI.Label(new Rect(cx - 230, 10, 200, 50), $"RED {redScore}", style);
        GUI.color = Color.white;
        GUI.Label(new Rect(cx - 20, 10, 40, 50), "-", style);
        GUI.color = Team.Blue.GetColor();
        GUI.Label(new Rect(cx + 30, 10, 200, 50), $"BLUE {blueScore}", style);

        if (!RoundOver && balloon != null)
        {
            GUI.color = balloon.NextHitter.GetColor();
            GUI.Label(new Rect(cx - 200, 60, 400, 40), $"{Label(balloon.NextHitter)}'S HIT", style);
        }

        if (!string.IsNullOrEmpty(message))
        {
            GUI.color = Color.white;
            GUI.Label(new Rect(cx - 300, 100, 600, 40), message, style);
        }
    }
}