using Godot;
using System;

public enum GameState
{
	Waiting,
	BuildingSequence,
	ShowingPattern,
	PlayerTurn,
	Validating,
	RoundComplete,
	GameEnded
}

public static class Roles
{
	public const string None = "";
	public const string Master = "master";
	public const string Player = "player";

	public static bool IsMaster(string role) => role == Master;

	public static bool IsPlayer(string role) => role == Player;
}

public partial class GameStateManager : Node
{
	public static GameStateManager Instance { get; private set; }

	private GameState _currentState = GameState.Waiting;
	private string _playerRole = Roles.None;
	private bool _gameStarted = false;
	private int _roundNumber = 0;

	[Signal]
	public delegate void StateChangedEventHandler(GameState newState);

	[Signal]
	public delegate void RoleAssignedEventHandler(string role);

	[Signal]
	public delegate void GameStartedSignalEventHandler();

	[Signal]
	public delegate void RoundChangedEventHandler(int roundNumber);

	public override void _Ready()
	{
		Instance = this;
	}

	public override void _ExitTree()
	{
		if (Instance == this)
			Instance = null;
	}

	public GameState CurrentState => _currentState;
	public string PlayerRole => _playerRole;
	public bool IsMaster => _playerRole == Roles.Master;
	public bool IsPlayer => _playerRole == Roles.Player;
	public bool GameStarted => _gameStarted;
	public int RoundNumber => _roundNumber;

	public bool CanPlayerInput => _currentState == GameState.PlayerTurn ||
								  (_currentState == GameState.BuildingSequence && IsMaster);

	public void SetRole(string role)
	{
		_playerRole = role;
		EmitSignal(SignalName.RoleAssigned, role);
	}

	public void ChangeState(GameState newState)
	{
		if (_currentState == newState)
			return;

		var oldState = _currentState;
		_currentState = newState;
		EmitSignal(SignalName.StateChanged, (int)newState);
	}

	public void StartGame()
	{
		if (_gameStarted)
			return;

		_gameStarted = true;
		_roundNumber = 0;
		ChangeState(GameState.BuildingSequence);
		EmitSignal(SignalName.GameStartedSignal);
	}

	public void SetRound(int roundNumber)
	{
		_roundNumber = roundNumber;
		EmitSignal(SignalName.RoundChanged, roundNumber);
	}

	public void EndGame()
	{
		_gameStarted = false;
		ChangeState(GameState.GameEnded);
	}

	public void Reset()
	{
		_currentState = GameState.Waiting;
		_playerRole = Roles.None;
		_gameStarted = false;
		_roundNumber = 0;
	}

	public bool IsInState(GameState state) => _currentState == state;

	public bool IsWaiting => _currentState == GameState.Waiting;
	public bool IsBuildingSequence => _currentState == GameState.BuildingSequence;
	public bool IsShowingPattern => _currentState == GameState.ShowingPattern;
	public bool IsPlayerTurn => _currentState == GameState.PlayerTurn;
	public bool IsValidating => _currentState == GameState.Validating;
	public bool IsRoundComplete => _currentState == GameState.RoundComplete;
	public bool IsGameEnded => _currentState == GameState.GameEnded;
}
