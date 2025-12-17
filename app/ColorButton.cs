using Godot;

public partial class ColorButton : Button
{
	[Export]
	public int ColorIndex = 0;

	public override void _Ready()
	{
		GD.Print($"ColorButton {ColorIndex} ready");
	}
}
