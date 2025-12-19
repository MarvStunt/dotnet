using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class Game : Control
{
	// Game state
	private List<int> sequence = new();
	private List<int> playerInput = new();
	private int playerResponseCount = 0;

	// Dependencies
	private GameUI ui;
	private PlayerManager playerManager;
	private NetworkManager networkManager;
	private GameStateManager stateManager;

	// Shortcut properties for cleaner code
	private bool IsMaster => stateManager?.IsMaster ?? false;
	private bool CanInput => stateManager?.CanPlayerInput ?? false;
	private int RoundNumber => stateManager?.RoundNumber ?? 0;

	public override void _Ready()
	{
		// Initialize UI manager
		ui = new GameUI(this);
		
		// Initialize player manager
		playerManager = new PlayerManager(ui.PlayersList);

		// Initialize state manager (create local instance if not autoloaded)
		stateManager = new GameStateManager();
		AddChild(stateManager);

		// Get network manager
		if (GetTree().Root.HasNode("NetworkManager"))
		{
			networkManager = GetTree().Root.GetNode<NetworkManager>("NetworkManager");
			stateManager.SetRole(networkManager.PlayerRole);
			ConnectNetworkSignals();
		}

		// Connect button signals
		ConnectButtonSignals();
		ConnectColorButtons();
		
		ui.DisableAllGameButtons();
		SetupNetworkGame();
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
		
		// Request the full player list from the server
		if (networkManager != null)
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
		networkManager.PlayerListReceived += OnPlayerListReceived;

	}

	// Handler for round changed (master builds sequence, player waits)
	private void OnRoundChanged(int newRoundNumber)
	{

		stateManager.SetRound(newRoundNumber);
		
		ui.SetRoundInfoText(RoundNumber);

		// Clear feedback from previous round
		playerManager.ClearAllFeedback();

		if (IsMaster)
		{
			// Master prepares to build sequence for this round
			sequence.Clear();
			playerInput.Clear();
			playerResponseCount = 0;
			stateManager.ChangeState(GameState.BuildingSequence);
			ui.SetInfoText($"Round {newRoundNumber}\nClick buttons to build sequence 🎯");

			ui.EnableGameButtons();
		}
		else
		{
			// Player waits for pattern
			stateManager.ChangeState(GameState.Waiting);
			ui.SetInfoText($"Round {RoundNumber}\nWaiting for master...");
		}
	}

	// Handler for when pattern is received from server (players only)
	private void OnShowPattern(int[] pattern, int roundNumber)
	{
		ReceiveSequence(pattern);
	}

	// Handler for player submission result from server
	public void OnPlayerSubmitted(string playerNameReceived, bool isCorrect, int pointsEarned, int totalScore)
	{
		// Get current player name from NetworkManager
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
			// Disable player input until next turn
			stateManager.ChangeState(GameState.RoundComplete);
			DisablePlayerButtons();
		}

		// Update player list with feedback using PlayerManager
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

	// Handler for game ended signal from server
	public void OnGameEnded(string leaderboardJson)
	{

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

		// Find current player's score
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

		// Store leaderboard data for ResultPanel
		// Create instance if not exists (fallback if Autoload not configured)
		if (ResultPanelData.Instance == null)
		{
			var resultData = new ResultPanelData();
			GetTree().Root.AddChild(resultData);
		}
		ResultPanelData.Instance.LeaderboardJson = leaderboardJson;

		// Load ResultPanel scene with deferred call
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

	// Show sequence
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
			// Le serveur envoie automatiquement le pattern via StartRound
			await Task.Delay(1000);
			ui.SetInfoText("Waiting for players...");
		}
		else
		{
			ui.SetInfoText("Your turn 🎯");
			stateManager.ChangeState(GameState.PlayerTurn);
		}
	}

	// Note: Le serveur génère maintenant automatiquement la séquence
	// Le master démarre simplement le round avec StartGame() ou NextRound()

	// Flash a button
	private async Task FlashButton(int index)
	{
		ColorButton btn = ui.GetColorButton(index);
		if (btn == null)
			return;

		// Enable button
		btn.Disabled = false;
		await ToSignal(GetTree().CreateTimer(0.5f), "timeout");

		// Disable button
		btn.Disabled = true;
		await ToSignal(GetTree().CreateTimer(0.2f), "timeout");
	}

	// Flash only player buttons with the correct index
	private async Task FlashPlayerButton(int index)
	{
		ColorButton btn = ui.GetColorButton(index);
		if (btn == null)
			return;

		float flashTime = 0.5f;
		// Enable button
		btn.Disabled = false;
		await ToSignal(GetTree().CreateTimer(flashTime), "timeout");

		// Disable button
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

		// TODO: Calculer le vrai temps de réaction si nécessaire
		long reactionTimeMs = 1000; // Temps par défaut
		networkManager.SubmitAttempt(answer, reactionTimeMs);
	}

	// Public method to receive sequence from server (for players)
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

		// Enable buttons for player to respond
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

	public void OnRetryPressed()
	{
		// Return to Game scene to play again
		GetTree().ChangeSceneToFile("res://Game.tscn");
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

		// TODO: Calculer le vrai temps de réaction si nécessaire
		long reactionTimeMs = 2000; // Temps par défaut
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

		// The server will send the complete player list with roles via PlayerListReceived
		// so we don't need to manually add players here - just update the UI

		ui.SetInfoText($"✅ {playerName} joined!");

		// Enable start button when at least 1 other player connected (besides master)
		if (IsMaster)
		{
			ui.SetStartGameDisabled(false); // Enable if we have at least 1 player
		}
	}

	public void OnPlayerListReceived(string playerListJson)
	{

		// Update player list using PlayerManager
		playerManager.UpdateFromJson(playerListJson);
		
		// Update UI player count
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
				// Temporary feedback
				string originalText = ui.CopyGameIdButton.Text;
				ui.CopyGameIdButton.Text = "✓ Copied!";
				await ToSignal(GetTree().CreateTimer(2.0f), "timeout");
				ui.CopyGameIdButton.Text = originalText;
			}
		}
		else
		{

		}
	}
}
