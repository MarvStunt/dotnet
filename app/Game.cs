using Godot;
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
	private Label sequenceLabel;
	private Label feedbackLabel;
	private Label playerName;
	private Button sendSequenceButton;
	private Button nextRoundButton;
	private Button disconnectButton;
	private Button startGameButton;
	private Button endGameButton;
	private Button copyGameIdButton;
	private NetworkManager networkManager;
	private ResultPanel resultPanel;
	private Control masterSection;
	private Control playerSection;
	private Button retryButton;
	private Button hubButton;

	private int playerResponseCount = 0;

	private int roundNumber = 0;

	public override void _Ready()
	{
		masterSection = GetNode<Control>("Main/Game Area/Master Section");
		playerSection = GetNode<Control>("Main/Game Area/Player Section");
		sequenceLabel = GetNode<Label>("Main/Game Area/Master Section/SequenceLabel");
		feedbackLabel = GetNode<Label>("Main/Game Area/Player Section/FeedbackLabel");
		sendSequenceButton = GetNode<Button>("Main/Game Area/Master Section/SendSequenceButton");
		nextRoundButton = GetNode<Button>("Main/Game Area/Master Section/NextRoundButton");
		disconnectButton = GetNode<Button>("Main/Status/DisconnectButton");
		roleLabel = GetNode<Label>("Main/Status/RoleLabel");
		gameIdLabel = GetNode<Label>("Main/Status/StateLabel");
		playerName = GetNode<Label>("Main/Status/PlayerName");
		copyGameIdButton = GetNode<Button>("Main/Status/CopyGameIdButton");
		resultPanel = GetNode<ResultPanel>("Main/ResultPanel");

		startGameButton = GetNode<Button>("Main/Game Area/Master Section/StartGameButton");
		endGameButton = GetNode<Button>("Main/Game Area/Master Section/EndGameButton");

		Godot.Collections.Array<Node> masterButtons = GetNode("Main/Game Area/Master Section/Buttons").GetChildren();
		Godot.Collections.Array<Node> playerButtons = GetNode("Main/Game Area/Player Section/Buttons").GetChildren();

		buttons = new Godot.Collections.Array<Node>();
		foreach (Node btn in masterButtons)
			buttons.Add(btn);
		foreach (Node btn in playerButtons)
			buttons.Add(btn);

		if (HasNode("Main/Game Area/Master Section/Label"))
			labelInfo = GetNode<Label>("Main/Game Area/Master Section/Label");
		if (HasNode("Main/Game Area/Player Section/Joueur"))
			labelInfo = GetNode<Label>("Main/Game Area/Player Section/Joueur");

		if (GetTree().Root.HasNode("NetworkManager"))
		{
			networkManager = GetTree().Root.GetNode<NetworkManager>("NetworkManager");
			isMaster = networkManager.PlayerRole == "master";
			ConnectNetworkSignals();
		}

		// Get result panel buttons from ResultPanel
		if (resultPanel != null)
		{
			retryButton = resultPanel.GetNode<Button>("VBoxContainer/Buttons/RetryButton");
			hubButton = resultPanel.GetNode<Button>("VBoxContainer/Buttons/BackToHubButton");
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
		if (resultPanel != null && retryButton != null && hubButton != null)
		{
			retryButton.Pressed += OnRetryPressed;
			hubButton.Pressed += OnHubPressed;
		}

		ConnectButtons();
		DisableAllGameButtons();

		SetupNetworkGame();
	}

	private void SetupNetworkGame()
	{
		GD.Print($"Network game setup - IsMaster: {isMaster}");

		if (roleLabel != null)
			roleLabel.Text = isMaster ? "👑 MASTER" : "🎮 PLAYER";
		if (gameIdLabel != null && networkManager != null)
			gameIdLabel.Text = $"Game: {networkManager.GameId}";
		if (playerName != null && networkManager != null)
			playerName.Text = networkManager.PlayerName;
		if (masterSection != null)
			masterSection.Visible = isMaster;
		if (playerSection != null)
			playerSection.Visible = !isMaster;

		if (isMaster)
		{
			if (labelInfo != null)
				labelInfo.Text = "Waiting for players to join... ⏳";
			// Don't start game automatically - wait for players
		}
		else
		{
			if (labelInfo != null)
				labelInfo.Text = "Waiting for master...";
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

		GD.Print("Network signals connected");
	}

	// Handler for round changed (master builds sequence, player waits)
	private void OnRoundChanged(int newRoundNumber)
	{
		GD.Print($"Game: Round changed to {newRoundNumber}");

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
			roundNumber = newRoundNumber;
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
		if (playerName.Text == playerNameReceived)
		{
			GD.Print($"Game: {playerName.Text} submitted - {isCorrect} (+{pointsEarned}pts, total: {totalScore})");
			if (isCorrect)
			{
				labelInfo.Text = $"✅ {playerName.Text}: Correct! +{pointsEarned}pts (Total: {totalScore})";
			}
			else
			{
				labelInfo.Text = $"❌ {playerName.Text}: Wrong! (Total: {totalScore})";
			}
			// Disable player input until next turn
			isPlayerTurn = false;
			DisablePlayerButtons();
		}

		if (isMaster)
		{
			playerResponseCount++;
			if (playerResponseCount == connectedPlayers)
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
			string playerName = playerData["playerName"].AsString();
			int score = (int)playerData["score"].AsInt64();

			GD.Print($"Processing: {playerName} with score {score}");

			if (score > bestScore)
			{
				bestScore = score;
				winner = playerName;
			}
		}

		labelInfo.Text = $"🏁 GAME FINISHED!\nWinner: {winner} ({bestScore}pts)";
		isPlayerTurn = false;

		// Find current player's score
		int playerScore = 0;
		foreach (var entry in leaderboard)
		{
			var playerData = (Godot.Collections.Dictionary)entry;
			if (playerData["playerName"].AsString() == playerName.Text)
			{
				playerScore = (int)playerData["score"].AsInt64();
				break;
			}
		}

		if (playerName.Text == winner)
		{
			if (resultPanel != null)
				resultPanel.ShowWon("🎉 GG SKILL DIFF! 🎉", playerScore);
		}
		else
		{
			if (resultPanel != null)
				resultPanel.ShowLost("😞 GROS NUL! 😞", playerScore);
		}
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
		if (playerSection == null)
			return;

		Godot.Collections.Array<Node> playerButtons = playerSection.GetNode("Buttons").GetChildren();

		if (index < 0 || index >= playerButtons.Count)
			return;

		ColorButton btn = playerButtons[index] as ColorButton;
		if (btn == null)
			return;

		// Enable button
		btn.Disabled = false;
		await ToSignal(GetTree().CreateTimer(0.5f), "timeout");

		// Disable button
		btn.Disabled = true;
		await ToSignal(GetTree().CreateTimer(0.2f), "timeout");
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
		if (resultPanel != null)
			resultPanel.Visible = false;


		if (isMaster)
		{
			StartMasterGame();
		}
		else
		{
			if (labelInfo != null)
				labelInfo.Text = "Waiting for master...";
		}
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
		if (playerSection == null)
			return;

		Godot.Collections.Array<Node> playerButtons = playerSection.GetNode("Buttons").GetChildren();
		foreach (Node btn in playerButtons)
		{
			if (btn is ColorButton colorBtn)
				colorBtn.Disabled = true;
		}

		GD.Print("Player buttons disabled");
	}

	public void OnPlayerJoined(string playerName, string playerId)
	{
		connectedPlayers++;
		GD.Print($"Game: Player joined - {playerName} (ID: {playerId}), Total: {connectedPlayers}");

		if (isMaster && labelInfo != null)
		{
			labelInfo.Text = $"Players connected: {connectedPlayers} 👥\nClick 'Start Game' to begin!";
		}

		// Enable start button when at least 1 player connected
		if (startGameButton != null && isMaster)
		{
			startGameButton.Disabled = (connectedPlayers == 0);
		}
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
