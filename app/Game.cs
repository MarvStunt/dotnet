using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class Game : Control
{
	private List<int> sequence = new();
	private List<int> playerInput = new();
	private bool isPlayerTurn = false;
	private bool isMaster = false;
	private int connectedPlayers = 0;
	private bool gameStarted = false;
	private Godot.Collections.Array<Node> buttons;
	private Label labelInfo;
	private Label roleLabel;
	private Label gameIdLabel;
	private Label gameInfoLabel;
	private Label playersCountLabel;
	private Label roundInfoLabel;
	private Label sequenceLabel;
	private Label feedbackLabel;
	private Label playerName;
	private Label gameStatusLabel;
	private Button sendSequenceButton;
	private Button nextRoundButton;
	private Button disconnectButton;
	private Button startGameButton;
	private Button endGameButton;
	private Button copyGameIdButton;
	private NetworkManager networkManager;
	private Control masterSection;
	private Control playerSection;
	private VBoxContainer playersList;
	private Dictionary<string, Label> playersLabels = new(); // Key is playerName, not playerId

	private int playerResponseCount = 0;

	private int roundNumber = 0;

	public override void _Ready()
	{
		// Get UI elements from new layout
		masterSection = GetNode<Control>("MainVBox/Content/GamePanel/GameMargin/GameVBox/ControlsSection/MasterSection");
		playerSection = GetNode<Control>("MainVBox/Content/GamePanel/GameMargin/GameVBox/ControlsSection/PlayerSection");
		
		// Labels
		sequenceLabel = GetNode<Label>("MainVBox/Content/GamePanel/GameMargin/GameVBox/ControlsSection/SequenceLabel");
		feedbackLabel = GetNode<Label>("MainVBox/Content/GamePanel/GameMargin/GameVBox/ControlsSection/FeedbackLabel");
		labelInfo = GetNode<Label>("MainVBox/Content/GamePanel/GameMargin/GameVBox/GameStatus");
		
		// Header labels
		roleLabel = GetNode<Label>("MainVBox/Header/StatusSection/RoleLabel");
		gameInfoLabel = GetNode<Label>("MainVBox/Header/TitleBox/GameInfo");
		playersCountLabel = GetNode<Label>("MainVBox/Header/StatusSection/PlayersCount");
		roundInfoLabel = GetNode<Label>("MainVBox/Header/StatusSection/RoundInfo");
		playerName = GetNode<Label>("MainVBox/Header/StatusSection/PlayerName");
		gameIdLabel = GetNode<Label>("MainVBox/Header/TitleBox/GameInfo");
		
		// Buttons
		sendSequenceButton = GetNode<Button>("MainVBox/Content/GamePanel/GameMargin/GameVBox/ControlsSection/MasterSection/MasterControlsHBox/SendSequenceButton");
		nextRoundButton = GetNode<Button>("MainVBox/Content/GamePanel/GameMargin/GameVBox/ControlsSection/MasterSection/MasterControlsHBox/NextRoundButton");
		disconnectButton = GetNode<Button>("MainVBox/Header/ActionButtons/DisconnectButton");
		copyGameIdButton = GetNode<Button>("MainVBox/Header/ActionButtons/CopyGameIdButton");
		startGameButton = GetNode<Button>("MainVBox/Content/GamePanel/GameMargin/GameVBox/ControlsSection/MasterSection/GameStartEndHBox/StartGameButton");
		endGameButton = GetNode<Button>("MainVBox/Content/GamePanel/GameMargin/GameVBox/ControlsSection/MasterSection/GameStartEndHBox/EndGameButton");
		
		// Players list
		playersList = GetNode<VBoxContainer>("MainVBox/Content/PlayersPanel/PlayersMargin/PlayersVBox/PlayersList");

		// Get color buttons from unified section
		Godot.Collections.Array<Node> colorButtons = GetNode("MainVBox/Content/GamePanel/GameMargin/GameVBox/ControlsSection/SequenceButtonsGrid").GetChildren();

		buttons = new Godot.Collections.Array<Node>();
		foreach (Node btn in colorButtons)
			buttons.Add(btn);

		if (GetTree().Root.HasNode("NetworkManager"))
		{
			networkManager = GetTree().Root.GetNode<NetworkManager>("NetworkManager");
			isMaster = networkManager.PlayerRole == "master";
			ConnectNetworkSignals();
		}

		if (disconnectButton != null)
			disconnectButton.Pressed += OnDisconnectPressed;
		if (sendSequenceButton != null)
			sendSequenceButton.Pressed += OnSendSequence;
		if (nextRoundButton != null)
			nextRoundButton.Pressed += OnNextRound;
		if (startGameButton != null)
			startGameButton.Pressed += OnStartGamePressed;
		if (endGameButton != null)
			endGameButton.Pressed += OnEndGamePressed;
		if (copyGameIdButton != null)
			copyGameIdButton.Pressed += OnCopyGameIdPressed;

		ConnectButtons();
		DisableAllGameButtons();

		SetupNetworkGame();
	}

	private void SetupNetworkGame()
	{
		GD.Print($"Network game setup - IsMaster: {isMaster}");

		if (roleLabel != null)
			roleLabel.Text = isMaster ? "👑 MASTER" : "🎮 PLAYER";
		if (gameInfoLabel != null && networkManager != null)
			gameInfoLabel.Text = $"Game: {networkManager.GameId}";
		if (gameIdLabel != null && networkManager != null)
			gameIdLabel.Text = $"Game: {networkManager.GameId}";
		if (playerName != null && networkManager != null)
			playerName.Text = $"Player: {networkManager.PlayerName}";
		if (playersCountLabel != null)
			playersCountLabel.Text = $"Players: {connectedPlayers}";
		if (roundInfoLabel != null)
			roundInfoLabel.Text = $"Round: {roundNumber}";
		
		// Request the full player list from the server
		if (networkManager != null)
		{
			networkManager.GetPlayerList();
		}
		
		// Toggle visibility of master and player sections
		if (masterSection != null)
			masterSection.Visible = isMaster;
		if (playerSection != null)
			playerSection.Visible = !isMaster;

		if (isMaster)
		{
			if (labelInfo != null)
				labelInfo.Text = "Waiting for players to join... ⏳";
			if (feedbackLabel != null)
				feedbackLabel.Visible = false;
		}
		else
		{
			if (labelInfo != null)
				labelInfo.Text = "Waiting for master...";
			if (sequenceLabel != null)
				sequenceLabel.Visible = false;
		}
	}

	private void StartMasterGame()
	{
		sequence.Clear();
		AddColorToSequence();
	}

	private void ConnectButtons()
	{
		foreach (ColorButton btn in buttons)
		{
			btn.Pressed += () => OnButtonPressed(btn.ColorIndex);
		}
	}

	private void ConnectNetworkSignals()
	{
		if (networkManager == null)
		{
			GD.PrintErr("NetworkManager not found - network signals not connected");
			return;
		}

		networkManager.ShowPattern += OnShowPattern;
		networkManager.RoundChanged += OnRoundChanged;
		networkManager.PlayerSubmitted += OnPlayerSubmitted;
		networkManager.GameEnded += OnGameEnded;
		networkManager.PlayerJoined += OnPlayerJoined;
		networkManager.PlayerListReceived += OnPlayerListReceived;

		GD.Print("Network signals connected");
	}

	// Handler for round changed (master builds sequence, player waits)
	private void OnRoundChanged(int newRoundNumber)
	{
		GD.Print($"Game: Round changed to {newRoundNumber}");

		roundNumber = newRoundNumber;
		
		if (roundInfoLabel != null)
			roundInfoLabel.Text = $"Round: {roundNumber}";

		ClearPlayerFeedback();

		if (isMaster)
		{
			// Master prepares to build sequence for this round
			sequence.Clear();
			playerInput.Clear();
			playerResponseCount = 0;
			isPlayerTurn = true;
			if (labelInfo != null)
				labelInfo.Text = $"Round {newRoundNumber}\nClick buttons to build sequence 🎯";
			GD.Print($"Master: Ready to build sequence for round {newRoundNumber}");
			EnableGameButtons();
		}
		else
		{
			// Player waits for pattern
			if (labelInfo != null)
				labelInfo.Text = $"Round {roundNumber}\nWaiting for master...";
		}
	}

	// Handler for when pattern is received from server (players only)
	private void OnShowPattern(int[] pattern, int roundNumber)
	{
		GD.Print($"Game: Pattern received - Round {roundNumber}: {string.Join(",", pattern)}");
		ReceiveSequence(pattern);
	}

	// Handler for player submission result from server
	public void OnPlayerSubmitted(string playerNameReceived, bool isCorrect, int pointsEarned, int totalScore)
	{
		// Get current player name from NetworkManager
		string currentPlayerName = networkManager != null ? networkManager.PlayerName : "";
		
		if (currentPlayerName == playerNameReceived)
		{
			GD.Print($"Game: {playerNameReceived} submitted - {isCorrect} (+{pointsEarned}pts, total: {totalScore})");
			if (isCorrect)
			{
				labelInfo.Text = $"✅ Correct! +{pointsEarned}pts (Total: {totalScore})";
			}
			else
			{
				labelInfo.Text = $"❌ Wrong! (Total: {totalScore})";
			}
			// Disable player input until next turn
			isPlayerTurn = false;
			DisablePlayerButtons();
		}

		// Update player list with feedback - find the player by name and update their label
		if (playersLabels.ContainsKey(playerNameReceived))
		{
			Label playerLabel = playersLabels[playerNameReceived];
			if (isCorrect)
			{
				playerLabel.Text = $"🎮 {playerNameReceived} ✅";
				playerLabel.AddThemeColorOverride("font_color", new Color(0.2f, 1, 0.4f, 1));
			}
			else
			{
				playerLabel.Text = $"🎮 {playerNameReceived} ❌";
				playerLabel.AddThemeColorOverride("font_color", new Color(1, 0.3f, 0.3f, 1));
			}
			GD.Print($"Updated player list for {playerNameReceived}: {(isCorrect ? "✅" : "❌")}");
		}
		else
		{
			GD.PrintErr($"Player {playerNameReceived} not found in player list!");
		}

		if (isMaster)
		{
			playerResponseCount++;
			if (playerResponseCount == connectedPlayers - 1)
			{
				nextRoundButton.Disabled = false;
				if (roundNumber >= 3)
				{
					endGameButton.Disabled = false;
				}
			}
		}
	}

	// Handler for game ended signal from server
	public void OnGameEnded(string leaderboardJson)
	{
		GD.Print($"Game: Game ended - Leaderboard: {leaderboardJson}");

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

			GD.Print($"Processing: {playerNameStr} with score {score}");

			if (score > bestScore)
			{
				bestScore = score;
				winner = playerNameStr;
			}
		}

		labelInfo.Text = $"🏁 GAME FINISHED!\nWinner: {winner} ({bestScore}pts)";
		isPlayerTurn = false;

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
		ResultPanelData.LeaderboardJson = leaderboardJson;

		// Load ResultPanel scene with deferred call
		GetTree().CallDeferred("change_scene_to_file", "res://ResultPanel.tscn");
	}

	private void AddColorToSequence()
	{
		if (isMaster)
		{
			isPlayerTurn = true;
			if (labelInfo != null)
				labelInfo.Text = "Click buttons to build your sequence 🎯";
		}
	}

	// Show sequence
	private async Task PlaySequence()
	{
		isPlayerTurn = false;
		playerInput.Clear();
		labelInfo.Text = "Watch 👀";

		foreach (int index in sequence)
			await FlashButton(index);

		if (isMaster)
		{
			labelInfo.Text = "Starting round...";
			// Le serveur envoie automatiquement le pattern via StartRound
			await Task.Delay(1000);
			labelInfo.Text = "Waiting for players...";
		}
		else
		{
			labelInfo.Text = "Your turn 🎯";
			isPlayerTurn = true;
		}
	}

	// Note: Le serveur génère maintenant automatiquement la séquence
	// Le master démarre simplement le round avec StartGame() ou NextRound()

	// Flash a button
	private async Task FlashButton(int index)
	{
		if (index < 0 || index >= buttons.Count)
			return;

		ColorButton btn = buttons[index] as ColorButton;
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
		if (index < 0 || index >= buttons.Count)
			return;

		ColorButton btn = buttons[index] as ColorButton;
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
		if (!isPlayerTurn)
			return;

		if (isMaster)
		{
			sequence.Add(index);
			GD.Print($"Master added to sequence: {index}, sequence length: {sequence.Count}");
			sendSequenceButton.Disabled = false;
			if (labelInfo != null)
				labelInfo.Text = $"Sequence built: {string.Join(",", sequence)} 🎯\nClick 'Send' when ready!";
			return;
		}

		playerInput.Add(index);
		int i = playerInput.Count - 1;

		if (playerInput[i] != sequence[i])
		{
			labelInfo.Text = "❌ Wrong!";
			isPlayerTurn = false;
			DisablePlayerButtons();
			SendAnswerToServer(playerInput.ToArray());
			return;
		}

		if (playerInput.Count == sequence.Count)
		{
			SendAnswerToServer(playerInput.ToArray());
			labelInfo.Text = "Validating with server...";
			isPlayerTurn = false;
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
		GD.Print($"Player sent answer: {string.Join(",", answer)}");
	}

	// Public method to receive sequence from server (for players)
	public void ReceiveSequence(int[] seq)
	{
		if (isMaster)
			return;

		sequence = seq.ToList();
		GD.Print($"Player received sequence: {string.Join(",", seq)}");
		_ = PlayPlayerSequence();
	}

	private async Task PlayPlayerSequence()
	{
		isPlayerTurn = false;
		labelInfo.Text = "Watch 👀";

		foreach (int index in sequence)
			await FlashPlayerButton(index);

		// Enable buttons for player to respond
		EnableGameButtons();
		labelInfo.Text = "Your turn 🎯";
		isPlayerTurn = true;
		playerInput.Clear();
	}
	public void OnDisconnectPressed()
	{
		GD.Print("Disconnect pressed");
		if (networkManager != null)
		{
			networkManager.Disconnect();
		}
		GetTree().ChangeSceneToFile("res://Hub.tscn");
	}

	public void OnRetryPressed()
	{
		GD.Print("Retry pressed");
		// Return to Game scene to play again
		GetTree().ChangeSceneToFile("res://Game.tscn");
	}

	public void OnHubPressed()
	{
		GD.Print("Back to hub pressed");
		if (networkManager != null)
		{
			networkManager.Disconnect();
		}
		GetTree().ChangeSceneToFile("res://Hub.tscn");
	}

	public void OnSendSequence()
	{
		if (!isMaster || networkManager == null)
		{
			GD.PrintErr("OnSendSequence: Not a master or not in network game");
			return;
		}

		GD.Print($"Master starting round");
		networkManager.StartRound(sequence);
		labelInfo.Text = "Sequence sent!\nWaiting for players...";
		DisableAllGameButtons();
	}

	public void OnNextRound()
	{
		if (!isMaster || networkManager == null)
		{
			GD.PrintErr("OnNextRound: Not a master or not in network game");
			return;
		}

		GD.Print("Master starting next round");
		networkManager.NextRound();
		labelInfo.Text = "Starting next round...\nWaiting for players...";
		nextRoundButton.Disabled = true;
		endGameButton.Disabled = true;
	}

	public void OnSubmitAnswer()
	{
		if (isMaster || networkManager == null)
		{
			GD.PrintErr("OnSubmitAnswer: Player only function");
			return;
		}

		GD.Print($"Player sending answer: {string.Join(",", playerInput)}");
		// TODO: Calculer le vrai temps de réaction si nécessaire
		long reactionTimeMs = 2000; // Temps par défaut
		networkManager.SubmitAttempt(playerInput.ToArray(), reactionTimeMs);
		labelInfo.Text = "Answer sent!\nWaiting for validation...";
	}

	public void OnColorButtonPressed(int colorIndex)
	{
		GD.Print($"Color button pressed: {colorIndex}");
		OnButtonPressed(colorIndex);
	}

	private void DisableAllGameButtons()
	{
		// Disable all color buttons
		foreach (ColorButton btn in buttons)
		{
			btn.Disabled = true;
		}

		// Disable send/submit buttons
		if (sendSequenceButton != null)
			sendSequenceButton.Disabled = true;

		// Disable start button until players join
		if (startGameButton != null)
			startGameButton.Disabled = true;

		GD.Print("All game buttons disabled");
	}

	private void EnableGameButtons()
	{
		// Enable color buttons
		foreach (ColorButton btn in buttons)
		{
			btn.Disabled = false;
		}

		GD.Print("Game buttons enabled");
	}

	private void DisablePlayerButtons()
	{
		// Disable all color buttons (unified button grid)
		foreach (Node btn in buttons)
		{
			if (btn is ColorButton colorBtn)
				colorBtn.Disabled = true;
		}

		GD.Print("Player buttons disabled");
	}

	public void OnPlayerJoined(string playerName, string playerId)
	{
		GD.Print($"Game: Player joined - {playerName} (ID: {playerId})");

		// The server will send the complete player list with roles via PlayerListReceived
		// so we don't need to manually add players here - just update the UI

		if (labelInfo != null)
		{
			labelInfo.Text = $"✅ {playerName} joined!";
		}

		// Enable start button when at least 1 other player connected (besides master)
		if (startGameButton != null && isMaster)
		{
			startGameButton.Disabled = false; // Enable if we have at least 1 player
		}
	}

	private void AddPlayerToList(string playerName)
	{
		if (playersList == null)
			return;

		// Don't add duplicate if already in list
		if (playersLabels.ContainsKey(playerName))
		{
			GD.Print($"Player {playerName} already in list");
			return;
		}

		// Remove empty label if it exists
		if (playersList.GetChildCount() == 1 && playersList.GetChild(0) is Label emptyLabel && emptyLabel.Text.Contains("No players"))
		{
			playersList.RemoveChild(emptyLabel);
			emptyLabel.QueueFree();
		}

		// Create a label for the player
		Label playerLabel = new Label();
		playerLabel.Text = $"🎮 {playerName}";
		playerLabel.AddThemeColorOverride("font_color", new Color(0.2f, 1, 0.4f, 1));
		playerLabel.AddThemeFontSizeOverride("font_size", 14);
		playerLabel.HorizontalAlignment = HorizontalAlignment.Left;
		
		playersList.AddChild(playerLabel);
		playersLabels[playerName] = playerLabel;

		GD.Print($"Added player {playerName} to list");
	}

	public void OnPlayerListReceived(string playerListJson)
	{
		GD.Print($"Player list received: {playerListJson}");

		try
		{
			var json = new Json();
			json.Parse(playerListJson);
			var playerList = (Godot.Collections.Array)json.Data;

			// Clear the old player list display
			foreach (Node child in playersList.GetChildren())
			{
				playersList.RemoveChild(child);
				child.QueueFree();
			}
			playersLabels.Clear();

			// Add all players from the server's player list with their roles
			foreach (var entry in playerList)
			{
				var playerData = (Godot.Collections.Dictionary)entry;
				string playerNameStr = playerData["playerName"].AsString();
				string roleStr = playerData.ContainsKey("role") ? playerData["role"].AsString() : "player";

				// Create display name with role indicator
				string displayName = roleStr == "master" ? $"👑 {playerNameStr}" : $"🎮 {playerNameStr}";
				AddPlayerToListWithRole(playerNameStr, displayName, roleStr);
			}

			// Update player count
			if (playersCountLabel != null)
				playersCountLabel.Text = $"Players: {playerList.Count}";
			
			connectedPlayers = playerList.Count;

			GD.Print($"Player list refreshed: {playerList.Count} players");
		}
		catch (Exception ex)
		{
			GD.PrintErr($"Error processing player list: {ex.Message}");
		}
	}

	private void AddPlayerToListWithRole(string playerName, string displayName, string role)
	{
		if (playersList == null)
			return;

		// Don't add duplicate if already in list
		if (playersLabels.ContainsKey(playerName))
		{
			GD.Print($"Player {playerName} already in list");
			return;
		}

		// Remove empty label if it exists
		if (playersList.GetChildCount() == 1 && playersList.GetChild(0) is Label emptyLabel && emptyLabel.Text.Contains("No players"))
		{
			playersList.RemoveChild(emptyLabel);
			emptyLabel.QueueFree();
		}

		// Create a label for the player
		Label playerLabel = new Label();
		playerLabel.Text = displayName;
		
		// Master color is golden/yellow, player color is green
		Color textColor = role == "master" ? new Color(1, 0.84f, 0, 1) : new Color(0.2f, 1, 0.4f, 1);
		playerLabel.AddThemeColorOverride("font_color", textColor);
		playerLabel.AddThemeFontSizeOverride("font_size", 14);
		playerLabel.HorizontalAlignment = HorizontalAlignment.Left;
		
		playersList.AddChild(playerLabel);
		playersLabels[playerName] = playerLabel;

		GD.Print($"Added player {playerName} to list (role: {role})");
	}

	private void ClearPlayerFeedback()
	{
		// Remove ✅/❌ feedback from all player labels
		foreach (var kvp in playersLabels)
		{
			string playerName = kvp.Key;
			Label playerLabel = kvp.Value;
			
			// Get the player's role to determine the emoji
			bool isMasterRole = playerLabel.Text.Contains("👑");
			string emoji = isMasterRole ? "👑" : "🎮";
			
			// Reset the label to just name + emoji without feedback
			playerLabel.Text = $"{emoji} {playerName}";
			
			// Reset color to default (golden for master, green for player)
			Color textColor = isMasterRole ? new Color(1, 0.84f, 0, 1) : new Color(0.2f, 1, 0.4f, 1);
			playerLabel.AddThemeColorOverride("font_color", textColor);
		}
		
		GD.Print("Cleared player feedback icons for new round");
	}

	public void OnStartGamePressed()
	{
		GD.Print("Start game button pressed");
		if (isMaster && connectedPlayers > 0 && !gameStarted)
		{
			gameStarted = true;
			EnableGameButtons();
			StartMasterGame();
			networkManager.StartGame();
			startGameButton.Disabled = true;
		}
		else if (connectedPlayers == 0)
		{
			if (labelInfo != null)
				labelInfo.Text = "Cannot start: No players connected";
		}
	}

	public void OnEndGamePressed()
	{
		GD.Print("End game button pressed");
		if (isMaster && networkManager != null)
		{
			if (labelInfo != null)
				labelInfo.Text = "🏁 Game ended by master!";
			networkManager.StopGame();
			gameStarted = false;
		}
		DisableAllGameButtons();
	}

	public async void OnCopyGameIdPressed()
	{
		if (networkManager != null && !string.IsNullOrEmpty(networkManager.GameId))
		{
			DisplayServer.ClipboardSet(networkManager.GameId);
			GD.Print($"Game ID copied to clipboard: {networkManager.GameId}");
			if (copyGameIdButton != null)
			{
				// Temporary feedback
				string originalText = copyGameIdButton.Text;
				copyGameIdButton.Text = "✓ Copied!";
				await ToSignal(GetTree().CreateTimer(2.0f), "timeout");
				copyGameIdButton.Text = originalText;
			}
		}
		else
		{
			GD.PrintErr("Game ID not available");
		}
	}
}
