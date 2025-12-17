using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>
/// Manages all network communication with the game server
/// </summary>
public partial class NetworkManager : Control
{
	private WebSocketPeer webSocketPeer;
	// TODO: Adapter cette URL selon votre serveur
	// Exemples: "ws://localhost:8080", "ws://votre-serveur.com:8080"
	private string serverUrl = "ws://localhost:8080";
	private bool isConnected = false;
	private string playerId;
	private string gameId;
	private string playerRole; // "master" or "player"

	// Signaux
	[Signal]
	public delegate void ConnectedEventHandler();

	[Signal]
	public delegate void DisconnectedEventHandler();

	[Signal]
	public delegate void ConnectionFailedEventHandler();

	[Signal]
	public delegate void MessageReceivedEventHandler(string message);

	[Signal]
	public delegate void GameStartedEventHandler(string gameId, string role);

	[Signal]
	public delegate void SequenceReceivedEventHandler(int[] sequence);

	[Signal]
	public delegate void ValidationResultEventHandler(bool isCorrect, string message);

	[Signal]
	public delegate void GameEndedEventHandler(bool won, string reason);

	[Signal]
	public delegate void PlayerJoinedEventHandler(string playerName, int totalPlayers);

	public override void _Ready()
	{
		playerId = Guid.NewGuid().ToString();
		SetProcessInternal(true);
	}

	public override void _Process(double delta)
	{
		if (isConnected && webSocketPeer != null)
		{
			webSocketPeer.Poll();
			int state = (int)webSocketPeer.GetReadyState();

			switch (state)
			{
				case (int)WebSocketPeer.State.Open:
					HandleMessages();
					break;
				case (int)WebSocketPeer.State.Closed:
					isConnected = false;
					EmitSignal(SignalName.Disconnected);
					GD.PrintErr("WebSocket disconnected");
					break;
			}
		}
	}

	/// <summary>
	/// Connect to the server
	/// </summary>
	public async Task<bool> ConnectToServer(string url = null)
	{
		if (url != null)
			serverUrl = url;

		GD.Print($"🔗 Attempting to connect to {serverUrl}...");

		if (webSocketPeer != null)
			webSocketPeer.Close();

		webSocketPeer = new WebSocketPeer();
		Error error = webSocketPeer.ConnectToUrl(serverUrl);

		if (error != Error.Ok)
		{
			GD.PrintErr($"❌ Failed to connect: {error}");
			EmitSignal(SignalName.ConnectionFailed);
			return false;
		}

		GD.Print($"🔄 Waiting for WebSocket handshake...");

		// Wait longer for connection to establish
		for (int i = 0; i < 20; i++)
		{
			await Task.Delay(100);
			webSocketPeer.Poll();

			int state = (int)webSocketPeer.GetReadyState();
			GD.Print($"   Attempt {i+1}/20 - State: {state} (Connecting=0, Open=1, Closing=2, Closed=3)");

			if (state == (int)WebSocketPeer.State.Open)
			{
				isConnected = true;
				EmitSignal(SignalName.Connected);
				GD.Print("✅ Connected to server");
				return true;
			}
		}

		GD.PrintErr($"❌ Connection timeout after 2 seconds");
		EmitSignal(SignalName.ConnectionFailed);
		return false;
	}

	/// <summary>
	/// Handle incoming messages
	/// </summary>
	private void HandleMessages()
	{
		while (webSocketPeer.GetAvailablePacketCount() > 0)
		{
			byte[] data = webSocketPeer.GetPacket();
			string json = System.Text.Encoding.UTF8.GetString(data);
			GD.Print($"Received: {json}");

			try
			{
				var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
				var message = JsonSerializer.Deserialize<Dictionary<string, object>>(json, options);

				if (message == null)
					return;

				string type = message.ContainsKey("type") ? message["type"].ToString() : "";

				switch (type)
				{
					case "game_started":
						// TODO: Adapter selon la réponse réelle de votre serveur
						HandleGameStarted(message);
						break;
					case "player_joined":
						HandlePlayerJoined(message);
						break;
					case "sequence":
						// TODO: Adapter selon la réponse réelle de votre serveur
						HandleSequenceReceived(message);
						break;
					case "validation_result":
						// TODO: Adapter selon la réponse réelle de votre serveur
						HandleValidationResult(message);
						break;
					case "game_ended":
						// TODO: Adapter selon la réponse réelle de votre serveur
						HandleGameEnded(message);
						break;
					default:
						// TODO: Gérer les autres types de messages selon votre serveur
						GD.Print($"Unknown message type: {type}");
						break;
				}
			}
			catch (Exception ex)
			{
				GD.PrintErr($"Error parsing message: {ex.Message}");
			}
		}
	}

	private void HandleGameStarted(Dictionary<string, object> message)
	{
		gameId = message.ContainsKey("game_id") ? message["game_id"].ToString() : "";
		playerRole = message.ContainsKey("role") ? message["role"].ToString() : "player";

		EmitSignal(SignalName.GameStarted, gameId, playerRole);
		GD.Print($"Game started: {gameId}, Role: {playerRole}");
	}

	private void HandlePlayerJoined(Dictionary<string, object> message)
	{
		string playerName = message.ContainsKey("player_name") ? message["player_name"].ToString() : "Unknown";
		int totalPlayers = 0;
		
		if (message.ContainsKey("total_players"))
		{
			if (message["total_players"] is JsonElement element)
				totalPlayers = element.GetInt32();
			else
				totalPlayers = Convert.ToInt32(message["total_players"]);
		}

		EmitSignal(SignalName.PlayerJoined, playerName, totalPlayers);
		GD.Print($"Player joined: {playerName}, Total players: {totalPlayers}");
	}

	private void HandleSequenceReceived(Dictionary<string, object> message)
	{
		if (message.ContainsKey("sequence") && message["sequence"] is JsonElement element)
		{
			try
			{
				var sequences = JsonSerializer.Deserialize<int[]>(element.GetRawText());
				EmitSignal(SignalName.SequenceReceived, sequences);
				GD.Print($"Sequence received: {string.Join(",", sequences)}");
			}
			catch (Exception ex)
			{
				GD.PrintErr($"Error deserializing sequence: {ex.Message}");
			}
		}
	}

	private void HandleValidationResult(Dictionary<string, object> message)
	{
		bool isCorrect = message.ContainsKey("correct") && Convert.ToBoolean(message["correct"]);
		string resultMessage = message.ContainsKey("message") ? message["message"].ToString() : "";

		EmitSignal(SignalName.ValidationResult, isCorrect, resultMessage);
		GD.Print($"Validation result: {isCorrect} - {resultMessage}");
	}

	private void HandleGameEnded(Dictionary<string, object> message)
	{
		bool won = message.ContainsKey("won") && Convert.ToBoolean(message["won"]);
		string reason = message.ContainsKey("reason") ? message["reason"].ToString() : "Game ended";

		EmitSignal(SignalName.GameEnded, won, reason);
		GD.Print($"Game ended: Won={won}, Reason={reason}");
	}

	/// <summary>
	/// Create a new game
	/// </summary>
	public void CreateGame(string playerName)
	{
		// TODO: Adapter le format du message selon votre serveur
		// Vérifier les noms de clés: "player_id", "player_name", etc.
		var message = new
		{
			type = "create_game",
			player_id = playerId,
			player_name = playerName
		};

		SendMessage(message);
	}

	/// <summary>
	/// Join an existing game
	/// </summary>
	public void JoinGame(string gameId, string playerName)
	{
		// TODO: Adapter le format du message selon votre serveur
		// Vérifier les noms de clés: "game_id", "player_id", "player_name", etc.
		var message = new
		{
			type = "join_game",
			game_id = gameId,
			player_id = playerId,
			player_name = playerName
		};

		SendMessage(message);
	}

	/// <summary>
	/// Master sends the sequence
	/// </summary>
	public void SendSequence(int[] sequence)
	{
		// TODO: Adapter le format du message selon votre serveur
		// Vérifier les noms de clés et la structure du tableau
		var message = new
		{
			type = "send_sequence",
			game_id = gameId,
			player_id = playerId,
			sequence = sequence
		};

		SendMessage(message);
		GD.Print($"Sequence sent: {string.Join(",", sequence)}");
	}

	/// <summary>
	/// Player sends their answer
	/// </summary>
	public void SendPlayerAnswer(int[] answer)
	{
		// TODO: Adapter le format du message selon votre serveur
		// Vérifier les noms de clés et la structure du tableau
		var message = new
		{
			type = "player_answer",
			game_id = gameId,
			player_id = playerId,
			answer = answer
		};

		SendMessage(message);
		GD.Print($"Answer sent: {string.Join(",", answer)}");
	}

	/// <summary>
	/// Request start of master turn
	/// </summary>
	public void RequestMasterTurn()
	{
		var message = new
		{
			type = "request_master_turn",
			game_id = gameId,
			player_id = playerId
		};

		SendMessage(message);
	}

	/// <summary>
	/// Request start of player turn
	/// </summary>
	public void RequestPlayerTurn()
	{
		var message = new
		{
			type = "request_player_turn",
			game_id = gameId,
			player_id = playerId
		};

		SendMessage(message);
	}

	/// <summary>
	/// Generic message sender
	/// </summary>
	private void SendMessage(object data)
	{
		if (!isConnected || webSocketPeer == null)
		{
			GD.PrintErr("Not connected to server");
			return;
		}

		string json = JsonSerializer.Serialize(data);
		byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);
		webSocketPeer.SendText(json);
		GD.Print($"Sent: {json}");
	}

	/// <summary>
	/// Disconnect from server
	/// </summary>
	public void Disconnect()
	{
		if (webSocketPeer != null)
		{
			webSocketPeer.Close();
			isConnected = false;
			EmitSignal(SignalName.Disconnected);
		}
	}

	public new bool IsConnected => isConnected;
	public string PlayerId => playerId;
	public string GameId => gameId;
	public string PlayerRole => playerRole;
}
