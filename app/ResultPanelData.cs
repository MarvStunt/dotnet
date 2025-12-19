using Godot;
using System;

public partial class ResultPanelData : Node
{
	public static ResultPanelData Instance { get; private set; }

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

	public void Clear()
	{
		LeaderboardJson = "";
	}
}
