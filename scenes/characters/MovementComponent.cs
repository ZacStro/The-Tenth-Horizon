using Godot;

public partial class MovementComponent : Node
{
    private GridManager _grid = null;
    private Sprite2D _sprite = null;
    private Vector2 _spriteBasePos = Vector2.Zero;
    private Vector2I _currentGridOffset = new Vector2I(-1, -1);

    public override void _Ready()
    {
        // Find parent CharacterBody2D and its Sprite2D child
        var parent = GetParent() as CharacterBody2D;
        if (parent != null)
        {
            _sprite = parent.GetNodeOrNull<Sprite2D>("Sprite2D");
            if (_sprite != null)
            {
                _spriteBasePos = _sprite.Position;
            }
        }

        // Locate GridManager in the scene tree
        var rootParent = parent?.GetParent()?.GetParent();
        if (rootParent != null)
        {
            _grid = rootParent.GetNodeOrNull<GridManager>("TileMapLayer");
            if (_grid != null)
            {
                // Connect to GridManager's PlayerMoved signal
                _grid.Connect("PlayerMoved", new Callable(this, nameof(OnGridPlayerMoved)));

                // If grid already has a valid PlayerOffset, sync position
                if (_grid.PlayerOffset.X >= 0 && _grid.PlayerOffset.Y >= 0)
                {
                    _currentGridOffset = _grid.PlayerOffset;
                    if (parent != null)
                    {
                        parent.GlobalPosition = _grid.PlayerWorldPosition;
                    }
                }
            }
        }
    }

    private void OnGridPlayerMoved(Variant fromVar, Variant toVar, Variant worldVar)
    {
        if (_grid == null || GetParent() is not CharacterBody2D character)
            return;

        Vector2I from, to;
        Vector2 worldPos;
        try
        {
            from = (Vector2I)fromVar;
            to = (Vector2I)toVar;
            worldPos = (Vector2)worldVar;
        }
        catch
        {
            return;
        }

        // Update current grid offset
        _currentGridOffset = to;

        // Update character position
        character.GlobalPosition = worldPos;

        // Face based on horizontal delta
        int dx = to.X - from.X;
        if (dx != 0)
        {
            FaceHorizontal(dx);
        }
    }

    private void FaceHorizontal(int dx)
    {
        if (_sprite == null)
            return;

        bool faceLeft = dx < 0;
        _sprite.FlipH = faceLeft;

        // Mirror sprite position when flipping to prevent half-grid offset
        if (_spriteBasePos.X != 0)
        {
            float baseX = _spriteBasePos.X;
            _sprite.Position = new Vector2(
                (baseX < 0 ? -baseX : baseX) * (faceLeft ? -1 : 1),
                _spriteBasePos.Y
            );
        }
    }

    // Get the character's current grid offset based on world position
    public Vector2I GetGridOffset()
    {
        if (_grid == null)
            return new Vector2I(-1, -1);

        if (GetParent() is not CharacterBody2D character)
            return new Vector2I(-1, -1);

        // Convert world position back to tile coordinates
        Vector2 localPos = _grid.ToLocal(character.GlobalPosition);
        Vector2I tileOffset = _grid.LocalToMap(localPos);
        return tileOffset;
    }
}
