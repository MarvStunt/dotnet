using Godot;
using System;
using System.Collections.Generic;

public partial class ResultPanel : Panel
{
	private Label titleLabel;
	private Label messageLabel;
	private Button backToHubButton;
	private VBoxContainer leaderboardVBox;
	private bool isMaster = false;

	public override void _Ready()
	{
		titleLabel = GetNode<Label>("ResultMargin/ResultVBox/TitleLabel");
		messageLabel = GetNode<Label>("ResultMargin/ResultVBox/MessageLabel");
		leaderboardVBox = GetNode<VBoxContainer>("ResultMargin/ResultVBox/LeaderboardContainer/LeaderboardMargin/LeaderboardVBox");
		backToHubButton = GetNode<Button>("ResultMargin/ResultVBox/Buttons/BackToHubButton");

		backToHubButton.Pressed += OnBackToHubPressed;

		if (ResultPanelData.Instance != null && !string.IsNullOrEmpty(ResultPanelData.Instance.LeaderboardJson))
		{
			var json = new Json();
			json.Parse(ResultPanelData.Instance.LeaderboardJson);
			var leaderboard = (Godot.Collections.Array)json.Data;
			ShowLeaderboard(leaderboard);
			
			ResultPanelData.Instance.Clear();
		}
		else
		{
			GetTree().CallDeferred("change_scene_to_file", "res://Hub.tscn");
		}
	}

	public void SetIsMaster(bool master)
	{
		isMaster = master;
	}

	public void ShowLeaderboard(Godot.Collections.Array leaderboard)
	{
		titleLabel.Text = "🏁 GAME FINISHED!";
		
		foreach (Node child in leaderboardVBox.GetChildren())
		{
			child.QueueFree();
		}

		string currentPlayerName = null;
		var root = GetTree().Root;
		if (root.HasNode("NetworkManager"))
		{
			var networkManager = root.GetNode<NetworkManager>("NetworkManager");
			currentPlayerName = networkManager.PlayerName;
		}

		int position = 1;
		foreach (var entry in leaderboard)
		{
			var playerData = (Godot.Collections.Dictionary)entry;
			string playerName = playerData["playerName"].AsString();
			int score = (int)playerData["score"].AsInt64();
			string role = playerData.ContainsKey("role") ? playerData["role"].AsString() : Roles.Player;

			if (Roles.IsMaster(role))
			{
				continue;
			}

			Label playerLabel = new Label();
			string medalEmoji = position switch
			{
				1 => "🥇",
				2 => "🥈",
				3 => "🥉",
				_ => $"{position}."
			};

			playerLabel.Text = $"{medalEmoji} {playerName}: {score} pts";
			
			if (playerName == currentPlayerName)
			{
				playerLabel.AddThemeColorOverride("font_color", new Color(0.2f, 1, 0.4f, 1));
			}
			else
			{
				playerLabel.AddThemeColorOverride("font_color", GetPlayerColor(position));
			}
			
			playerLabel.AddThemeFontSizeOverride("font_size", 20);
			playerLabel.HorizontalAlignment = HorizontalAlignment.Center;
			
			leaderboardVBox.AddChild(playerLabel);
			position++;
		}

		messageLabel.Text = "See who won! 🎉";
		Visible = true;
	}

	private Color GetPlayerColor(int position)
	{
		return position switch
		{
			1 => new Color(1, 0.84f, 0, 1),
			2 => new Color(0.75f, 0.75f, 0.75f, 1),
			3 => new Color(0.8f, 0.5f, 0.2f, 1),
			_ => new Color(0.7f, 0.7f, 0.7f, 1)
		};
	}

	private void OnBackToHubPressed()
	{
		GetTree().ChangeSceneToFile("res://Hub.tscn");
	}
}
