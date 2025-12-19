using Godot;

public static class UINodePaths
{
	#region Main Sections
	public const string MainVBox = "MainVBox";
	public const string Header = MainVBox + "/Header";
	public const string Content = MainVBox + "/Content";
	#endregion

	#region Game Panel
	public const string GamePanel = Content + "/GamePanel/GameMargin/GameVBox";
	public const string ControlsSection = GamePanel + "/ControlsSection";
	public const string MasterSection = ControlsSection + "/MasterSection";
	public const string PlayerSection = ControlsSection + "/PlayerSection";
	#endregion

	#region Labels
	public const string SequenceLabel = ControlsSection + "/SequenceLabel";
	public const string FeedbackLabel = ControlsSection + "/FeedbackLabel";
	public const string GameStatus = GamePanel + "/GameStatus";
	#endregion

	#region Header Labels
	public const string StatusSection = Header + "/StatusSection";
	public const string TitleBox = Header + "/TitleBox";
	public const string RoleLabel = StatusSection + "/RoleLabel";
	public const string GameInfo = TitleBox + "/GameInfo";
	public const string PlayersCount = StatusSection + "/PlayersCount";
	public const string RoundInfo = StatusSection + "/RoundInfo";
	public const string PlayerName = StatusSection + "/PlayerName";
	#endregion

	#region Master Controls
	public const string MasterControlsHBox = MasterSection + "/MasterControlsHBox";
	public const string SendSequenceButton = MasterControlsHBox + "/SendSequenceButton";
	public const string NextRoundButton = MasterControlsHBox + "/NextRoundButton";
	public const string StartGameButton = MasterControlsHBox + "/StartGameButton";
	public const string EndGameButton = MasterControlsHBox + "/EndGameButton";
	#endregion

	#region Header Buttons
	public const string ActionButtons = Header + "/ActionButtons";
	public const string DisconnectButton = ActionButtons + "/DisconnectButton";
	public const string CopyGameIdButton = ActionButtons + "/CopyGameIdButton";
	#endregion

	#region Containers
	public const string PlayersPanel = Content + "/PlayersPanel/PlayersMargin/PlayersVBox";
	public const string PlayersList = PlayersPanel + "/PlayersList";
	public const string ColorButtonsGrid = ControlsSection + "/SequenceButtonsGrid";
	#endregion
}
