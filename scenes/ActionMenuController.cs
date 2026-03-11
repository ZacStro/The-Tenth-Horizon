using Godot;
using System;
using System.Collections.Generic;

public partial class ActionMenuController : Control
{
    private PanelContainer _panelContainer = null;
    private VBoxContainer _menuContainer = null;
    private List<Button> _menuButtons = new List<Button>();
    private PlayerCharacterController _currentCharacter = null;
    private GridManager _grid = null;
    private TurnManager _turnManager = null;
    private List<PlayerCharacterController> _playerCharacters = new List<PlayerCharacterController>();

    public override void _Ready()
    {
        // Hide initially
        Visible = false;
        ZIndex = 1000; // Ensure menu appears on top

        // Search up the tree for GridManager
        Node node = this;
        while (node != null && _grid == null)
        {
            _grid = node.GetNodeOrNull<GridManager>("TileMapLayer");
            node = node.GetParent();
        }

        // Search for TurnManager from GridManager's parent
        if (_grid != null)
        {
            node = _grid.GetParent();
            while (node != null && _turnManager == null)
            {
                _turnManager = node.GetNodeOrNull<TurnManager>("TurnManager");
                node = node.GetParent();
            }

            // Auto-discover all player characters under Characters/
            var gameRoot = _grid.GetParent();
            var charactersNode = gameRoot?.GetNodeOrNull<Node>("Characters");
            if (charactersNode != null)
            {
                foreach (Node child in charactersNode.GetChildren())
                {
                    var playerChar = child as PlayerCharacterController;
                    if (playerChar != null)
                    {
                        _playerCharacters.Add(playerChar);
                    }
                }
            }
        }

        // Connect to all player characters
        foreach (var playerChar in _playerCharacters)
        {
            playerChar.Connect("CharacterClicked", Callable.From(() => OnCharacterClickedChar(playerChar)));
        }

        // Create menu UI with PanelContainer as the visible element
        var bgStyleBox = new StyleBoxFlat();
        bgStyleBox.BgColor = new Color(0.1f, 0.1f, 0.1f, 0.95f);
        bgStyleBox.SetContentMarginAll(8f);

        _panelContainer = new PanelContainer();
        _panelContainer.AddThemeStyleboxOverride("panel", bgStyleBox);
        AddChild(_panelContainer);

        _menuContainer = new VBoxContainer();
        _menuContainer.CustomMinimumSize = new Vector2(120, 0);
        _panelContainer.AddChild(_menuContainer);

        // Create button style
        var buttonStyle = new StyleBoxFlat();
        buttonStyle.BgColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        buttonStyle.SetContentMarginAll(6f);

        // Create Move button
        var moveButton = new Button();
        moveButton.Text = "Move";
        moveButton.CustomMinimumSize = new Vector2(100, 32);
        moveButton.AddThemeStyleboxOverride("normal", buttonStyle);
        moveButton.Pressed += OnMovePressed;
        _menuContainer.AddChild(moveButton);
        _menuButtons.Add(moveButton);

        // Create Wait button
        var waitButton = new Button();
        waitButton.Text = "Wait";
        waitButton.CustomMinimumSize = new Vector2(100, 32);
        waitButton.AddThemeStyleboxOverride("normal", buttonStyle);
        waitButton.Pressed += OnWaitPressed;
        _menuContainer.AddChild(waitButton);
        _menuButtons.Add(waitButton);
    }

    private void OnCharacterClickedChar(PlayerCharacterController character)
    {
        // Only show menu if this character hasn't ended their turn
        if (character != null && !character.HasEndedTurn)
        {
            ShowMenu(character);
        }
    }

    public void ShowMenu(PlayerCharacterController character)
    {
        _currentCharacter = character;
        if (_currentCharacter != null)
        {
            // Get the camera to convert world position to screen position
            var camera = GetViewport().GetCamera2D();
            if (camera != null)
            {
                // Convert character's world position to screen coordinates
                Vector2 worldPos = character.GlobalPosition;
                Vector2 cameraOffset = worldPos - camera.GlobalPosition;
                Vector2 zoomedOffset = cameraOffset * camera.Zoom;
                Vector2 screenPos = zoomedOffset + GetViewport().GetVisibleRect().Size / 2;
                
                // Position menu above the character (in screen space)
                GlobalPosition = screenPos + new Vector2(0, -80);
            }
            Visible = true;
        }
    }

    public void HideMenu()
    {
        Visible = false;
        _currentCharacter = null;
    }

    private void OnMovePressed()
    {
        if (_grid != null && _currentCharacter != null)
        {
            // Sync GridManager's PlayerOffset to the current character's position
            // (needed on first move or if character position drifts)
            _grid.PlayerOffset = _currentCharacter.GetGridOffset();
            
            // Tell GridManager to start movement from this character's position
            _grid.StartMovement(_grid.PlayerOffset);
        }
        HideMenu();
    }

    private void OnWaitPressed()
    {
        if (_currentCharacter != null)
        {
            _currentCharacter.HasEndedTurn = true;
            if (_turnManager != null)
            {
                _turnManager.OnPlayerCharacterEndedTurn();
            }
        }
        HideMenu();
    }
}
