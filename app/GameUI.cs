using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Manages all UI elements and display logic for the Game scene.
/// Provides helper methods for UI updates and state management.
/// </summary>
public class GameUI
{
    #region UI Elements
    // Sections
    public Control MasterSection { get; private set; }
    public Control PlayerSection { get; private set; }
    
    // Labels
    public Label SequenceLabel { get; private set; }
    public Label FeedbackLabel { get; private set; }
    public Label LabelInfo { get; private set; }
    public Label RoleLabel { get; private set; }
    public Label GameInfoLabel { get; private set; }
    public Label PlayersCountLabel { get; private set; }
    public Label RoundInfoLabel { get; private set; }
    public Label PlayerNameLabel { get; private set; }
    
    // Buttons
    public Button SendSequenceButton { get; private set; }
    public Button NextRoundButton { get; private set; }
    public Button DisconnectButton { get; private set; }
    public Button CopyGameIdButton { get; private set; }
    public Button StartGameButton { get; private set; }
    public Button EndGameButton { get; private set; }
    
    // Containers
    public VBoxContainer PlayersList { get; private set; }
    public Godot.Collections.Array<Node> ColorButtons { get; private set; }
    #endregion

    private readonly Control _root;

    /// <summary>
    /// Initialize the GameUI with the root control node
    /// </summary>
    public GameUI(Control root)
    {
        _root = root;
        InitializeNodes();
    }

    /// <summary>
    /// Get all UI nodes from the scene tree using UINodePaths constants
    /// </summary>
    private void InitializeNodes()
    {
        // Sections
        MasterSection = _root.GetNode<Control>(UINodePaths.MasterSection);
        PlayerSection = _root.GetNode<Control>(UINodePaths.PlayerSection);
        
        // Labels
        SequenceLabel = _root.GetNode<Label>(UINodePaths.SequenceLabel);
        FeedbackLabel = _root.GetNode<Label>(UINodePaths.FeedbackLabel);
        LabelInfo = _root.GetNode<Label>(UINodePaths.GameStatus);
        RoleLabel = _root.GetNode<Label>(UINodePaths.RoleLabel);
        GameInfoLabel = _root.GetNode<Label>(UINodePaths.GameInfo);
        PlayersCountLabel = _root.GetNode<Label>(UINodePaths.PlayersCount);
        RoundInfoLabel = _root.GetNode<Label>(UINodePaths.RoundInfo);
        PlayerNameLabel = _root.GetNode<Label>(UINodePaths.PlayerName);
        
        // Buttons
        SendSequenceButton = _root.GetNode<Button>(UINodePaths.SendSequenceButton);
        NextRoundButton = _root.GetNode<Button>(UINodePaths.NextRoundButton);
        DisconnectButton = _root.GetNode<Button>(UINodePaths.DisconnectButton);
        CopyGameIdButton = _root.GetNode<Button>(UINodePaths.CopyGameIdButton);
        StartGameButton = _root.GetNode<Button>(UINodePaths.StartGameButton);
        EndGameButton = _root.GetNode<Button>(UINodePaths.EndGameButton);
        
        // Containers
        PlayersList = _root.GetNode<VBoxContainer>(UINodePaths.PlayersList);
        
        // Color buttons
        var colorButtonNodes = _root.GetNode(UINodePaths.ColorButtonsGrid).GetChildren();
        ColorButtons = new Godot.Collections.Array<Node>();
        foreach (Node btn in colorButtonNodes)
            ColorButtons.Add(btn);
    }

    #region Label Helper Methods
    /// <summary>
    /// Safely set text on a label (null-safe)
    /// </summary>
    public void SetLabelText(Label label, string text)
    {
        if (label != null) 
            label.Text = text;
    }

    public void SetInfoText(string text) => SetLabelText(LabelInfo, text);
    public void SetRoleText(string text) => SetLabelText(RoleLabel, text);
    public void SetGameInfoText(string text) => SetLabelText(GameInfoLabel, text);
    public void SetPlayersCountText(int count) => SetLabelText(PlayersCountLabel, $"Players: {count}");
    public void SetRoundInfoText(int round) => SetLabelText(RoundInfoLabel, $"Round: {round}");
    public void SetPlayerNameText(string name) => SetLabelText(PlayerNameLabel, $"Player: {name}");
    public void SetSequenceText(string text) => SetLabelText(SequenceLabel, text);
    public void SetFeedbackText(string text) => SetLabelText(FeedbackLabel, text);
    #endregion

    #region Button Helper Methods
    /// <summary>
    /// Safely set button disabled state (null-safe)
    /// </summary>
    public void SetButtonDisabled(Button button, bool disabled)
    {
        if (button != null) 
            button.Disabled = disabled;
    }

    public void SetSendSequenceDisabled(bool disabled) => SetButtonDisabled(SendSequenceButton, disabled);
    public void SetNextRoundDisabled(bool disabled) => SetButtonDisabled(NextRoundButton, disabled);
    public void SetStartGameDisabled(bool disabled) => SetButtonDisabled(StartGameButton, disabled);
    public void SetEndGameDisabled(bool disabled) => SetButtonDisabled(EndGameButton, disabled);
    #endregion

    #region Visibility Helper Methods
    /// <summary>
    /// Safely set control visibility (null-safe)
    /// </summary>
    public void SetVisible(Control control, bool visible)
    {
        if (control != null) 
            control.Visible = visible;
    }

    public void SetMasterSectionVisible(bool visible) => SetVisible(MasterSection, visible);
    public void SetPlayerSectionVisible(bool visible) => SetVisible(PlayerSection, visible);
    public void SetFeedbackLabelVisible(bool visible) => SetVisible(FeedbackLabel, visible);
    public void SetSequenceLabelVisible(bool visible) => SetVisible(SequenceLabel, visible);
    #endregion

    #region Color Button Methods
    /// <summary>
    /// Disable all color buttons
    /// </summary>
    public void DisableAllColorButtons()
    {
        foreach (Node btn in ColorButtons)
        {
            if (btn is ColorButton colorBtn)
                colorBtn.Disabled = true;
        }
    }

    /// <summary>
    /// Enable all color buttons
    /// </summary>
    public void EnableAllColorButtons()
    {
        foreach (Node btn in ColorButtons)
        {
            if (btn is ColorButton colorBtn)
                colorBtn.Disabled = false;
        }
    }

    /// <summary>
    /// Get a color button by index
    /// </summary>
    public ColorButton GetColorButton(int index)
    {
        if (index >= 0 && index < ColorButtons.Count)
            return ColorButtons[index] as ColorButton;
        return null;
    }
    #endregion

    #region UI Setup Methods
    /// <summary>
    /// Setup the UI for a network game
    /// </summary>
    public void SetupForNetworkGame(bool isMaster, string gameId, string playerName, int connectedPlayers, int roundNumber)
    {
        SetRoleText(isMaster ? "👑 MASTER" : "🎮 PLAYER");
        SetGameInfoText($"Game: {gameId}");
        SetPlayerNameText(playerName);
        SetPlayersCountText(connectedPlayers);
        SetRoundInfoText(roundNumber);
        
        SetMasterSectionVisible(isMaster);
        SetPlayerSectionVisible(!isMaster);
        
        if (isMaster)
        {
            SetInfoText("Waiting for players to join... ⏳");
            SetFeedbackLabelVisible(false);
        }
        else
        {
            SetInfoText("Waiting for master...");
            SetSequenceLabelVisible(false);
        }
    }

    /// <summary>
    /// Disable all game-related buttons
    /// </summary>
    public void DisableAllGameButtons()
    {
        DisableAllColorButtons();
        SetSendSequenceDisabled(true);
        SetStartGameDisabled(true);
    }

    /// <summary>
    /// Enable game buttons for play
    /// </summary>
    public void EnableGameButtons()
    {
        EnableAllColorButtons();
    }
    #endregion
}
