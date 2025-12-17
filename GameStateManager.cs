using Godot;
using System;

/// <summary>
/// Enums for game state management
/// </summary>
public enum GameState
{
	Waiting,           // Waiting for game to start
	Setup,             // Game setup
	MasterTurn,        // Master creating sequence
	PlayingMaster,     // Master playing sequence
	PlayerTurn,        // Player responding
	Validating,        // Server validating
	RoundEnded,        // Round complete
	GameWon,           // All rounds won
	GameLost           // Player failed
}

public enum PlayerRole
{
	None,
	Master,
	Player
}

/// <summary>
/// Manages the game state and transitions
/// </summary>
public partial class GameStateManager : Node
{
	private GameState currentState = GameState.Waiting;
	private PlayerRole playerRole = PlayerRole.None;
	private NetworkManager networkManager;

	[Signal]
	public delegate void StateChangedEventHandler(int newState);

	[Signal]
	public delegate void RoleAssignedEventHandler(int role);

	public override void _Ready()
	{
		networkManager = GetNode<NetworkManager>("/root/NetworkManager");
	}

	public void SetRole(PlayerRole role)
	{
		playerRole = role;
		EmitSignal(SignalName.RoleAssigned, (int)role);
		GD.Print($"Role assigned: {role}");
	}

	public void ChangeState(GameState newState)
	{
		if (currentState == newState)
			return;

		currentState = newState;
		EmitSignal(SignalName.StateChanged, (int)newState);
		GD.Print($"State changed to: {newState}");
	}

	public GameState GetCurrentState => currentState;
	public PlayerRole GetPlayerRole => playerRole;
	public bool IsMaster => playerRole == PlayerRole.Master;
	public bool IsPlayer => playerRole == PlayerRole.Player;
}
