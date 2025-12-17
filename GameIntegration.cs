using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Game Integration Manager - Wires everything together
/// This script should be attached to the Game scene root
/// </summary>
public partial class GameIntegration : Node2D
{
	private Game gameLogic;
	private ResultPanel resultPanel;
	private NetworkManager networkManager;
	private Label statusLabel;

	public override void _Ready()
	{
		// Get references
		gameLogic = GetNode<Game>(".");
		
		try
		{
			resultPanel = GetNode<ResultPanel>("ResultPanel");
		}
		catch
		{
			// If ResultPanel script is not attached, get as Panel
			var panelNode = GetNode<Panel>("ResultPanel");
			if (panelNode is ResultPanel rp)
				resultPanel = rp;
			else
				GD.PrintErr("ResultPanel not found or script not attached");
		}

		networkManager = GetTree().Root.HasNode("NetworkManager")
			? GetTree().Root.GetNode<NetworkManager>("NetworkManager")
			: null;

		try
		{
			statusLabel = GetNode<Label>("StatusLabel");
		}
		catch
		{
			// Optional status label
		}

		// Connect network signals if in network mode
		if (networkManager != null && networkManager.IsConnected)
		{
			ConnectNetworkSignals();
		}

		GD.Print("Game Integration initialized");
	}

	private void ConnectNetworkSignals()
	{
		// Use weak reference to avoid memory leaks
		networkManager.Connect(
			NetworkManager.SignalName.SequenceReceived,
			Callable.From<int[]>(OnSequenceReceived)
		);

		networkManager.Connect(
			NetworkManager.SignalName.ValidationResult,
			Callable.From<bool, string>(OnValidationResult)
		);

		networkManager.Connect(
			NetworkManager.SignalName.GameEnded,
			Callable.From<bool, string>(OnGameEnded)
		);

		GD.Print("Network signals connected");
	}

	/// <summary>
	/// Called when sequence is received from server (for players)
	/// </summary>
	private void OnSequenceReceived(int[] sequence)
	{
		GD.Print($"Integration: Sequence received - {string.Join(",", sequence)}");
		gameLogic.ReceiveSequence(sequence);
	}

	/// <summary>
	/// Called when server validates player answer
	/// </summary>
	private void OnValidationResult(bool isCorrect, string message)
	{
		GD.Print($"Integration: Validation result - {isCorrect} - {message}");
		gameLogic.OnValidationResult(isCorrect, message);

		if (!isCorrect && resultPanel != null)
		{
			resultPanel.ShowLost(message);
		}
	}

	/// <summary>
	/// Called when game ends
	/// </summary>
	private void OnGameEnded(bool won, string reason)
	{
		GD.Print($"Integration: Game ended - Won: {won}, Reason: {reason}");
		gameLogic.OnGameEnded(won, reason);

		// Show result panel
		if (resultPanel != null)
		{
			if (won)
				resultPanel.ShowWon(reason);
			else
				resultPanel.ShowLost(reason);
		}
	}

	/// <summary>
	/// Helper method to get game mode info
	/// </summary>
	public void PrintGameInfo()
	{
		if (networkManager == null)
		{
			GD.Print("=== GAME INFO ===");
			GD.Print("Mode: LOCAL");
			return;
		}

		GD.Print("=== GAME INFO ===");
		GD.Print($"Mode: NETWORK");
		GD.Print($"Connected: {networkManager.IsConnected}");
		GD.Print($"GameID: {networkManager.GameId}");
		GD.Print($"PlayerID: {networkManager.PlayerId}");
		GD.Print($"Role: {networkManager.PlayerRole}");
		GD.Print($"==================");
	}

	// Call this from debugger: PrintGameInfo()
	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventKey keyEvent)
		{
			if (keyEvent.Pressed && keyEvent.Keycode == Key.F9)
			{
				PrintGameInfo();
			}
		}
	}
}
