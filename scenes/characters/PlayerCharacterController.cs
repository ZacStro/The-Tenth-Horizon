using Godot;
using System.Collections.Generic;

public partial class PlayerCharacterController : CharacterBody2D
{
    // Minimal, Player controller.
    // Handles UI interaction (click detection).
    // Movement logic delegated to MovementComponent child.

    [Signal]
    public delegate void CharacterClickedEventHandler();

    private GridManager _grid;
    private MovementComponent _movementComponent = null;
    private Vector2 _touchStartPos;
    private double _touchStartTime;
    private bool _touchDragging;
    private const float DragThreshold = 10f;      // pixels
    private const double TapTimeThreshold = 0.25; // seconds

    public override void _Ready()
    {
        // Locate the GridManager in the scene
        var rootParent = GetParent()?.GetParent();
        if (rootParent != null)
        {
            _grid = rootParent.GetNodeOrNull<GridManager>("TileMapLayer");
        }

        // Find the MovementComponent child
        _movementComponent = GetNodeOrNull<MovementComponent>("MovementComponent");
    }

    public override void _Input(InputEvent @event)
    {
        if(GridManager.IsMobile())
        {
              // Mobile: touch to show menu
            if (@event is InputEventScreenTouch touch)
            {
                if (touch.Pressed)
                {
                    _touchStartPos = touch.Position;
                    _touchStartTime = Time.GetTicksMsec() / 1000.0;
                    _touchDragging = false;
                }
                else // Touch released
                {
                    var touchDuration = (Time.GetTicksMsec() / 1000.0) - _touchStartTime;
                    var touchEndPos = touch.Position;
                    var dragDistance = touchEndPos.DistanceTo(_touchStartPos);

                    if (!_grid.IsMoving && !_touchDragging && touchDuration <= TapTimeThreshold && dragDistance < DragThreshold)
                    {
                        EmitSignal(nameof(CharacterClicked));
                        // GetTree().Root.SetInputAsHandled();
                    }
                }
                // var touchPos = touch.Position;
                // var distToTouch = GlobalPosition.DistanceTo(touchPos);
                // // Same radius as mouse clicks
                // if (distToTouch < 50)
                // {
                //     EmitSignal(nameof(CharacterClicked));
                //     GetTree().Root.SetInputAsHandled();
                // }
            }
            else if (@event is InputEventScreenDrag drag)
            {
                // mark as drag so release won't count as tap
                var distance = _touchStartPos.DistanceTo(drag.Position);
                if (distance > DragThreshold)
                    _touchDragging = true;
            }
        }
        else
        {
          // Desktop: mouse click to show menu
            if (@event is InputEventMouseButton mb && mb.IsPressed() && mb.ButtonIndex == MouseButton.Left)
            {
                var mousePos = GetGlobalMousePosition();
                var distToMouse = GlobalPosition.DistanceTo(mousePos);
                // Simple click detection: if mouse is within ~50 pixels of character center, count as a click
                if (distToMouse < 50)
                {
                    EmitSignal(nameof(CharacterClicked));
                    // GetTree().Root.SetInputAsHandled();
                }
            }
        }
       
    }

    // Called by TurnManager when this character's turn starts
    public void OnTurnStart()
    {
        // Hook for UI or turn logic. Keep empty for now.
    }

    // Called to move the character to a new tile offset. Implementation can be
    // provided later to animate the character along a path. Keep signature using
    // Vector2I to match the rest of the project.
    public void MoveTo(Vector2I target)
    {
        // Intentionally minimal: higher-level code will call into this to animate.
    }

    // Get the character's current grid offset (delegates to MovementComponent)
    public Vector2I GetGridOffset()
    {
        if (_movementComponent != null)
            return _movementComponent.GetGridOffset();
        return new Vector2I(-1, -1);
    }

    // Turn state
    public bool HasEndedTurn { get; set; } = false;
}
