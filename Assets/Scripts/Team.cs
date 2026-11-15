using UnityEngine;

public enum Team
{
    Red,
    Blue,
    None
}

public interface IBalloonHitter
{
    Team Team { get; }
    bool IsSliding { get; }
    Vector2 Velocity { get; }
    Vector2 Position { get; }
}

public static class TeamExtensions
{
    public static readonly Color RedColor = new Color32(0xFF, 0x35, 0x35, 0xFF);   // #ff3535
    public static readonly Color BlueColor = new Color32(0x5B, 0x6E, 0xE1, 0xFF);  // #5b6ee1

    public static Team Opponent(this Team t)
    {
        if (t == Team.Red) return Team.Blue;
        if (t == Team.Blue) return Team.Red;
        return Team.None;
    }

    public static Color GetColor(this Team t)
    {
        if (t == Team.Red) return RedColor;
        if (t == Team.Blue) return BlueColor;
        return Color.white;
    }
}