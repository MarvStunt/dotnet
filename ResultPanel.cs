using Godot;
using System;

/// <summary>
/// Result Panel - Displays game end results
/// </summary>
public partial class ResultPanel : Panel
{
	private Label titleLabel;
	private Label messageLabel;
	private Button retryButton;
	private Button backToHubButton;

	public override void _Ready()
	{
		// Get child nodes
		titleLabel = GetNode<Label>("VBoxContainer/TitleLabel");
		messageLabel = GetNode<Label>("VBoxContainer/MessageLabel");
		retryButton = GetNode<Button>("VBoxContainer/Buttons/RetryButton");
		backToHubButton = GetNode<Button>("VBoxContainer/Buttons/BackToHubButton");

		// Connect button signals
		retryButton.Pressed += OnRetryPressed;
		backToHubButton.Pressed += OnBackToHubPressed;

		// Hide by default
		Visible = false;
	}

	/// <summary>
	/// Show win result
	/// </summary>
	public void ShowWon(string reason = "Bravo! Vous avez gagné!")
	{
		titleLabel.Text = "Vous avez gagné!";
		messageLabel.Text = reason;
		Visible = true;
	}

	/// <summary>
	/// Show loss result
	/// </summary>
	public void ShowLost(string reason = "Dommage, vous avez perdu!")
	{
		titleLabel.Text = "Vous avez perdu!";
		messageLabel.Text = reason;
		Visible = true;
	}

	private void OnRetryPressed()
	{
		GD.Print("Retry pressed");
		Visible = false;
		// TODO: Implement retry logic
	}

	private void OnBackToHubPressed()
	{
		GD.Print("Back to hub pressed");
		Visible = false;
		// TODO: Implement back to hub logic
	}
}
