using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public class PlayerManager
{
	private readonly VBoxContainer _playersList;
	private readonly List<Player> _players = new();

	public IReadOnlyList<Player> Players => _players;

	public int PlayerCount => _players.Count;

	public PlayerManager(VBoxContainer playersList)
	{
		_playersList = playersList;
	}

	#region Player List Operations
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

	public bool AddPlayer(string playerName, string role = null)
	{
		role ??= Roles.Player;
		
		if (_playersList == null)
			return false;

		if (_players.Any(pl => pl.MatchesName(playerName)))
		{
			return false;
		}

		RemoveEmptyLabel();

		Label playerLabel = new Label();
		Player player = new Player(playerName, role);
		player.ApplyLabelStyle(playerLabel);
		_playersList.AddChild(playerLabel);
		_players.Add(player);

		return true;
	}

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

	public Player FindPlayer(string playerName)
	{
		return _players.FirstOrDefault(p => p.MatchesName(playerName));
	}

	public bool HasPlayer(string playerName)
	{
		return _players.Any(p => p.MatchesName(playerName));
	}

	public void MarkPlayerDisconnected(string playerName)
	{
		Player player = FindPlayer(playerName);
		if (player != null)
		{
			player.IsConnected = false;
			// Update the label to show disconnection icon
			player.ApplyLabelStyle(player.Label);
		}
	}

	public void MarkPlayerReconnected(string playerName)
	{
		Player player = FindPlayer(playerName);
		if (player != null)
		{
			player.IsConnected = true;
			player.ApplyLabelStyle(player.Label);
		}
	}
	#endregion

	#region Feedback Operations
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

	public void ClearAllFeedback()
	{
		foreach (var player in _players)
		{
			player.SetFeedback(null);
		}
	}
	#endregion

	#region Bulk Operations
	public void UpdateFromJson(string playerListJson)
	{
		try
		{
			var json = new Json();
			json.Parse(playerListJson);
			var playerList = (Godot.Collections.Array)json.Data;

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

	public List<string> GetAllPlayerNames()
	{
		return _players.Select(p => p.Name).ToList();
	}

	public int GetNonMasterPlayerCount()
	{
		return _players.Count(p => !p.IsMaster && p.IsConnected);
	}
	#endregion

	#region Private Helpers
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
