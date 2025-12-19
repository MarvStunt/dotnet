using Godot;
using System;

public class Player
{
    public string Name { get; set; }
    public string Role { get; set; } = Roles.Player;
    public int Score { get; set; } = 0;
    public Label Label { get; set; }

    public Player(string name, string role = null, int score = 0)
    {
        Name = name;
        Role = role ?? Roles.Player;
        Score = score;
    }

    public bool IsMaster => Role == Roles.Master;
    public bool IsPlayer => Role == Roles.Player;

    public string DisplayName()
    {
        return IsMaster ? $"👑 {Name}" : $"🎮 {Name}";
    }

    public Color TextColor()
    {
        return IsMaster ? new Color(1, 0.84f, 0, 1) : new Color(0.2f, 1, 0.4f, 1);
    }

    public void ApplyLabelStyle(Label label)
    {
        Label = label;
        if (Label == null)
            return;
        Label.Text = DisplayName();
        Label.AddThemeColorOverride("font_color", TextColor());
        Label.AddThemeFontSizeOverride("font_size", 14);
        Label.HorizontalAlignment = HorizontalAlignment.Left;
    }

    public void SetFeedback(bool? correct)
    {
        if (Label == null)
            return;

        if (correct == null)
        {
            Label.Text = DisplayName();
            Label.AddThemeColorOverride("font_color", TextColor());
            return;
        }

        if (correct == true)
        {
            Label.Text = $"{DisplayName()} ✅";
            Label.AddThemeColorOverride("font_color", new Color(0.2f, 1, 0.4f, 1));
        }
        else
        {
            Label.Text = $"{DisplayName()} ❌";
            Label.AddThemeColorOverride("font_color", new Color(1, 0.3f, 0.3f, 1));
        }
    }

    public bool MatchesName(string playerName)
    {
        return Name == playerName;
    }

    public bool Equals(Player other)
    {
        if (other == null) return false;
        return Name == other.Name;
    }
}
