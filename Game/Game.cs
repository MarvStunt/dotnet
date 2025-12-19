using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class Game : Control
{
	private List<int> sequence = new();
	private List<int> playerInput = new();
	private int playerResponseCount = 0;

	private GameUI ui;
	private PlayerManager playerManager;
	private NetworkManager networkManager;
	private GameStateManager stateManager;

	private bool IsMaster => stateManager?.IsMaster ?? false;
	private bool CanInput => stateManager?.CanPlayerInput ?? false;
	private int RoundNumber => stateManager?.RoundNumber ?? 0;

	public override void _Ready()
	{
		ui = new GameUI(this);
		
		playerManager = new PlayerManager(ui.PlayersList);

		stateManager = new GameStateManager();
		AddChild(stateManager);

		if (GetTree().Root.HasNode("NetworkManager"))
		{
			networkManager = GetTree().Root.GetNode<NetworkManager>("NetworkManager");
			stateManager.SetRole(networkManager.PlayerRole);
			ConnectNetworkSignals();
		}

		ConnectButtonSignals();
		ConnectColorButtons();
		
		ui.DisableAllGameButtons();
		SetupNetworkGame();
	}

	public override void _ExitTree()
	{
		if (networkManager != null)
		{
			networkManager.ShowPattern -= OnShowPattern;
			networkManager.RoundChanged -= OnRoundChanged;
			networkManager.PlayerSubmitted -= OnPlayerSubmitted;
			networkManager.GameEnded -= OnGameEnded;
			networkManager.PlayerJoined -= OnPlayerJoined;
			networkManager.PlayerDisconnected -= OnPlayerDisconnected;
			networkManager.PlayerReconnected -= OnPlayerReconnected;
			networkManager.GameMasterDisconnected -= OnGameMasterDisconnected;
			networkManager.PlayerListReceived -= OnPlayerListReceived;
		}
	}

	private void ConnectButtonSignals()
	{
		if (ui.DisconnectButton != null)
			ui.DisconnectButton.Pressed += OnDisconnectPressed;
		if (ui.SendSequenceButton != null)
			ui.SendSequenceButton.Pressed += OnSendSequence;
		if (ui.NextRoundButton != null)
			ui.NextRoundButton.Pressed += OnNextRound;
		if (ui.StartGameButton != null)
			ui.StartGameButton.Pressed += OnStartGamePressed;
		if (ui.EndGameButton != null)
			ui.EndGameButton.Pressed += OnEndGamePressed;
		if (ui.CopyGameIdButton != null)
			ui.CopyGameIdButton.Pressed += OnCopyGameIdPressed;
	}

	private void SetupNetworkGame()
	{
		string gameId = networkManager?.GameId ?? "";
		string playerName = networkManager?.PlayerName ?? "";
		
		ui.SetupForNetworkGame(IsMaster, gameId, playerName, playerManager.PlayerCount, RoundNumber);
		
		if (networkManager != null)
		{
			CallDeferred(nameof(RequestPlayerList));
		}
	}

	private void RequestPlayerList()
	{
		if (networkManager != null && networkManager.IsConnected)
		{
			networkManager.GetPlayerList();
		}
	}

	private void StartMasterGame()
	{
		sequence.Clear();
		AddColorToSequence();
	}

	private void ConnectColorButtons()
	{
		foreach (ColorButton btn in ui.ColorButtons)
		{
			btn.Pressed += () => OnButtonPressed(btn.ColorIndex);
		}
	}

	private void ConnectNetworkSignals()
	{
		if (networkManager == null)
		{

			return;
		}

		networkManager.ShowPattern += OnShowPattern;
		networkManager.RoundChanged += OnRoundChanged;
		networkManager.PlayerSubmitted += OnPlayerSubmitted;
		networkManager.GameEnded += OnGameEnded;
		networkManager.PlayerJoined += OnPlayerJoined;
		networkManager.PlayerDisconnected += OnPlayerDisconnected;
		networkManager.PlayerReconnected += OnPlayerReconnected;
		networkManager.GameMasterDisconnected += OnGameMasterDisconnected;
		networkManager.PlayerListReceived += OnPlayerListReceived;

	}

	private void OnRoundChanged(int newRoundNumber)
	{

		stateManager.SetRound(newRoundNumber);
		
		ui.SetRoundInfoText(RoundNumber);

		playerManager.ClearAllFeedback();

		if (IsMaster)
		{
			sequence.Clear();
			playerInput.Clear();
			playerResponseCount = 0;
			stateManager.ChangeState(GameState.BuildingSequence);
			ui.SetInfoText($"Round {newRoundNumber}\nClick buttons to build sequence 🎯");

			ui.EnableGameButtons();
		}
		else
		{
			stateManager.ChangeState(GameState.Waiting);
			ui.SetInfoText($"Round {RoundNumber}\nWaiting for master...");
		}
	}

	private void OnShowPattern(int[] pattern, int roundNumber)
	{
		ReceiveSequence(pattern);
	}

	public void OnPlayerSubmitted(string playerNameReceived, bool isCorrect, int pointsEarned, int totalScore)
	{
		if (!IsInsideTree() || IsQueuedForDeletion())
			return;

		string currentPlayerName = networkManager != null ? networkManager.PlayerName : "";
		
		if (currentPlayerName == playerNameReceived)
		{
			if (isCorrect)
			{
				ui.SetInfoText($"✅ Correct! +{pointsEarned}pts (Total: {totalScore})");
			}
			else
			{
				ui.SetInfoText($"❌ Wrong! (Total: {totalScore})");
			}
			stateManager.ChangeState(GameState.RoundComplete);
			DisablePlayerButtons();
		}

		playerManager.SetPlayerFeedback(playerNameReceived, isCorrect);

		if (IsMaster)
		{
			playerResponseCount++;
			if (playerResponseCount == playerManager.GetNonMasterPlayerCount())
			{
				ui.SetNextRoundDisabled(false);
				if (RoundNumber >= 3)
				{
					ui.SetEndGameDisabled(false);
				}
			}
		}
	}

	public void OnGameEnded(string leaderboardJson)
	{
		if (!IsInsideTree() || IsQueuedForDeletion())
			return;

		var json = new Json();
		json.Parse(leaderboardJson);
		var leaderboard = (Godot.Collections.Array)json.Data;

		int bestScore = 0;
		string winner = "";

		foreach (var entry in leaderboard)
		{
			var playerData = (Godot.Collections.Dictionary)entry;
			string playerNameStr = playerData["playerName"].AsString();
			int score = (int)playerData["score"].AsInt64();



			if (score > bestScore)
			{
				bestScore = score;
				winner = playerNameStr;
			}
		}

		ui.SetInfoText($"🏁 GAME FINISHED!\nWinner: {winner} ({bestScore}pts)");
		stateManager.EndGame();

		string currentPlayerName = networkManager != null ? networkManager.PlayerName : "";
		int playerScore = 0;
		foreach (var entry in leaderboard)
		{
			var playerData = (Godot.Collections.Dictionary)entry;
			if (playerData["playerName"].AsString() == currentPlayerName)
			{
				playerScore = (int)playerData["score"].AsInt64();
				break;
			}
		}

		if (ResultPanelData.Instance == null)
		{
			var resultData = new ResultPanelData();
			GetTree().Root.AddChild(resultData);
		}
		ResultPanelData.Instance.LeaderboardJson = leaderboardJson;

		GetTree().CallDeferred("change_scene_to_file", "res://ResultPanel.tscn");
	}

	private void AddColorToSequence()
	{
		if (IsMaster)
		{
			stateManager.ChangeState(GameState.BuildingSequence);
			ui.SetInfoText("Click buttons to build your sequence 🎯");
		}
	}

	private async Task PlaySequence()
	{
		stateManager.ChangeState(GameState.ShowingPattern);
		playerInput.Clear();
		ui.SetInfoText("Watch 👀");

		foreach (int index in sequence)
			await FlashButton(index);

		if (IsMaster)
		{
			ui.SetInfoText("Starting round...");
			await Task.Delay(1000);
			ui.SetInfoText("Waiting for players...");
		}
		else
		{
			ui.SetInfoText("Your turn 🎯");
			stateManager.ChangeState(GameState.PlayerTurn);
		}
	}


	private async Task FlashButton(int index)
	{
		ColorButton btn = ui.GetColorButton(index);
		if (btn == null)
			return;

		btn.Disabled = false;
		await ToSignal(GetTree().CreateTimer(0.5f), "timeout");

		btn.Disabled = true;
		await ToSignal(GetTree().CreateTimer(0.2f), "timeout");
	}

	private async Task FlashPlayerButton(int index)
	{
		ColorButton btn = ui.GetColorButton(index);
		if (btn == null)
			return;

		float flashTime = 0.5f;
		btn.Disabled = false;
		await ToSignal(GetTree().CreateTimer(flashTime), "timeout");

		btn.Disabled = true;
		await ToSignal(GetTree().CreateTimer(flashTime), "timeout");
	}

	private async void OnButtonPressed(int index)
	{
		if (!CanInput)
			return;

		if (IsMaster)
		{
			sequence.Add(index);

			ui.SetSendSequenceDisabled(false);
			ui.SetInfoText($"Sequence built: {string.Join(",", sequence)} 🎯\nClick 'Send' when ready!");
			return;
		}

		playerInput.Add(index);
		int i = playerInput.Count - 1;

		if (playerInput[i] != sequence[i])
		{
			ui.SetInfoText("❌ Wrong!");
			stateManager.ChangeState(GameState.Validating);
			DisablePlayerButtons();
			SendAnswerToServer(playerInput.ToArray());
			return;
		}

		if (playerInput.Count == sequence.Count)
		{
			SendAnswerToServer(playerInput.ToArray());
			ui.SetInfoText("Validating with server...");
			stateManager.ChangeState(GameState.Validating);
			DisablePlayerButtons();
		}
	}

	private void SendAnswerToServer(int[] answer)
	{
		if (networkManager == null)
			return;

		long reactionTimeMs = 1000;
		networkManager.SubmitAttempt(answer, reactionTimeMs);
	}

	public void ReceiveSequence(int[] seq)
	{
		if (IsMaster)
			return;

		sequence = seq.ToList();
		_ = PlayPlayerSequence();
	}

	private async Task PlayPlayerSequence()
	{
		stateManager.ChangeState(GameState.ShowingPattern);
		ui.SetInfoText("Watch 👀");

		foreach (int index in sequence)
			await FlashPlayerButton(index);

		ui.EnableGameButtons();
		ui.SetInfoText("Your turn 🎯");
		stateManager.ChangeState(GameState.PlayerTurn);
		playerInput.Clear();
	}
	public void OnDisconnectPressed()
	{
		if (networkManager != null)
		{
			networkManager.Disconnect();
		}
		GetTree().ChangeSceneToFile("res://Hub.tscn");
	}

	public void OnHubPressed()
	{
		if (networkManager != null)
		{
			networkManager.Disconnect();
		}
		GetTree().ChangeSceneToFile("res://Hub.tscn");
	}

	public void OnSendSequence()
	{
		if (!IsMaster || networkManager == null)
		{

			return;
		}

		networkManager.StartRound(sequence);
		ui.SetInfoText("Sequence sent!\nWaiting for players...");
		stateManager.ChangeState(GameState.Waiting);
		ui.DisableAllGameButtons();
	}

	public void OnNextRound()
	{
		if (!IsMaster || networkManager == null)
		{

			return;
		}

		networkManager.NextRound();
		ui.SetInfoText("Starting next round...\nWaiting for players...");
		ui.SetNextRoundDisabled(true);
		ui.SetEndGameDisabled(true);
	}

	public void OnSubmitAnswer()
	{
		if (IsMaster || networkManager == null)
		{

			return;
		}

		long reactionTimeMs = 2000;
		networkManager.SubmitAttempt(playerInput.ToArray(), reactionTimeMs);
		ui.SetInfoText("Answer sent!\nWaiting for validation...");
	}

	public void OnColorButtonPressed(int colorIndex)
	{
		OnButtonPressed(colorIndex);
	}

	private void DisablePlayerButtons()
	{
		ui.DisableAllColorButtons();
	}

	public void OnPlayerJoined(string playerName)
	{
		if (!IsInsideTree() || IsQueuedForDeletion())
			return;


		ui.SetInfoText($"✅ {playerName} joined!");

		if (IsMaster)
		{
			ui.SetStartGameDisabled(false);
		}
	}

	public void OnPlayerDisconnected(string playerName, string role)
	{
		if (!IsInsideTree() || IsQueuedForDeletion())
			return;
		
		playerManager.MarkPlayerDisconnected(playerName);
		string roleText = role == "master" ? "Game Master" : "Player";
		ui.SetInfoText($"🔌 {playerName} ({roleText}) disconnected");
		
		GD.Print($"Player disconnected: {playerName} (Role: {role})");
	}

	public void OnPlayerReconnected(string playerName, string role)
	{
		if (!IsInsideTree() || IsQueuedForDeletion())
			return;

		playerManager.MarkPlayerReconnected(playerName);
		
		string roleText = role == "master" ? "Game Master" : "Player";
		ui.SetInfoText($"🔄 {playerName} ({roleText}) reconnected!");
		
		GD.Print($"Player reconnected: {playerName} (Role: {role})");
	}

	public void OnGameMasterDisconnected(string masterName)
	{
		if (!IsInsideTree() || IsQueuedForDeletion())
			return;

		GD.Print($"⚠️ Game Master {masterName} disconnected - returning to hub");
		
		// Show message to user
		ui.SetInfoText($"⚠️ Game Master disconnected!\nReturning to lobby...");
		
		// Wait a moment for user to see the message, then return to hub
		GetTree().CreateTimer(2.0).Timeout += () =>
		{
			if (IsInsideTree() && !IsQueuedForDeletion())
			{
				// Disconnect from server
				if (networkManager != null)
				{
					networkManager.Disconnect();
				}
				
				// Change scene to hub
				GetTree().ChangeSceneToFile("res://Hub.tscn");
			}
		};
	}

	public void OnPlayerListReceived(string playerListJson)
	{
		if (!IsInsideTree() || IsQueuedForDeletion())
			return;

		playerManager.UpdateFromJson(playerListJson);
		
		ui.SetPlayersCountText(playerManager.PlayerCount);
	}

	public void OnStartGamePressed()
	{
		if (IsMaster && playerManager.PlayerCount > 0 && !stateManager.GameStarted)
		{
			stateManager.StartGame();
			ui.EnableGameButtons();
			StartMasterGame();
			networkManager.StartGame();
			ui.SetStartGameDisabled(true);
		}
		else if (playerManager.PlayerCount == 0)
		{
			ui.SetInfoText("Cannot start: No players connected");
		}
	}

	public void OnEndGamePressed()
	{
		if (IsMaster && networkManager != null)
		{
			ui.SetInfoText("🏁 Game ended by master!");
			networkManager.StopGame();
			stateManager.EndGame();
		}
		ui.DisableAllGameButtons();
	}

	public async void OnCopyGameIdPressed()
	{
		if (networkManager != null && !string.IsNullOrEmpty(networkManager.GameId))
		{
			DisplayServer.ClipboardSet(networkManager.GameId);

			if (ui.CopyGameIdButton != null)
			{
				string originalText = ui.CopyGameIdButton.Text;
				ui.CopyGameIdButton.Text = "✓ Copied!";
				await ToSignal(GetTree().CreateTimer(2.0f), "timeout");
				ui.CopyGameIdButton.Text = originalText;
			}
		}
	}
}
