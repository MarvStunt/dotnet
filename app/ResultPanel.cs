using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Result Panel - Displays final leaderboard for all players
/// </summary>
public partial class ResultPanel : Panel
{
	private Label titleLabel;
	private Label messageLabel;
	private Button backToHubButton;
	private VBoxContainer leaderboardVBox;
	private bool isMaster = false;

	public override void _Ready()
	{
		// Get child nodes
		titleLabel = GetNode<Label>("ResultMargin/ResultVBox/TitleLabel");
		messageLabel = GetNode<Label>("ResultMargin/ResultVBox/MessageLabel");
		leaderboardVBox = GetNode<VBoxContainer>("ResultMargin/ResultVBox/LeaderboardContainer/LeaderboardMargin/LeaderboardVBox");
		backToHubButton = GetNode<Button>("ResultMargin/ResultVBox/Buttons/BackToHubButton");

		// Connect button signals
		backToHubButton.Pressed += OnBackToHubPressed;

		// Display the leaderboard if data is available
		if (!string.IsNullOrEmpty(ResultPanelData.LeaderboardJson))
		{
			GD.Print($"ResultPanel: Displaying leaderboard: {ResultPanelData.LeaderboardJson}");
			var json = new Json();
			json.Parse(ResultPanelData.LeaderboardJson);
			var leaderboard = (Godot.Collections.Array)json.Data;
			ShowLeaderboard(leaderboard);
		}
		else
		{
			GD.Print("ResultPanel: No leaderboard data - returning to Hub");
			// No data available, return to Hub instead of displaying error
			GetTree().ChangeSceneToFile("res://Hub.tscn");
		}
	}

	/// <summary>
	/// Set if the player is master (affects button availability)
	/// </summary>
	public void SetIsMaster(bool master)
	{
		isMaster = master;
	}

	/// <summary>
	/// Show final leaderboard for everyone (both master and players)
	/// </summary>
	public void ShowLeaderboard(Godot.Collections.Array leaderboard)
	{
		titleLabel.Text = "🏁 GAME FINISHED!";
		
		// Clear previous leaderboard entries
		foreach (Node child in leaderboardVBox.GetChildren())
		{
			child.QueueFree();
		}

		// Get current player name if available (for highlighting)
		string currentPlayerName = null;
		var root = GetTree().Root;
		if (root.HasNode("NetworkManager"))
		{
			var networkManager = root.GetNode<NetworkManager>("NetworkManager");
			currentPlayerName = networkManager.PlayerName;
		}

		// Filter leaderboard to exclude master players, then display
		int position = 1;
		foreach (var entry in leaderboard)
		{
			var playerData = (Godot.Collections.Dictionary)entry;
			string playerName = playerData["playerName"].AsString();
			int score = (int)playerData["score"].AsInt64();
			string role = playerData.ContainsKey("role") ? playerData["role"].AsString() : "player";

			// Skip master players in the leaderboard
			if (role == "master")
			{
				GD.Print($"Skipping master player: {playerName}");
				continue;
			}

			// Create a label for each player
			Label playerLabel = new Label();
			string medalEmoji = position switch
			{
				1 => "🥇",
				2 => "🥈",
				3 => "🥉",
				_ => $"{position}."
			};

			playerLabel.Text = $"{medalEmoji} {playerName}: {score} pts";
			
			// Highlight current player
			if (playerName == currentPlayerName)
			{
				playerLabel.AddThemeColorOverride("font_color", new Color(0.2f, 1, 0.4f, 1)); // Green for current player
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

	/// <summary>
	/// Get color based on leaderboard position
	/// </summary>
	private Color GetPlayerColor(int position)
	{
		return position switch
		{
			1 => new Color(1, 0.84f, 0, 1), // Gold
			2 => new Color(0.75f, 0.75f, 0.75f, 1), // Silver
			3 => new Color(0.8f, 0.5f, 0.2f, 1), // Bronze
			_ => new Color(0.7f, 0.7f, 0.7f, 1) // White
		};
	}

	private void OnBackToHubPressed()
	{
		GD.Print("Back to hub pressed");
		// Return to Hub scene
		GetTree().ChangeSceneToFile("res://Hub.tscn");
	}
}
