using Godot;
using System;

/// <summary>
/// Singleton to pass data between scenes (registered as Autoload in Godot)
/// Access via: ResultPanelData.Instance
/// </summary>
public partial class ResultPanelData : Node
{
	public static ResultPanelData Instance { get; private set; }

	/// <summary>
	/// JSON data for the leaderboard to display
	/// </summary>
	public string LeaderboardJson { get; set; } = "";

	public override void _Ready()
	{
		Instance = this;
	}

	public override void _ExitTree()
	{
		if (Instance == this)
			Instance = null;
	}

	/// <summary>
	/// Clear data after use to prevent stale data
	/// </summary>
	public void Clear()
	{
		LeaderboardJson = "";
	}
}
