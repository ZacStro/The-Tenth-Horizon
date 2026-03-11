using Godot;
using System;
using System.Collections.Generic;

public partial class TurnManager : Node
{
    public enum Phase
    {
        PlayerTurns,
        EnemyTurns
    }

    [Signal]
    public delegate void PhaseChangedEventHandler(Phase newPhase);

    private Phase _currentPhase = Phase.PlayerTurns;
    private List<PlayerCharacterController> _playerCharacters = new List<PlayerCharacterController>();

    public override void _Ready()
    {
        // Auto-discover all player characters under Characters/
        var charactersNode = GetNodeOrNull<Node>("../Characters");
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

        // Start with player phase
        StartPlayerPhase();
    }

    private void StartPlayerPhase()
    {
        _currentPhase = Phase.PlayerTurns;
        ResetPlayerTurns();
        EmitSignal(SignalName.PhaseChanged, (int)Phase.PlayerTurns);
    }

    private void StartEnemyPhase()
    {
        _currentPhase = Phase.EnemyTurns;
        EmitSignal(SignalName.PhaseChanged, (int)Phase.EnemyTurns);
        // TODO: Implement enemy AI logic here
        // For now, just transition back to player phase
        GetTree().CreateTimer(1.0).Timeout += () => StartPlayerPhase();
    }

    public void OnPlayerCharacterEndedTurn()
    {
        // Check if all player characters have ended their turn
        if (HaveAllPlayersEndedTurn())
        {
            StartEnemyPhase();
        }
    }

    private bool HaveAllPlayersEndedTurn()
    {
        foreach (var character in _playerCharacters)
        {
            if (!character.HasEndedTurn)
                return false;
        }
        return true;
    }

    private void ResetPlayerTurns()
    {
        foreach (var character in _playerCharacters)
        {
            character.HasEndedTurn = false;
        }
    }
}
