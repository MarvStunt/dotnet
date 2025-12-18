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
            NetworkManager.SignalName.ShowPattern,
            Callable.From<int[], int>(OnShowPattern)
        );

        networkManager.Connect(
            NetworkManager.SignalName.PlayerSubmitted,
            Callable.From<string, bool, int, int>(OnPlayerSubmitted)
        );

        networkManager.Connect(
            NetworkManager.SignalName.GameEnded,
            Callable.From<string>(OnGameEnded)
        );

        GD.Print("Network signals connected");
    }

    /// <summary>
    /// Called when pattern is received from server (for players)
    /// </summary>
    private void OnShowPattern(int[] pattern, int roundNumber)
    {
        GD.Print($"Integration: Pattern received - Round {roundNumber}: {string.Join(",", pattern)}");
        gameLogic.ReceiveSequence(pattern);
    }

    /// <summary>
    /// Called when server sends player submission result
    /// </summary>
    private void OnPlayerSubmitted(string playerName, bool isCorrect, int pointsEarned, int totalScore)
    {
        GD.Print($"Integration: {playerName} submitted - {isCorrect} (+{pointsEarned}pts, total: {totalScore})");
        gameLogic.OnPlayerSubmitted(playerName, isCorrect, pointsEarned, totalScore);
    }

    /// <summary>
    /// Called when game ends
    /// </summary>
    private void OnGameEnded(string leaderboardJson)
    {
        GD.Print($"Integration: Game ended - Leaderboard: {leaderboardJson}");
        gameLogic.OnGameEnded(leaderboardJson);
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
