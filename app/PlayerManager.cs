using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages the player list UI and player data for the Game scene.
/// Handles adding, removing, updating players and their visual representation.
/// </summary>
public class PlayerManager
{
	private readonly VBoxContainer _playersList;
	private readonly List<Player> _players = new();

	/// <summary>
	/// Gets the list of all players
	/// </summary>
	public IReadOnlyList<Player> Players => _players;

	/// <summary>
	/// Gets the count of connected players
	/// </summary>
	public int PlayerCount => _players.Count;

	/// <summary>
	/// Initialize the PlayerManager with the player list container
	/// </summary>
	public PlayerManager(VBoxContainer playersList)
	{
		_playersList = playersList;
	}

	#region Player List Operations
	/// <summary>
	/// Clear all players from the list and UI
	/// </summary>
	public void ClearAll()
	{
		if (_playersList == null)
			return;

		foreach (Node child in _playersList.GetChildren())
		{
			_playersList.RemoveChild(child);
			child.QueueFree();
		}
		_players.Clear();
	}

	/// <summary>
	/// Add a player to the list with a specified role
	/// </summary>
	public bool AddPlayer(string playerName, string role = null)
	{
		role ??= Roles.Player;
		
		if (_playersList == null)
			return false;

		// Don't add duplicate if already in list
		if (_players.Any(pl => pl.MatchesName(playerName)))
		{
			return false;
		}

		// Remove empty label if it exists
		RemoveEmptyLabel();

		// Create a label and Player model for the player
		Label playerLabel = new Label();
		Player player = new Player(playerName, role);
		player.ApplyLabelStyle(playerLabel);
		_playersList.AddChild(playerLabel);
		_players.Add(player);

		return true;
	}

	/// <summary>
	/// Remove a player from the list
	/// </summary>
	public bool RemovePlayer(string playerName)
	{
		Player player = _players.FirstOrDefault(p => p.MatchesName(playerName));
		if (player == null)
			return false;

		if (player.Label != null)
		{
			_playersList?.RemoveChild(player.Label);
			player.Label.QueueFree();
		}

		_players.Remove(player);
		return true;
	}

	/// <summary>
	/// Find a player by name
	/// </summary>
	public Player FindPlayer(string playerName)
	{
		return _players.FirstOrDefault(p => p.MatchesName(playerName));
	}

	/// <summary>
	/// Check if a player exists in the list
	/// </summary>
	public bool HasPlayer(string playerName)
	{
		return _players.Any(p => p.MatchesName(playerName));
	}
	#endregion

	#region Feedback Operations
	/// <summary>
	/// Set feedback (correct/incorrect) for a specific player
	/// </summary>
	public void SetPlayerFeedback(string playerName, bool isCorrect)
	{
		Player player = FindPlayer(playerName);
		if (player != null)
		{
			player.SetFeedback(isCorrect);
		}
		else
		{
		}
	}

	/// <summary>
	/// Clear all player feedback (reset to normal display)
	/// </summary>
	public void ClearAllFeedback()
	{
		foreach (var player in _players)
		{
			player.SetFeedback(null);
		}
	}
	#endregion

	#region Bulk Operations
	/// <summary>
	/// Update the player list from a JSON array received from the server
	/// </summary>
	public void UpdateFromJson(string playerListJson)
	{
		try
		{
			var json = new Json();
			json.Parse(playerListJson);
			var playerList = (Godot.Collections.Array)json.Data;

			// Clear and rebuild the list
			ClearAll();

			foreach (var entry in playerList)
			{
				var playerData = (Godot.Collections.Dictionary)entry;
				string playerName = playerData["playerName"].AsString();
				string role = playerData.ContainsKey("role") ? playerData["role"].AsString() : Roles.Player;
				AddPlayer(playerName, role);
			}

		}
		catch (Exception ex)
		{
			GD.PrintErr($"Error updating player list from JSON: {ex.Message}");
		}
	}

	/// <summary>
	/// Get all player names
	/// </summary>
	public List<string> GetAllPlayerNames()
	{
		return _players.Select(p => p.Name).ToList();
	}

	/// <summary>
	/// Get count of players excluding the master
	/// </summary>
	public int GetNonMasterPlayerCount()
	{
		return _players.Count(p => !p.IsMaster);
	}
	#endregion

	#region Private Helpers
	/// <summary>
	/// Remove the "No players" empty label if it exists
	/// </summary>
	private void RemoveEmptyLabel()
	{
		if (_playersList == null)
			return;

		if (_playersList.GetChildCount() == 1 && 
			_playersList.GetChild(0) is Label emptyLabel && 
			emptyLabel.Text.Contains("No players"))
		{
			_playersList.RemoveChild(emptyLabel);
			emptyLabel.QueueFree();
		}
	}
	#endregion
}
