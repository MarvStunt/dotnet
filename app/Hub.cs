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
		playerNameInputCreate = GetNode<LineEdit>("VBoxContainer/CreateGamePanel/CreateGameSubPanel/PlayerNameInput_Create");
		createGameButton = GetNode<Button>("VBoxContainer/CreateGamePanel/CreateGameSubPanel/CreateGameButton");
		gameIdInput = GetNode<LineEdit>("VBoxContainer/JoinGamePanel/JoinGameSubPanel/GameIdInput");
		playerNameInputJoin = GetNode<LineEdit>("VBoxContainer/JoinGamePanel/JoinGameSubPanel/PlayerNameInput_Join");
		joinGameButton = GetNode<Button>("VBoxContainer/JoinGamePanel/JoinGameSubPanel/JoinGameButton");
		statusLabel = GetNode<Label>("VBoxContainer/StatusLabel");

		// Connect buttons
		createGameButton.Pressed += OnCreateGamePressed;
		joinGameButton.Pressed += OnJoinGamePressed;

		statusLabel.Text = "Welcome to Memory Game";
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
			networkManager.CreateGame(playerName);

			// Wait a bit for response
			await Task.Delay(2000);

			// Check if game was started
			if (!string.IsNullOrEmpty(networkManager.GameId))
			{
				statusLabel.Text = $"✅ Game created! ID: {networkManager.GameId}";
				await Task.Delay(1000);
				GetTree().ChangeSceneToFile("res://Game.tscn");
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr($"Error creating game: {ex.Message}");
			statusLabel.Text = "❌ Error creating game";
			createGameButton.Disabled = false;
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
			networkManager.JoinGame(gameId, playerName);

			// Wait for response
			await Task.Delay(2000);

			if (!string.IsNullOrEmpty(networkManager.GameId))
			{
				statusLabel.Text = $"✅ Joined game! Role: {networkManager.PlayerRole}";
				await Task.Delay(1000);
				GetTree().ChangeSceneToFile("res://Game.tscn");
			}
			else
			{
				statusLabel.Text = "❌ Failed to join game";
				joinGameButton.Disabled = false;
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr($"Error joining game: {ex.Message}");
			statusLabel.Text = "❌ Error joining game";
			joinGameButton.Disabled = false;
		}
	}
}
