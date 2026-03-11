using Godot;
using System;
using System.Collections.Generic;

public partial class MoveConfirmationController : Control
{
    //[Signal]
    //public delegate void AskForConfirmationMoveEventHandler();
    public enum MoveConfirmationState { UnConfirmed, Confirmed,  Cancelled }
   
    public MoveConfirmationState MoveConfirmed = MoveConfirmationState.UnConfirmed;
     public enum ConfirmationReasonState { Hazardous, Mobile } 
     public ConfirmationReasonState ConfirmationReason = ConfirmationReasonState.Hazardous;
     private PanelContainer _panelContainer = null;
    private VBoxContainer _menuContainer = null;
    private List<Button> _menuButtons = new List<Button>();
    private PlayerCharacterController _currentCharacter = null;
    private GridManager _grid = null;
    private TurnManager _turnManager = null;
    public override void _Ready()
    {
       
        GD.Print("FULL PATH: ", GetPath());
GD.Print("ROOT PATH: ", GetTree().Root.GetPath());
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
        GD.Print("GridManager found: ", _grid != null, "Name: ", _grid?.Name);

        // Search for TurnManager from GridManager's parent
        if (_grid != null)
        {
            node = _grid.GetParent();
            while (node != null && _turnManager == null)
            {
                _turnManager = node.GetNodeOrNull<TurnManager>("TurnManager");
                node = node.GetParent();
            }
            GD.Print("TurnManager found: ", _turnManager != null, "Name: ", _turnManager?.Name);
        }
        GD.Print("MoveConfirmationController ready, connecting signals...");

        // Connect("MoveConfirmationRequested", Callable.From(() => MoveConfirmationRequested()));
        Connect("MoveConfirmationRequested", new Callable(this, nameof(MoveConfirmationRequested)));
        // Create menu UI with PanelContainer as the visible element
        var bgStyleBox = new StyleBoxFlat();
        bgStyleBox.BgColor = new Color(0.1f, 0.1f, 0.1f, 0.95f);
        bgStyleBox.SetContentMarginAll(8f);

        _panelContainer = new PanelContainer();
        _panelContainer.AddThemeStyleboxOverride("panel", bgStyleBox);
        _panelContainer.ZIndex = 2000;
        _panelContainer.Position = GetViewport().GetVisibleRect().Size / 2; // Start in center of screen
        AddChild(_panelContainer);

        _menuContainer = new VBoxContainer();
        _menuContainer.CustomMinimumSize = new Vector2(120, 0);
        _panelContainer.AddChild(_menuContainer);

        // Create warning label
        var warningLabel = new Label();
        warningLabel.Text = "Are you sure you want to move?";
        warningLabel.HorizontalAlignment = HorizontalAlignment.Center;
        warningLabel.AddThemeFontSizeOverride("font_size", 14); // Optional: smaller font
        warningLabel.Modulate = new Color(1f, 1f, 1f, 0.9f); // Optional: slightly transparent
        _menuContainer.AddChild(warningLabel);

        // Create button style
        var buttonStyle = new StyleBoxFlat();
        buttonStyle.BgColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        buttonStyle.SetContentMarginAll(6f);

        // Create Confirm Move button
        var confirmButton = new Button();
        confirmButton.Text = "Confirm Move";
        confirmButton.CustomMinimumSize = new Vector2(100, 32);
        confirmButton.AddThemeStyleboxOverride("normal", buttonStyle);
        confirmButton.Pressed += OnConfirmPressed;
        _menuContainer.AddChild(confirmButton);
        _menuButtons.Add(confirmButton);

        // Create Wait button
        var waitButton = new Button();
        waitButton.Text = "Nevermind";
        waitButton.CustomMinimumSize = new Vector2(100, 32);
        waitButton.AddThemeStyleboxOverride("normal", buttonStyle);
        waitButton.Pressed += OnWaitPressed;
        _menuContainer.AddChild(waitButton);
        _menuButtons.Add(waitButton);
    }

      public void MoveConfirmationRequested()
    {
        GD.Print("Received signal to ask for move confirmation...");
        if (_menuContainer.GetChildCount() > 0)
    {
        GD.Print("Updating warning label text based on confirmation reason...");
        var warningLabel = _menuContainer.GetChild<Label>(0);
        if (warningLabel != null)
        {
            warningLabel.Text = ConfirmationReason == ConfirmationReasonState.Hazardous
                ? "Hazardous tile ahead. Confirm?" 
                : "Confirm move?";
            GD.Print($"Label updated: {warningLabel.Text}");
        }
    }
        ShowMenu();
    }

    public void ShowMenu()
    {
        GD.Print("Showing move confirmation menu...");
        // GD.Print("Current character: ", _currentCharacter != null ? _currentCharacter.Name : "None");
        // if (_currentCharacter != null)
        // {
            // Get the camera to convert world position to screen position
            var camera = GetViewport().GetCamera2D();
            GD.Print($"Camera found: {camera != null}, Camera name: {camera?.Name}");
            if (camera != null)
            {
                Vector2 screenPos = camera.GlobalPosition;
                GD.Print($"Camera global position: {camera.GlobalPosition}, screen visible rect: {GetViewport().GetVisibleRect().Size}");
                // Position menu in middle of screen
                GlobalPosition = screenPos + new Vector2(0, -80);
            }
            Visible = true;
            GD.Print("Menu should now be visible. Current character: ", _currentCharacter.Name);
        // }
    }

    public void HideMenu()
    {
        Visible = false;
        _currentCharacter = null;
    }

     private void OnWaitPressed()
    {
        if (_grid != null)
        {
            MoveConfirmed = MoveConfirmationState.Cancelled;
            _grid.IsMoving = false;
        }
        HideMenu();
    }

     private void OnConfirmPressed()
    {
        if (_grid != null /*&& _currentCharacter != null*/)
        {
            MoveConfirmed = MoveConfirmationState.Confirmed;
        }
        HideMenu();
    }

}
