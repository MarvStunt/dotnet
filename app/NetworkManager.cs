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
	private string serverUrl = "ws://localhost:5000/gamehub";
	private bool isConnected = false;
	private string playerId;
	private string gameId;
	private string playerRole; // Uses Roles.Master or Roles.Player constants
	private string playerName;

	// Dictionary-based handlers for server invocations
	private Dictionary<string, Action<JsonElement>> _invocationHandlers;

	// Signaux
	[Signal]
	public delegate void ConnectedEventHandler();

	[Signal]
	public delegate void DisconnectedEventHandler();

	[Signal]
	public delegate void ConnectionFailedEventHandler();

	[Signal]
	public delegate void GameCreatedEventHandler(string gameCode);

	[Signal]
	public delegate void PlayerJoinedGameEventHandler(bool success);

	[Signal]
	public delegate void GameStartedEventHandler(int sessionId);

	[Signal]
	public delegate void ShowPatternEventHandler(int[] pattern, int roundNumber);

	[Signal]
	public delegate void RoundChangedEventHandler(int roundNumber);

	[Signal]
	public delegate void GameEndedEventHandler(string leaderboardJson);

	[Signal]
	public delegate void PlayerJoinedEventHandler(string playerName);

	[Signal]
	public delegate void PlayerDisconnectedEventHandler(string playerName);

	[Signal]
	public delegate void PlayerSubmittedEventHandler(string playerName, bool isCorrect, int pointsEarned, int totalScore);

	[Signal]
	public delegate void OperationFailedEventHandler(string error, string invocationId);

	[Signal]
	public delegate void PlayerListReceivedEventHandler(string playerListJson);

	public override void _Ready()
	{
		playerId = Guid.NewGuid().ToString();
		SetProcessInternal(true);
		InitializeInvocationHandlers();
	}

	/// <summary>
	/// Initialize the dictionary of handlers for server invocations
	/// </summary>
	private void InitializeInvocationHandlers()
	{
		_invocationHandlers = new Dictionary<string, Action<JsonElement>>
		{
			["GameStarted"] = HandleGameStarted,
			["ShowPattern"] = HandleShowPattern,
			["RoundChanged"] = HandleRoundChanged,
			["GameEnded"] = HandleGameEnded,
			["PlayerJoined"] = HandlePlayerJoined,
			["PlayerDisconnected"] = HandlePlayerDisconnected,
			["PlayerSubmitted"] = HandlePlayerSubmitted,
			["PlayerListReceived"] = HandlePlayerListReceived
		};
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


		if (webSocketPeer != null)
			webSocketPeer.Close();

		webSocketPeer = new WebSocketPeer();
		Error error = webSocketPeer.ConnectToUrl(serverUrl);

		if (error != Error.Ok)
		{
			EmitSignal(SignalName.ConnectionFailed);
			return false;
		}


		// Wait longer for connection to establish
		for (int i = 0; i < 20; i++)
		{
			await Task.Delay(100);
			webSocketPeer.Poll();

			int state = (int)webSocketPeer.GetReadyState();

			if (state == (int)WebSocketPeer.State.Open)
			{
				// Envoyer le handshake SignalR
				string handshake = "{\"protocol\":\"json\",\"version\":1}\u001E";
				webSocketPeer.SendText(handshake);

				isConnected = true;
				EmitSignal(SignalName.Connected);
				return true;
			}
		}

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
			string text = System.Text.Encoding.UTF8.GetString(data);
			string[] messages = text.Split(new[] { '\u001E' }, StringSplitOptions.RemoveEmptyEntries);
			foreach (string json in messages)
			{
				if (string.IsNullOrWhiteSpace(json))
					continue;


				try
				{
					var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
					var message = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, options);

					if (message == null)
						continue;

					int messageType = message.ContainsKey("type") ? message["type"].GetInt32() : 0;

					switch (messageType)
					{
						case 1: // Invocation (from server to client)
							HandleInvocation(message);
							break;
						case 2: // StreamItem
							break;
						case 3: // Completion (response to our invocations)
							HandleCompletion(message);
							break;
						case 6: // Ping
							break;
						default:
							break;
					}
				}
				catch (Exception ex)
				{
					GD.PrintErr($"Error processing message: {ex.Message}");
				}
			}
		}
	}

	private void HandleInvocation(Dictionary<string, JsonElement> message)
	{
		if (!message.ContainsKey("target") || !message.ContainsKey("arguments"))
			return;

		string target = message["target"].GetString();
		var arguments = message["arguments"];

		if (_invocationHandlers.TryGetValue(target, out var handler))
		{
			handler(arguments);
		}
		else
		{
			GD.Print($"[NetworkManager] Unknown invocation target: {target}");
		}
	}

	#region Invocation Handlers

	private void HandleGameStarted(JsonElement arguments)
	{
		if (arguments.GetArrayLength() > 0)
		{
			string sessionId = arguments[0].GetString();
			EmitSignal(SignalName.GameStarted, sessionId);
		}
	}

	private void HandleShowPattern(JsonElement arguments)
	{
		if (arguments.GetArrayLength() > 1)
		{
			var pattern = JsonSerializer.Deserialize<int[]>(arguments[0].GetRawText());
			int roundNumber = arguments[1].GetInt32();
			EmitSignal(SignalName.ShowPattern, pattern, roundNumber);
		}
	}

	private void HandleRoundChanged(JsonElement arguments)
	{
		if (arguments.GetArrayLength() > 0)
		{
			int roundNumber = arguments[0].GetInt32();
			EmitSignal(SignalName.RoundChanged, roundNumber);
		}
	}

	private void HandleGameEnded(JsonElement arguments)
	{
		if (arguments.GetArrayLength() > 0)
		{
			string leaderboard = arguments[0].GetRawText();
			EmitSignal(SignalName.GameEnded, leaderboard);
		}
	}

	private void HandlePlayerJoined(JsonElement arguments)
	{
		if (arguments.GetArrayLength() > 1)
		{
			string playerName = arguments[0].GetString();
			EmitSignal(SignalName.PlayerJoined, playerName);
		}
	}

	private void HandlePlayerDisconnected(JsonElement arguments)
	{
		if (arguments.GetArrayLength() > 0)
		{
			string playerName = arguments[0].GetString();
			EmitSignal(SignalName.PlayerDisconnected, playerName);
		}
	}

	private void HandlePlayerSubmitted(JsonElement arguments)
	{
		if (arguments.GetArrayLength() > 3)
		{
			string playerName = arguments[0].GetString();
			bool isCorrect = arguments[1].GetBoolean();
			int pointsEarned = arguments[2].GetInt32();
			int totalScore = arguments[3].GetInt32();
			EmitSignal(SignalName.PlayerSubmitted, playerName, isCorrect, pointsEarned, totalScore);
		}
	}

	private void HandlePlayerListReceived(JsonElement arguments)
	{
		if (arguments.GetArrayLength() > 0)
		{
			string playerListJson = arguments[0].GetRawText();
			EmitSignal(SignalName.PlayerListReceived, playerListJson);
		}
	}

	#endregion

	private void HandleCompletion(Dictionary<string, JsonElement> message)
	{
		if (!message.ContainsKey("invocationId"))
			return;

		string invocationId = message["invocationId"].GetString();

		// Traiter les erreurs en premier
		if (message.ContainsKey("error"))
		{
			string error = message["error"].GetString();
			EmitSignal(SignalName.OperationFailed, error, invocationId);
			return; // Ne pas continuer si erreur
		}

		if (message.ContainsKey("result"))
		{
			var result = message["result"];

			// Si c'est une réponse à CreateGame
			if (result.ValueKind == JsonValueKind.String)
			{
				string gameCode = result.GetString();
				gameId = gameCode;
				EmitSignal(SignalName.GameCreated, gameCode);
			}
			else if (result.ValueKind == JsonValueKind.True || result.ValueKind == JsonValueKind.False)
			{
				bool success = result.GetBoolean();
				if (success)
				{
					// C'est probablement une réponse à JoinGame
					EmitSignal(SignalName.PlayerJoinedGame, true);
				}
				else
				{
					EmitSignal(SignalName.OperationFailed, "Operation returned false", invocationId);
				}
			}
		}
	}

	public void CreateGame(string playerName)
	{
		var message = new
		{
			type = 1, // Invocation message type
			target = "CreateGame",
			arguments = new object[] { playerName, 4 }, // gameMasterName, gridSize
			invocationId = Guid.NewGuid().ToString()
		};
		

		SendMessage(message);
	}

	/// <summary>
	/// Join an existing game
	/// </summary>
	public void JoinGame(string gameCode, string playerName)
	{
		var message = new
		{
			type = 1,
			target = "JoinGame",
			arguments = new object[] { gameCode, playerName },
			invocationId = Guid.NewGuid().ToString()
		};

		this.gameId = gameCode;
		SendMessage(message);
	}

	/// <summary>
	/// Start the game (Game Master only)
	/// </summary>
	public void StartGame()
	{
		var message = new
		{
			type = 1,
			target = "StartGame",
			arguments = new object[] { gameId }
		};

		SendMessage(message);
	}

	/// <summary>
	/// Start round with a specific sequence (Game Master only)
	/// </summary>
	public void StartRound(List<int> sequence)
	{
		var sequenceJson = JsonSerializer.Serialize(sequence);
		var message = new
		{
			type = 1,
			target = "StartRound",
			arguments = new object[] { gameId, sequenceJson }
		};

		SendMessage(message);
	}

	/// <summary>
	/// Next round (Game Master only)
	/// </summary>
	public void NextRound()
	{
		var message = new
		{
			type = 1,
			target = "NextRound",
			arguments = new object[] { gameId }
		};

		SendMessage(message);
	}

	/// <summary>
	/// Stop the game (Game Master only)
	/// </summary>
	public void StopGame()
	{
		var message = new
		{
			type = 1,
			target = "StopGame",
			arguments = new object[] { gameId }
		};

		SendMessage(message);
	}

	/// <summary>
	/// Player submits their answer
	/// </summary>
	public void SubmitAttempt(int[] attempt, long reactionTimeMs)
	{
		var attemptList = new List<int>(attempt);
		var message = new
		{
			type = 1,
			target = "SubmitAttempt",
			arguments = new object[] { gameId, attemptList, reactionTimeMs }
		};

		SendMessage(message);
	}

	/// <summary>
	/// Get the current leaderboard
	/// </summary>
	public void GetLeaderboard()
	{
		var message = new
		{
			type = 1,
			target = "GetLeaderboard",
			arguments = new object[] { gameId },
			invocationId = Guid.NewGuid().ToString()
		};

		SendMessage(message);
	}

	/// <summary>
	/// Get the current player list with roles
	/// </summary>
	public void GetPlayerList()
	{
		var message = new
		{
			type = 1,
			target = "GetPlayerList",
			arguments = new object[] { gameId },
			invocationId = Guid.NewGuid().ToString()
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
			return;
		}

		string json = JsonSerializer.Serialize(data);
		string signalRMessage = json + "\u001E";
		webSocketPeer.SendText(signalRMessage);
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
	public string PlayerRole
	{
		get => playerRole;
		set => playerRole = value;
	}
	public string PlayerName
	{
		get => playerName;
		set => playerName = value;
	}
}
