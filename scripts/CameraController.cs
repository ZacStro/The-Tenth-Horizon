using Godot;
using System;

public partial class CameraController : Camera2D
{
	[Export] public float PanSpeed = 400f;
	[Export] public int EdgeMargin = 20;
	[Export] public bool DisableEdgePan = false; // Useful to turn off edge panning (e.g., on mobile)

	// Camera bounds: prevent panning too far from the playable area
	[Export] public Vector2 CameraBoundsMin = new Vector2(-500, -500);
	[Export] public Vector2 CameraBoundsMax = new Vector2(500, 500);
	[Export] public bool EnableCameraBounds = true;

	// Mobile / touch settings
	[Export] public float MinZoom = 0.5f;
	[Export] public float MaxZoom = 2.0f;
	[Export] public float PinchZoomSpeed = 1.0f; // multiplier applied to pinch zooming
	[Export] public float DesktopZoomSpeed = 0.9f; // multiplier per mouse wheel step (smaller -> faster zoom)

	private Vector2 _screenSize;
	// Track active touches: touch index -> last-known screen position
	private readonly System.Collections.Generic.Dictionary<int, Vector2> _touches = new();

	public override void _Ready()
	{
		// Cached window size in pixels
		_screenSize = GetViewport().GetVisibleRect().Size;

		// Auto-disable edge panning on mobile platforms (Android / iOS)
		try
		{
			var osName = OS.GetName();
			if (!string.IsNullOrEmpty(osName))
			{
				if (osName.Equals("Android", StringComparison.OrdinalIgnoreCase) ||
					osName.Equals("iOS", StringComparison.OrdinalIgnoreCase))
				{
					DisableEdgePan = true;
					GD.Print("CameraController: running on mobile (", osName, ") — disabling edge pan.");
				}
			}
		}
		catch (Exception e)
		{
			GD.PrintErr("CameraController: OS detection failed: ", e.Message);
		}
	}

	public override void _Process(double delta)
	{
		// Keep cached screen size up-to-date in case the window was resized.
		_screenSize = GetViewport().GetVisibleRect().Size;
		Vector2 motion = Vector2.Zero;

		// Keyboard pan
		if (Input.IsActionPressed("ui_right")) motion.X += 1;
		if (Input.IsActionPressed("ui_left"))  motion.X -= 1;
		if (Input.IsActionPressed("ui_down"))  motion.Y += 1;
		if (Input.IsActionPressed("ui_up"))    motion.Y -= 1;

		// Mouse-edge pan (only if mouse is within viewport and edge pan not disabled)
		if (!DisableEdgePan && _touches.Count == 0)
		{
			Vector2 mp = GetViewport().GetMousePosition();
			// Check if mouse is within the viewport before checking edge pan
			var viewport = GetViewport().GetVisibleRect();
			if (mp.X >= 0 && mp.X <= viewport.Size.X && mp.Y >= 0 && mp.Y <= viewport.Size.Y)
			{
				if (mp.X <= EdgeMargin)                    motion.X -= 1;
				else if (mp.X >= viewport.Size.X - EdgeMargin) motion.X += 1;
				if (mp.Y <= EdgeMargin)                    motion.Y -= 1;
				else if (mp.Y >= viewport.Size.Y - EdgeMargin) motion.Y += 1;
			}
		}

		if (motion != Vector2.Zero)
		{
			motion = motion.Normalized() * PanSpeed * (float)delta;
			Position += motion;
		}

		// Apply camera bounds to keep grid in view
		if (EnableCameraBounds)
		{
			Position = new Vector2(
				Mathf.Clamp(Position.X, CameraBoundsMin.X, CameraBoundsMax.X),
				Mathf.Clamp(Position.Y, CameraBoundsMin.Y, CameraBoundsMax.Y)
			);
		}
	}

	public override void _Input(InputEvent @event)
	{
		// Desktop: allow mouse wheel zoom (preserve world-space focal point under cursor)
		if (@event is InputEventMouseButton mb && mb.IsPressed())
		{
			if (mb.ButtonIndex == MouseButton.WheelUp || mb.ButtonIndex == MouseButton.WheelDown)
			{
				float zs = (float)Zoom.X;
				Vector2 screenPoint = GetViewport().GetMousePosition();
				Vector2 screenCenter = _screenSize * 0.5f;
				// world coordinate under the mouse before zoom
				Vector2 worldBefore = Position + (screenPoint - screenCenter) * zs;
				float factor = mb.ButtonIndex == MouseButton.WheelUp ? DesktopZoomSpeed : 1.0f / DesktopZoomSpeed;
				float newZoom = Mathf.Clamp(zs * factor, MinZoom, MaxZoom);
				Zoom = new Vector2(newZoom, newZoom);
				// adjust camera so the world point under cursor stays fixed
				Position = worldBefore - (screenPoint - screenCenter) * newZoom;

				// Apply bounds after zoom
				if (EnableCameraBounds)
				{
					Position = new Vector2(
						Mathf.Clamp(Position.X, CameraBoundsMin.X, CameraBoundsMax.X),
						Mathf.Clamp(Position.Y, CameraBoundsMin.Y, CameraBoundsMax.Y)
					);
				}
				return;
			}
		}

		// Handle basic touch events for mobile: single-finger drag to pan, two-finger pinch to zoom
		if (@event is InputEventScreenTouch touch)
		{
			if (touch.Pressed)
			{
				_touches[touch.Index] = touch.Position;
			}
			else
			{
				_touches.Remove(touch.Index);
			}
		}
		else if (@event is InputEventScreenDrag drag)
		{
			// Previous position for this touch
			Vector2 prevPos = _touches.ContainsKey(drag.Index) ? _touches[drag.Index] : drag.Position - drag.Relative;
			Vector2 currPos = drag.Position;
			Vector2 delta = currPos - prevPos;

			// Update storage immediately
			_touches[drag.Index] = currPos;

			if (_touches.Count == 1)
			{
				// Single-finger gestures are reserved for game interactions (tapping/dragging on tiles).
				// Camera panning requires two fingers — do nothing here so other nodes receive the touch.
			}
			else if (_touches.Count >= 2)
			{
				// Two-finger gestures: pinch to zoom and two-finger pan (midpoint movement)
				var ids = new System.Collections.Generic.List<int>(_touches.Keys);
				int a = ids[0], b = ids[1];
				Vector2 prevA = a == drag.Index ? prevPos : _touches[a];
				Vector2 prevB = b == drag.Index ? prevPos : _touches[b];
				Vector2 currA = a == drag.Index ? currPos : _touches[a];
				Vector2 currB = b == drag.Index ? currPos : _touches[b];

				float prevDist = (prevA - prevB).Length();
				float currDist = (currA - currB).Length();

				// Midpoint movement for two-finger pan
				Vector2 prevMid = (prevA + prevB) * 0.5f;
				Vector2 currMid = (currA + currB) * 0.5f;

				// Pan by the screen midpoint delta (keep direction consistent with single-finger drag)
				float zs = (float)Zoom.X;
				Position -= (currMid - prevMid) * zs;

				// Pinch zoom: if both touches moved, change zoom proportionally and preserve the
				// world-space point under the pinch midpoint.
				if (prevDist > 0 && currDist > 0)
				{
					// compute world point under the screen midpoint before zoom
					Vector2 screenMid = currMid;
					Vector2 screenCenter = _screenSize * 0.5f;
					Vector2 worldBefore = Position + (screenMid - screenCenter) * zs;

					// Use currDist/prevDist so moving fingers apart (curr>prev) zooms in (>1)
					float pinchFactor = (currDist / prevDist) * PinchZoomSpeed;
					float newZoom = Mathf.Clamp(zs * pinchFactor, MinZoom, MaxZoom);
					Zoom = new Vector2(newZoom, newZoom);

					// Adjust camera position so the world point under the midpoint remains fixed.
					Position = worldBefore - (screenMid - screenCenter) * newZoom;
				}

				// Apply bounds after pan/zoom
				if (EnableCameraBounds)
				{
					Position = new Vector2(
						Mathf.Clamp(Position.X, CameraBoundsMin.X, CameraBoundsMax.X),
						Mathf.Clamp(Position.Y, CameraBoundsMin.Y, CameraBoundsMax.Y)
					);
				}
			}
		}
	}

}
