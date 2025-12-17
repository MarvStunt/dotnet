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
    private string playerRole; // "master" or "player"
    private string playerName;

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
    public delegate void GameStartedEventHandler(int sessionId);

    [Signal]
    public delegate void ShowPatternEventHandler(int[] pattern, int roundNumber);

    [Signal]
    public delegate void RoundChangedEventHandler(int roundNumber);

    [Signal]
    public delegate void GameEndedEventHandler(string leaderboardJson);

    [Signal]
    public delegate void PlayerJoinedEventHandler(string playerName, int playerId);

    [Signal]
    public delegate void PlayerDisconnectedEventHandler(string playerName);

    [Signal]
    public delegate void PlayerSubmittedEventHandler(string playerName, bool isCorrect, int pointsEarned, int totalScore);

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
            GD.Print($"   Attempt {i + 1}/20 - State: {state} (Connecting=0, Open=1, Closing=2, Closed=3)");

            if (state == (int)WebSocketPeer.State.Open)
            {
                // Envoyer le handshake SignalR
                string handshake = "{\"protocol\":\"json\",\"version\":1}\u001E";
                webSocketPeer.SendText(handshake);
                GD.Print("🤝 SignalR handshake sent");

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
            string text = System.Text.Encoding.UTF8.GetString(data);
            string[] messages = text.Split(new[] { '\u001E' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string json in messages)
            {
                if (string.IsNullOrWhiteSpace(json))
                    continue;

                GD.Print($"Received: {json}");

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
                            GD.Print($"Unknown SignalR message type: {messageType}");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"Error parsing message: {ex.Message}\n{json}");
                }
            }
        }
    }

    private void HandleInvocation(Dictionary<string, JsonElement> message)
    {
        Console.WriteLine("Handling invocation message");
        Console.WriteLine(JsonSerializer.Serialize(message));
        Console.WriteLine("-----");

        if (!message.ContainsKey("target") || !message.ContainsKey("arguments"))
            return;

        string target = message["target"].GetString();
        var arguments = message["arguments"];

        switch (target)
        {
            case "GameStarted":
                if (arguments.GetArrayLength() > 0)
                {
                    int sessionId = arguments[0].GetInt32();
                    EmitSignal(SignalName.GameStarted, sessionId);
                    GD.Print($"Game started: {sessionId}");
                }
                break;

            case "ShowPattern":
                if (arguments.GetArrayLength() > 1)
                {
                    var pattern = JsonSerializer.Deserialize<int[]>(arguments[0].GetRawText());
                    int roundNumber = arguments[1].GetInt32();
                    EmitSignal(SignalName.ShowPattern, pattern, roundNumber);
                    GD.Print($"Pattern received - Round {roundNumber}: {string.Join(",", pattern)}");
                }
                break;

            case "RoundChanged":
                if (arguments.GetArrayLength() > 0)
                {
                    int roundNumber = arguments[0].GetInt32();
                    EmitSignal(SignalName.RoundChanged, roundNumber);
                    GD.Print($"Round changed to: {roundNumber}");
                }
                break;

            case "GameEnded":
                if (arguments.GetArrayLength() > 0)
                {
                    string leaderboard = arguments[0].GetRawText();
                    EmitSignal(SignalName.GameEnded, leaderboard);
                    GD.Print($"Game ended - Leaderboard: {leaderboard}");
                }
                break;

            case "PlayerJoined":
                if (arguments.GetArrayLength() > 1)
                {
                    string playerName = arguments[0].GetString();
                    int playerId = arguments[1].GetInt32();
                    EmitSignal(SignalName.PlayerJoined, playerName, playerId);
                    GD.Print($"Player joined: {playerName} (ID: {playerId})");
                }
                break;

            case "PlayerDisconnected":
                if (arguments.GetArrayLength() > 0)
                {
                    string playerName = arguments[0].GetString();
                    EmitSignal(SignalName.PlayerDisconnected, playerName);
                    GD.Print($"Player disconnected: {playerName}");
                }
                break;

            case "PlayerSubmitted":
                if (arguments.GetArrayLength() > 3)
                {
                    string playerName = arguments[0].GetString();
                    bool isCorrect = arguments[1].GetBoolean();
                    int pointsEarned = arguments[2].GetInt32();
                    int totalScore = arguments[3].GetInt32();
                    EmitSignal(SignalName.PlayerSubmitted, playerName, isCorrect, pointsEarned, totalScore);
                    GD.Print($"{playerName} submitted: {(isCorrect ? "✓" : "✗")} (+{pointsEarned}pts, total: {totalScore})");
                }
                break;

            default:
                GD.Print($"Unknown invocation target: {target}");
                break;
        }
    }

    private void HandleCompletion(Dictionary<string, JsonElement> message)
    {
        Console.WriteLine("Handling completion message");
        Console.WriteLine(JsonSerializer.Serialize(message));
        Console.WriteLine("-----");
        // Gère les réponses aux méthodes invoquées (CreateGame, JoinGame, etc.)
        if (!message.ContainsKey("invocationId"))
            return;

        string invocationId = message["invocationId"].GetString();

        if (message.ContainsKey("result"))
        {
            var result = message["result"];

            // Si c'est une réponse à CreateGame
            if (result.ValueKind == JsonValueKind.String)
            {
                string gameCode = result.GetString();
                gameId = gameCode;
                EmitSignal(SignalName.GameCreated, gameCode);
                GD.Print($"✅ Game created with code: {gameCode}");
            }
            else if (result.ValueKind == JsonValueKind.True || result.ValueKind == JsonValueKind.False)
            {
                bool success = result.GetBoolean();
                GD.Print($"Operation result: {success}");
            }
        }
        else if (message.ContainsKey("error"))
        {
            string error = message["error"].GetString();
            GD.PrintErr($"Server error: {error}");
        }
    }

    public void CreateGame(string playerName)
    {
        GD.Print($"Creating game as {playerName}");
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
        GD.Print($"Joining game {gameCode} as {playerName}");
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
        GD.Print($"Starting game {gameId}");
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
        GD.Print($"Next round requested");
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
        GD.Print($"Stopping game {gameId}");
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
        GD.Print($"Answer submitted: {string.Join(",", attempt)} (time: {reactionTimeMs}ms)");
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
        GD.Print($"Requesting leaderboard");
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
        string signalRMessage = json + "\u001E";
        webSocketPeer.SendText(signalRMessage);
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
