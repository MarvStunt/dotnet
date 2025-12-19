using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Hub scene - Create or Join a game
/// </summary>
public partial class Hub : Control
{
	private NetworkManager networkManager;
	private LineEdit playerNameInputCreate;
	private LineEdit playerNameInputJoin;
	private LineEdit gameIdInput;
	private Button createGameButton;
	private Button joinGameButton;
	private Label statusLabel;
	private bool isWaitingForResponse = false;
	private string currentOperation = "";

	public override void _Ready()
	{
		// Try to find network manager
		if (!IsNodeReady())
			return;

		// Get network manager - create if doesn't exist
		if (GetTree().Root.HasNode("NetworkManager"))
		{
			networkManager = GetTree().Root.GetNode<NetworkManager>("NetworkManager");
		}
		else
		{
			networkManager = new NetworkManager();
			// Use CallDeferred to avoid "Parent node is busy setting up children" error
			GetTree().Root.CallDeferred("add_child", networkManager);
			networkManager.Name = "NetworkManager";
		}

		// Find UI elements
		playerNameInputCreate = GetNode<LineEdit>("MainVBox/Content/CreateGameCard/CreateGameMargin/CreateGameVBox/PlayerNameInput_Create");
		createGameButton = GetNode<Button>("MainVBox/Content/CreateGameCard/CreateGameMargin/CreateGameVBox/CreateGameButton");
		gameIdInput = GetNode<LineEdit>("MainVBox/Content/JoinGameCard/JoinGameMargin/JoinGameVBox/GameIdInput");
		playerNameInputJoin = GetNode<LineEdit>("MainVBox/Content/JoinGameCard/JoinGameMargin/JoinGameVBox/PlayerNameInput_Join");
		joinGameButton = GetNode<Button>("MainVBox/Content/JoinGameCard/JoinGameMargin/JoinGameVBox/JoinGameButton");
		statusLabel = GetNode<Label>("MainVBox/StatusPanel/StatusMargin/StatusLabel");

		// Connect buttons
		createGameButton.Pressed += OnCreateGamePressed;
		joinGameButton.Pressed += OnJoinGamePressed;

		// Connect network manager signals
		networkManager.OperationFailed += OnOperationFailed;
		networkManager.GameCreated += OnGameCreated;
		networkManager.PlayerJoinedGame += OnPlayerJoinedGame;

		statusLabel.Text = "Welcome to Memory Game";
	}

	public override void _ExitTree()
	{
		// Disconnect signals to prevent errors when Hub is freed but NetworkManager persists
		if (networkManager != null)
		{
			networkManager.OperationFailed -= OnOperationFailed;
			networkManager.GameCreated -= OnGameCreated;
			networkManager.PlayerJoinedGame -= OnPlayerJoinedGame;
		}
	}

	private void OnOperationFailed(string error, string invocationId)
	{
		// Check if Hub is still valid (not disposed/freed)
		if (!IsInsideTree() || statusLabel == null || IsQueuedForDeletion())
			return;
		
		statusLabel.Text = $"❌ Error: {error}";

		// Réactiver les boutons
		if (createGameButton != null && !createGameButton.IsQueuedForDeletion())
			createGameButton.Disabled = false;
		if (joinGameButton != null && !joinGameButton.IsQueuedForDeletion())
			joinGameButton.Disabled = false;
		
		isWaitingForResponse = false;
		currentOperation = "";
	}

	private void OnGameCreated(string gameCode)
	{
		if (!isWaitingForResponse || currentOperation != "create")
			return;

		// Check if Hub is still valid
		if (!IsInsideTree() || IsQueuedForDeletion())
			return;

		if (statusLabel != null && !statusLabel.IsQueuedForDeletion())
			statusLabel.Text = $"✅ Game created! ID: {gameCode}";
		
		isWaitingForResponse = false;

		// Changer de scène seulement si succès
		GetTree().CallDeferred("change_scene_to_file", "res://Game.tscn");
	}

	private void OnPlayerJoinedGame(bool success)
	{
		// Only process if we're actually waiting for a join response
		if (!isWaitingForResponse || currentOperation != "join")
			return;

		// Check if Hub is still valid
		if (!IsInsideTree() || IsQueuedForDeletion())
			return;

		isWaitingForResponse = false;
		currentOperation = "";

		if (success)
		{
			if (statusLabel != null && !statusLabel.IsQueuedForDeletion())
				statusLabel.Text = "✅ Joined game successfully!";
			// Changer de scène seulement si succès
			GetTree().CallDeferred("change_scene_to_file", "res://Game.tscn");
		}
		else
		{
			if (statusLabel != null && !statusLabel.IsQueuedForDeletion())
				statusLabel.Text = "❌ Failed to join game";
			if (joinGameButton != null && !joinGameButton.IsQueuedForDeletion())
				joinGameButton.Disabled = false;
		}
	}

	private void OnCreateGamePressed()
	{
		string playerName = playerNameInputCreate.Text.Trim();

		if (string.IsNullOrEmpty(playerName))
		{
			statusLabel.Text = "❌ Please enter your name";
			return;
		}

		statusLabel.Text = "⏳ Connecting to server...";
		createGameButton.Disabled = true;

		_ = CreateGame(playerName);
	}

	private async Task CreateGame(string playerName)
	{
		try
		{
			if (!networkManager.IsConnected)
			{
				// TODO: Vérifier que l'URL du serveur dans NetworkManager est correcte
				bool connected = await networkManager.ConnectToServer();
				if (!connected)
				{
					statusLabel.Text = "❌ Failed to connect to server";
					createGameButton.Disabled = false;
					return;
				}
			}

			statusLabel.Text = "⏳ Creating game...";
			isWaitingForResponse = true;
			currentOperation = "create";

			networkManager.PlayerName = playerName;
			networkManager.PlayerRole = Roles.Master;
			networkManager.CreateGame(playerName);

			// Les signaux OnGameCreated ou OnOperationFailed géreront la suite
		}
		catch (Exception ex)
		{
			GD.PrintErr($"Error creating game: {ex.Message}");
			statusLabel.Text = "❌ Error creating game";
			createGameButton.Disabled = false;
			isWaitingForResponse = false;
		}
	}

	private void OnJoinGamePressed()
	{
		string playerName = playerNameInputJoin.Text.Trim();
		string gameId = gameIdInput.Text.Trim();

		if (string.IsNullOrEmpty(playerName))
		{
			statusLabel.Text = "❌ Please enter your name";
			return;
		}

		if (string.IsNullOrEmpty(gameId))
		{
			statusLabel.Text = "❌ Please enter a game ID";
			return;
		}

		statusLabel.Text = "⏳ Connecting to server...";
		joinGameButton.Disabled = true;

		_ = JoinGame(gameId, playerName);
	}

	private async Task JoinGame(string gameId, string playerName)
	{
		try
		{
			if (!networkManager.IsConnected)
			{
				bool connected = await networkManager.ConnectToServer();
				if (!connected)
				{
					statusLabel.Text = "❌ Failed to connect to server";
					joinGameButton.Disabled = false;
					return;
				}
			}

			statusLabel.Text = "⏳ Joining game...";
			isWaitingForResponse = true;
			currentOperation = "join";

			networkManager.PlayerName = playerName;
			networkManager.PlayerRole = Roles.Player;
			networkManager.JoinGame(gameId, playerName);

			// Les signaux OnGameCreated ou OnOperationFailed géreront la suite
		}
		catch (Exception ex)
		{
			GD.PrintErr($"Error joining game: {ex.Message}");
			statusLabel.Text = "❌ Error joining game";
			joinGameButton.Disabled = false;
			isWaitingForResponse = false;
		}
	}
}
