using Godot;

public partial class CameraController : Camera2D
{
	[Export] public float PanSpeed = 400f;
	[Export] public int EdgeMargin = 20;

	private Vector2 _screenSize;

	public override void _Ready()
	{
		// Cached window size in pixels
		_screenSize = GetViewport().GetVisibleRect().Size;
	}

	public override void _Process(double delta)
	{
		Vector2 motion = Vector2.Zero;

		// Keyboard pan
		if (Input.IsActionPressed("ui_right")) motion.X += 1;
		if (Input.IsActionPressed("ui_left"))  motion.X -= 1;
		if (Input.IsActionPressed("ui_down"))  motion.Y += 1;
		if (Input.IsActionPressed("ui_up"))    motion.Y -= 1;

		// Mouse‐edge pan
		Vector2 mp = GetViewport().GetMousePosition();
		if (mp.X <= EdgeMargin)                    motion.X -= 1;
		else if (mp.X >= _screenSize.X - EdgeMargin) motion.X += 1;
		if (mp.Y <= EdgeMargin)                    motion.Y -= 1;
		else if (mp.Y >= _screenSize.Y - EdgeMargin) motion.Y += 1;

		if (motion != Vector2.Zero)
		{
			motion = motion.Normalized() * PanSpeed * (float)delta;
			Position += motion;
		}
	}

}
