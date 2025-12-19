using Godot;

public partial class ColorButton : Button
{
	[Export]
	public int ColorIndex = 0;

	[Export]
	public Color ButtonColor = new Color(0.5f, 0.5f, 0.5f, 1);

	public override void _Ready()
	{
		// Hide text
		Text = "";

		// Set button colors using theme overrides
		var styleBox = new StyleBoxFlat();
		styleBox.BgColor = ButtonColor;
		styleBox.SetCornerRadiusAll(8);
		AddThemeStyleboxOverride("normal", styleBox);
		
		var styleBoxHover = new StyleBoxFlat();
		styleBoxHover.BgColor = ButtonColor.Lightened(0.15f);
		styleBoxHover.SetCornerRadiusAll(8);
		AddThemeStyleboxOverride("hover", styleBoxHover);
		
		var styleBoxPressed = new StyleBoxFlat();
		styleBoxPressed.BgColor = ButtonColor.Darkened(0.2f);
		styleBoxPressed.SetCornerRadiusAll(8);
		AddThemeStyleboxOverride("pressed", styleBoxPressed);
		
		var styleBoxDisabled = new StyleBoxFlat();
		styleBoxDisabled.BgColor = ButtonColor.Darkened(0.4f);
		styleBoxDisabled.SetCornerRadiusAll(8);
		AddThemeStyleboxOverride("disabled", styleBoxDisabled);
	}
}
