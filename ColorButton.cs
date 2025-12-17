using Godot;

public partial class ColorButton : Button
{
	[Export]
	public int ColorIndex = 0;

	public override void _Ready()
	{
		// Connect the button press signal - le signal est géré automatiquement
		// Le script Game.cs se charge de connecter les boutons via ConnectButtons()
		GD.Print($"ColorButton {ColorIndex} ready");
	}
}
