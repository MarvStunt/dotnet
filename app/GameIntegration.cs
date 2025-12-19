using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class GameIntegration : Node2D
{
    private Game gameLogic;
    private ResultPanel resultPanel;
    private NetworkManager networkManager;
    private Label statusLabel;

    public override void _Ready()
    {
        gameLogic = GetNode<Game>(".");

        try
        {
            resultPanel = GetNode<ResultPanel>("ResultPanel");
        }
        catch
        {
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
        }

        if (networkManager != null && networkManager.IsConnected)
        {
            ConnectNetworkSignals();
        }

    }

    private void ConnectNetworkSignals()
    {
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

    }

    private void OnShowPattern(int[] pattern, int roundNumber)
    {
        gameLogic.ReceiveSequence(pattern);
    }

    private void OnPlayerSubmitted(string playerName, bool isCorrect, int pointsEarned, int totalScore)
    {
        gameLogic.OnPlayerSubmitted(playerName, isCorrect, pointsEarned, totalScore);
    }

    private void OnGameEnded(string leaderboardJson)
    {
        gameLogic.OnGameEnded(leaderboardJson);
    }

    public void PrintGameInfo()
    {
        if (networkManager == null)
        {
            return;
        }

    }

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
