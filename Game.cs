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
	private bool isNetworkGame = false;
	private bool isMaster = false;

	private Godot.Collections.Array<Node> buttons;
	private Label labelInfo;
	private Label roleLabel;
	private Label gameIdLabel;
	private Label sequenceLabel;
	private Label feedbackLabel;
	private Button sendSequenceButton;
	private Button submitButton;
	private Button disconnectButton;
	private NetworkManager networkManager;
	private ResultPanel resultPanel;
	private Control masterSection;
	private Control playerSection;

	public override void _Ready()
	{
		// Get master and player sections
		masterSection = GetNode<Control>("Main/Game Area/Master Section");
		playerSection = GetNode<Control>("Main/Game Area/Player Section");
		
		// Get labels
		sequenceLabel = GetNode<Label>("Main/Game Area/Master Section/SequenceLabel");
		feedbackLabel = GetNode<Label>("Main/Game Area/Player Section/FeedbackLabel");
		
		// Get buttons
		sendSequenceButton = GetNode<Button>("Main/Game Area/Master Section/SendSequenceButton");
		submitButton = GetNode<Button>("Main/Game Area/Player Section/SubmitButton");
		disconnectButton = GetNode<Button>("Main/Status/DisconnectButton");
		
		// Get labels from Status
		roleLabel = GetNode<Label>("Main/Status/RoleLabel");
		gameIdLabel = GetNode<Label>("Main/Status/StateLabel");
		
		// Get ResultPanel - try to get as ResultPanel, fallback to Panel if script not attached yet
		try
		{
			resultPanel = GetNode<ResultPanel>("Main/ResultPanel");
		}
		catch
		{
			// If ResultPanel script is not attached, get as Panel and we'll handle it
			var panelNode = GetNode<Panel>("Main/ResultPanel");
			if (panelNode is ResultPanel rp)
				resultPanel = rp;
			else
				GD.PrintErr("ResultPanel node found but script not attached properly");
		}
		
		// Get color buttons - Master section
		Godot.Collections.Array<Node> masterButtons = GetNode("Main/Game Area/Master Section/Buttons").GetChildren();
		// Get color buttons - Player section  
		Godot.Collections.Array<Node> playerButtons = GetNode("Main/Game Area/Player Section/Buttons").GetChildren();
		
		// Combine both button arrays
		buttons = new Godot.Collections.Array<Node>();
		foreach (Node btn in masterButtons)
			buttons.Add(btn);
		foreach (Node btn in playerButtons)
			buttons.Add(btn);
		
		// Optional info label
		if (HasNode("Main/Game Area/Master Section/Label"))
			labelInfo = GetNode<Label>("Main/Game Area/Master Section/Label");
		if (HasNode("Main/Game Area/Player Section/Joueur"))
			labelInfo = GetNode<Label>("Main/Game Area/Player Section/Joueur");

		// Check if network manager exists
		if (GetTree().Root.HasNode("NetworkManager"))
		{
			networkManager = GetTree().Root.GetNode<NetworkManager>("NetworkManager");
			isNetworkGame = networkManager.IsConnected;
			isMaster = networkManager.PlayerRole == "master";
			
			// Connect network signals for game updates
			ConnectNetworkSignals();
		}

		// Connect UI button signals
		if (disconnectButton != null)
			disconnectButton.Pressed += OnDisconnectPressed;
		if (sendSequenceButton != null)
			sendSequenceButton.Pressed += OnSendSequence;
		if (submitButton != null)
			submitButton.Pressed += OnSubmitAnswer;
		if (resultPanel != null)
		{
			GetNode<Button>("Main/ResultPanel/VBoxContainer/Buttons/RetryButton").Pressed += OnRetryPressed;
			GetNode<Button>("Main/ResultPanel/VBoxContainer/Buttons/BackToHubButton").Pressed += OnHubPressed;
		}

		ConnectButtons();

		if (isNetworkGame)
		{
			SetupNetworkGame();
		}
		else
		{
			StartLocalGame();
		}
	}

	private void SetupNetworkGame()
	{
		GD.Print($"Network game setup - IsMaster: {isMaster}");

		if (roleLabel != null)
			roleLabel.Text = isMaster ? "👑 MASTER" : "🎮 PLAYER";
		if (gameIdLabel != null)
			gameIdLabel.Text = $"Game: {networkManager.GameId}";
		
		// Show/hide sections based on role
		if (masterSection != null)
			masterSection.Visible = isMaster;
		if (playerSection != null)
			playerSection.Visible = !isMaster;

		if (isMaster)
		{
			if (labelInfo != null)
				labelInfo.Text = "You are the MASTER\nCreate your sequence!";
			StartMasterGame();
		}
		else
		{
			if (labelInfo != null)
				labelInfo.Text = "Waiting for master...";
		}
	}

	private void StartLocalGame()
	{
		GD.Print("Local game started");
		sequence.Clear();
		AddColorToSequence();
	}

	// ========== MASTER MODE ==========

	private void StartMasterGame()
	{
		sequence.Clear();
		AddColorToSequence();
	}

	// Connect all buttons
	private void ConnectButtons()
	{
		foreach (ColorButton btn in buttons)
		{
			btn.Pressed += () => OnButtonPressed(btn.ColorIndex);
		}
	}

	// Connect network signals for receiving game updates from server
	private void ConnectNetworkSignals()
	{
		if (networkManager == null)
		{
			GD.PrintErr("NetworkManager not found - network signals not connected");
			return;
		}

		// Connect sequence signal (for players receiving the master's sequence)
		networkManager.SequenceReceived += OnSequenceReceived;

		// Connect validation signal (for players receiving validation of their answer)
		networkManager.ValidationResult += OnValidationResult;

		// Connect game ended signal (for all players)
		networkManager.GameEnded += OnGameEnded;

		GD.Print("Network signals connected");
	}

	// Handler for when sequence is received from server (players only)
	private void OnSequenceReceived(int[] sequence)
	{
		GD.Print($"Game: Sequence received - {string.Join(",", sequence)}");
		ReceiveSequence(sequence);
	}

	// Handler for validation result from server
	public void OnValidationResult(bool isCorrect, string message)
	{
		GD.Print($"Game: Validation result - {isCorrect} ({message})");
		if (isCorrect)
		{
			labelInfo.Text = $"✅ Correct! {message}";
		}
		else
		{
			labelInfo.Text = $"❌ Wrong! {message}";
		}
	}

	// Handler for game ended signal from server
	public void OnGameEnded(bool won, string reason)
	{
		GD.Print($"Game: Game ended - Won={won}, Reason={reason}");
		if (won)
		{
			labelInfo.Text = $"🎉 YOU WON!\n{reason}";
		}
		else
		{
			labelInfo.Text = $"😔 GAME OVER\n{reason}";
		}

		isPlayerTurn = false;
	}

	// Game Master adds one color
	private void AddColorToSequence()
	{
		int randomIndex = (int)(GD.Randi() % (uint)buttons.Count);
		sequence.Add(randomIndex);
		_ = PlaySequence();
	}

	// Show sequence
	private async Task PlaySequence()
	{
		isPlayerTurn = false;
		playerInput.Clear();
		labelInfo.Text = "Watch 👀";

		foreach (int index in sequence)
			await FlashButton(index);

		if (isNetworkGame && isMaster)
		{
			labelInfo.Text = "Sending sequence to players...";
			SendSequenceToServer();
			await Task.Delay(1000);
			labelInfo.Text = "Waiting for players...";
		}
		else
		{
			labelInfo.Text = "Your turn 🎯";
			isPlayerTurn = true;
		}
	}

	private void SendSequenceToServer()
	{
		if (networkManager == null)
			return;

		int[] seq = sequence.ToArray();
		networkManager.SendSequence(seq);
		GD.Print($"Master sent sequence: {string.Join(",", seq)}");
	}

	// Flash a button
	private async Task FlashButton(int index)
	{
		if (index < 0 || index >= buttons.Count)
			return;

		ColorButton btn = buttons[index] as ColorButton;
		if (btn == null)
			return;

		Color original = btn.Modulate;

		btn.Modulate = Colors.White;
		await ToSignal(GetTree().CreateTimer(0.4f), "timeout");

		btn.Modulate = original;
		await ToSignal(GetTree().CreateTimer(0.2f), "timeout");
	}

	// Player clicks
	private async void OnButtonPressed(int index)
	{
		if (!isPlayerTurn)
			return;

		playerInput.Add(index);
		await FlashButton(index);
		int i = playerInput.Count - 1;

		if (playerInput[i] != sequence[i])
		{
			labelInfo.Text = "❌ Wrong!";
			isPlayerTurn = false;
			return;
		}

		if (playerInput.Count == sequence.Count)
		{
			if (isNetworkGame)
			{
				SendAnswerToServer(playerInput.ToArray());
				labelInfo.Text = "Validating with server...";
				isPlayerTurn = false;
			}
			else
			{
				await Task.Delay(1000);
				AddColorToSequence();
			}
		}
	}

	private void SendAnswerToServer(int[] answer)
	{
		if (networkManager == null)
			return;

		networkManager.SendPlayerAnswer(answer);
		GD.Print($"Player sent answer: {string.Join(",", answer)}");
	}

	// Public method to receive sequence from server (for players)
	public void ReceiveSequence(int[] seq)
	{
		if (!isNetworkGame || isMaster)
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
			await FlashButton(index);

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
		// hide result panel if shown
		if (resultPanel != null)
			resultPanel.Visible = false;

		// restart game depending on mode/role
		if (isNetworkGame)
		{
			if (isMaster)
			{
				StartMasterGame();
			}
			else
			{
				// wait for master to send sequence
				if (labelInfo != null)
					labelInfo.Text = "Waiting for master...";
			}
		}
		else
		{
			StartLocalGame();
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
		if (!isMaster || !isNetworkGame || networkManager == null)
		{
			GD.PrintErr("OnSendSequence: Not a master or not in network game");
			return;
		}

		GD.Print($"Master sending sequence: {string.Join(",", sequence)}");
		networkManager.SendSequence(sequence.ToArray());
		labelInfo.Text = "Sequence sent!\nWaiting for players...";
	}

	public void OnSubmitAnswer()
	{
		if (isMaster || !isNetworkGame || networkManager == null)
		{
			GD.PrintErr("OnSubmitAnswer: Player only function");
			return;
		}

		GD.Print($"Player sending answer: {string.Join(",", playerInput)}");
		networkManager.SendPlayerAnswer(playerInput.ToArray());
		labelInfo.Text = "Answer sent!\nWaiting for validation...";
	}

	public void OnColorButtonPressed(int colorIndex)
	{
		GD.Print($"Color button pressed: {colorIndex}");
		OnButtonPressed(colorIndex);
	}
}
