using Godot;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;

public partial class GridManager : TileMapLayer
{
	[Signal]
	public delegate void PlayerMovedEventHandler(Vector2I from, Vector2I to, Vector2 worldPos);
	[Signal]
    public delegate void MoveConfirmationRequestedEventHandler();

	// Public current player offset (tile coordinates)
	public Vector2I PlayerOffset { get; set; } = new Vector2I(-1, -1);
	// Last committed player world position (global coordinates)
	public Vector2 PlayerWorldPosition { get; private set; } = Vector2.Zero;
	public bool IsMoving = false;
	private int MoveTouches = 0; // Track number of touches during move confirmation flow to prevent multiple confirmations on mobile
	//Constants
	const int MainLayer = 0;
	const int MainAtlasID = 1;
	private static readonly Vector2I PlayerAtlasCoords = new Vector2I(0,2);
	private static readonly Vector2I BlankAtlasCoords  = new Vector2I(0, 0);
	private static readonly Vector2I HazardAtlasCoords  = new Vector2I(0, 1);
	private const int MovementHighlightAltIndex = 1;
	private const int HazardousEndAltIndex = 4;
	private const int PathLineAltIndex = 2;
	private const int CursorAltIndex   = 3;
	private const int MoveRadius = 7;
	
	private HashSet<Vector2I> _moveRadiusHighlighted = new HashSet<Vector2I>();
	private HashSet<Vector2I> _HazzardousEndHighlighted = new HashSet<Vector2I>();
	private List<Vector2I> _lastMovePath = new List<Vector2I>();

	private Vector2I _playerStartOffset;
	
	private Vector2I _offsetClicked; // Track the currently clicked offset for move confirmation flow
	private Vector2I _currentAtlasCoords;

	private MoveConfirmationController _moveConfirmationController = null;

    public override void _Ready()
    {
		// Search up the tree for MoveConfirmationController
		Node node = this;  
		GD.Print("Starting node for search: ", node.Name);      
		while (node != null && _moveConfirmationController == null)
		{
			GD.Print("Checking node: ", node.Name);
			_moveConfirmationController = node.GetNodeOrNull<MoveConfirmationController>("UI/MoveConfirmation");
			node = node.GetParent();
		}
		GD.Print("MoveConfirmationController found: ", _moveConfirmationController != null, " Name: ", _moveConfirmationController?.Name);

		if (_moveConfirmationController != null)
    	{
        // CONNECT GridManager signal TO MoveConfirmationController
        Connect(SignalName.MoveConfirmationRequested, 
                new Callable(_moveConfirmationController, nameof(MoveConfirmationController.MoveConfirmationRequested)));
        GD.Print("Signal connected: GridManager -> MoveConfirmationController");
    	}
    }	

	public void StartMovement(Vector2I startOffset)
	{
		IsMoving = true;
		_playerStartOffset = startOffset;
		DrawMoveRadius(startOffset, true); //Draw with Hazards
		_HazzardousEndHighlighted = new HashSet<Vector2I>(_moveRadiusHighlighted); //Move list with hazards to a separate Vector
		//GD.Print($"_moveRadiusHighlighted count: {_moveRadiusHighlighted.Count} Hazard count: {_HazzardousEndHighlighted.Count}");
		_moveRadiusHighlighted.Clear(); //Clear highlights
		//GD.Print($"_moveRadiusHighlighted cleared.");
		DrawMoveRadius(startOffset); //Draw without Hazards to show valid end points
		//GD.Print($"Redo: _moveRadiusHighlighted count: {_moveRadiusHighlighted.Count} Hazard count: {_HazzardousEndHighlighted.Count}");
		_HazzardousEndHighlighted.ExceptWith(_moveRadiusHighlighted); //Keep track of hazard end points to allow movement there without messing with non-hazard movement\
		//GD.Print($"Post except: _moveRadiusHighlighted count: {_moveRadiusHighlighted.Count} Hazard count: {_HazzardousEndHighlighted.Count}");
	}

	public void DrawMoveRadius(Vector2I startOffset, bool allowHazards = false)
	{
		if (!IsMoving) return;
		var axial = OffsetToAxial(startOffset);
		var cubeCenter = AxialToCube(axial);

		foreach (var cube in ComputeReachableRadiusCubes(cubeCenter, MoveRadius, allowHazards))
		{
			if (cube == cubeCenter)
				continue;

			var ax = new Vector2I(cube.X, cube.Y);
			var offset = AxialToOffset(ax);

			if (GetCellAtlasCoords(offset) == BlankAtlasCoords)
			{
				if (allowHazards)
				{
					SetCell(offset, MainAtlasID, BlankAtlasCoords, HazardousEndAltIndex);
				_moveRadiusHighlighted.Add(offset);
				}
				else
				{
					SetCell(offset, MainAtlasID, BlankAtlasCoords, MovementHighlightAltIndex);
				_moveRadiusHighlighted.Add(offset);
				}
				
			}
			if (GetCellAtlasCoords(offset) == HazardAtlasCoords)
			{
				SetCell(offset, MainAtlasID, HazardAtlasCoords, MovementHighlightAltIndex);
				_moveRadiusHighlighted.Add(offset);
			}
		}
	}

	public void StopMovement()
	{
		IsMoving = false;
		ClearLastPath();
		ClearMoveRadius();
		_moveRadiusHighlighted.Clear();
	}

	public override void _Process(double delta)
	{
		if (IsMoving){ 
		//GD.Print("Processing move preview...");
		var mousePos = GetGlobalMousePosition();
		var cursorOffset = LocalToMap(ToLocal(mousePos));
		var atlas = GetCellAtlasCoords(cursorOffset);
		//GD.Print($"muspos: {mousePos} cursorOffset: {cursorOffset} atlas: {atlas}");
		//GD.Print($"muspos: {mousePos} cursorOffset: {cursorOffset} atlas: {atlas}");
		//GD.Print($"_moveRadiusHighlighted count: {_moveRadiusHighlighted.Count} moveRadiusHighlighted contains cursoroffset: {_moveRadiusHighlighted.Contains(cursorOffset)}");
		if ((atlas == BlankAtlasCoords || atlas == HazardAtlasCoords))
		{
			if (_moveRadiusHighlighted.Contains(cursorOffset))
			{
				// GD.Print($"Drawing safe path: {_playerStartOffset} -> {cursorOffset}");
				// Valid target: clear any previous alt-2/alt-3 marks, then:
				DrawPathLine(_playerStartOffset, cursorOffset);
				// GD.Print("Draw safe PathLine called"); 
			} else if (_HazzardousEndHighlighted.Contains(cursorOffset)) {
				// GD.Print($"Drawing hazard path: {_playerStartOffset} -> {cursorOffset}");
				// Valid target: clear any previous alt-2/alt-3 marks, then:
				DrawPathLine(_playerStartOffset, cursorOffset, false); //Draw path including hazards to show player where they could go if they accepted hazard risk
				// GD.Print("Draw hazard PathLine called"); 
			}
		}
		}
	}
	
	 public override void _Input(InputEvent @event)
	{
		if (GridManager.IsMobile())
		{ // Mobile: touch to confirm move
			if (@event is InputEventScreenTouch touch && !touch.IsPressed() && IsMoving)
			{
				MoveTouches++;
				if (MoveTouches <= 1)
				{
					return;	
				}
				
				var worldPoint    = GetGlobalMousePosition();
				var localPoint    = ToLocal(worldPoint);
				_offsetClicked = LocalToMap(localPoint);
				// IsMoving = false;
				GD.Print($"TOUCH world={worldPoint}, local={localPoint}, offset={_offsetClicked}");

				if (_moveRadiusHighlighted.Contains(_offsetClicked)|| _HazzardousEndHighlighted.Contains(_offsetClicked))
				{
					MoveTouches = 0; //Reset touch count for next move
					IsMoving = false;
					_moveConfirmationController.ConfirmationReason = MoveConfirmationController.ConfirmationReasonState.Mobile;
					_moveConfirmationController.MoveConfirmed = MoveConfirmationController.MoveConfirmationState.UnConfirmed;
					EmitSignal(nameof(MoveConfirmationRequested));
				}
			}
		}
		else 
		{ // Desktop: left mouse click to confirm move
			if (@event is InputEventMouseButton mb &&
			mb.ButtonIndex == MouseButton.Left && 
			mb.IsPressed() && 
			IsMoving)
			{
				GD.Print($"Movement happening, mouse event detected. MousePos: {mb.Position}");
				var worldPoint    = GetGlobalMousePosition();
				var localPoint    = ToLocal(worldPoint);
				_offsetClicked = LocalToMap(localPoint);
				GD.Print($"TOUCH world={worldPoint}, local={localPoint}, offset={_offsetClicked}");
				GD.Print($"Mouse world point: {worldPoint}, local point: {localPoint}, offset clicked: {_offsetClicked}");
				if (_moveRadiusHighlighted.Contains(_offsetClicked))
				{
					GD.Print("Safe move clicked, committing move.");
					CommitMove(_offsetClicked);
				} else if (_HazzardousEndHighlighted.Contains(_offsetClicked)) {
					GD.Print("Hazardous move clicked, asking for confirmation.");
					IsMoving = false;
					_moveConfirmationController.ConfirmationReason = MoveConfirmationController.ConfirmationReasonState.Hazardous;
					_moveConfirmationController.MoveConfirmed = MoveConfirmationController.MoveConfirmationState.UnConfirmed;
					GD.Print("Emitting MoveConfirmationRequested signal.");
					EmitSignal(nameof(MoveConfirmationRequested));
					GD.Print("Waiting for move confirmation...");
					// while (_moveConfirmationController.MoveConfirmed == MoveConfirmationController.MoveConfirmationState.UnConfirmed)
					// {
					// 	GD.Print("Waiting for move confirmation...");
						
					// }
					// GD.Print($"Move confirmation result: {_moveConfirmationController.MoveConfirmed}");
					// if(_moveConfirmationController.MoveConfirmed == MoveConfirmationController.MoveConfirmationState.Confirmed)
					// {
					// 	GD.Print("Move confirmed, committing move.");
					// 	CommitMove(offsetClicked);
					// }
				}
			}
		}

		switch (_moveConfirmationController.MoveConfirmed)
		{
			case MoveConfirmationController.MoveConfirmationState.UnConfirmed:
				// Show confirmation UI or wait
				break;
				
			case MoveConfirmationController.MoveConfirmationState.Confirmed:
				CommitMove(_offsetClicked);
				_moveConfirmationController.MoveConfirmed = MoveConfirmationController.MoveConfirmationState.UnConfirmed; // Reset
				break;
				
			case MoveConfirmationController.MoveConfirmationState.Cancelled:
				// Just return to move selection state (keep move radius visible, allow clicking another tile or same tile again)
				_moveConfirmationController.MoveConfirmed = MoveConfirmationController.MoveConfirmationState.UnConfirmed; // Reset
				break;
		}
	}

	private void CommitMove(Vector2I offsetClicked)
	{
		// Commit move: update tiles, update tracked player offset, and emit a signal
		ClearLastPath();
		ClearMoveRadius();
		_moveRadiusHighlighted.Clear();
		SetCell(offsetClicked, MainAtlasID, PlayerAtlasCoords, 0);
		SetCell(_playerStartOffset, MainAtlasID, BlankAtlasCoords, 0);

		var from = _playerStartOffset;
		var to = offsetClicked;
		PlayerOffset = to;
		// Compute the world position of the target tile and store it.
		Vector2 tileLocal = MapToLocal(to);
		Vector2 tileWorld = ToGlobal(tileLocal);
		PlayerWorldPosition = tileWorld;
		// Emit signal so character nodes can follow the tile visually
		// Pass the computed tile world position so listeners don't need to map offsets themselves.
		EmitSignal(nameof(PlayerMoved), from, to, tileWorld);
		// Leave move mode
		IsMoving = false;
	}

	private void ClearMoveRadius()
	{
		foreach (var moveOfs in _moveRadiusHighlighted)
		{
			// Only reset blanks (avoid overwriting player or other marks)
			if (GetCellAtlasCoords(moveOfs) == BlankAtlasCoords)
				SetCell(moveOfs, MainAtlasID, BlankAtlasCoords, 0);
			if (GetCellAtlasCoords(moveOfs) == HazardAtlasCoords)
				SetCell(moveOfs, MainAtlasID, HazardAtlasCoords, 0);
		}
		foreach (var moveOfs in _HazzardousEndHighlighted)
		{
			// Only reset blanks (avoid overwriting player or other marks)
			if (GetCellAtlasCoords(moveOfs) == BlankAtlasCoords)
				SetCell(moveOfs, MainAtlasID, BlankAtlasCoords, 0);
			if (GetCellAtlasCoords(moveOfs) == HazardAtlasCoords)
				SetCell(moveOfs, MainAtlasID, HazardAtlasCoords, 0);
		}
	}
	
	private void ClearLastPath()
	{
		foreach (var oldOfs in _lastMovePath)
		{
			if (GetCellAtlasCoords(oldOfs) == BlankAtlasCoords)
			{
			if(_moveRadiusHighlighted.Contains(oldOfs))
				SetCell(oldOfs, MainAtlasID, BlankAtlasCoords, MovementHighlightAltIndex);
			if(_HazzardousEndHighlighted.Contains(oldOfs))
				SetCell(oldOfs, MainAtlasID, BlankAtlasCoords, HazardousEndAltIndex);
			}
			if (GetCellAtlasCoords(oldOfs) == HazardAtlasCoords)
				SetCell(oldOfs, MainAtlasID, HazardAtlasCoords, MovementHighlightAltIndex);
		}
	}
	
	private void DrawPathLine(Vector2I startOffset, Vector2I endOffset, bool avoidHazards = true)
	{
		var startCube = OffsetToCube(startOffset);
		var endCube   = OffsetToCube(endOffset);
		var cubePath  = FindPathCubes(startCube, endCube, avoidHazards);

		ClearLastPath();
		_lastMovePath.Clear();

		if (cubePath == null || cubePath.Count == 0)
		{
			GD.Print($"No path found from {startOffset} to {endOffset}");
			GD.Print($"Cubeepath: {endOffset}");
			return;
		}

		for (int i = 1; i < cubePath.Count; i++)
		{
			var cube = cubePath[i];
			var ofs  = AxialToOffset(new Vector2I(cube.X, cube.Y));  // or track inverse of OffsetToCube
			int alt  = (i == cubePath.Count - 1) ? CursorAltIndex : PathLineAltIndex;
			if (GetCellAtlasCoords(ofs) == BlankAtlasCoords)
			{
				if(_HazzardousEndHighlighted.Contains(ofs))
				{alt += 3;} //If this is a hazardous end point, use the alt index that includes the hazard marker
				SetCell(ofs, MainAtlasID, BlankAtlasCoords, alt);
			_lastMovePath.Add(ofs);
			} else if (GetCellAtlasCoords(ofs) == HazardAtlasCoords)
			{
				SetCell(ofs, MainAtlasID, HazardAtlasCoords, alt);
			}
			_lastMovePath.Add(ofs);
			
		}
	}

	private List<Vector3I> FindPathCubes(Vector3I startCube, Vector3I endCube, bool avoidHazards = true)
	{
		var frontier = new PriorityQueue<Vector3I, int>();
		var cameFrom = new Dictionary<Vector3I, Vector3I>();
		var costSoFar = new Dictionary<Vector3I, int>();

		frontier.Enqueue(startCube, 0);
		cameFrom[startCube] = startCube;
		costSoFar[startCube] = 0;

		Vector3I[] directions = {
			new(1,-1,0), new(1,0,-1), new(0,1,-1),
			new(-1,1,0), new(-1,0,1), new(0,-1,1)
		};

		while (frontier.Count > 0)
		{
			var current = frontier.Dequeue();
			if (current == endCube)
				break;

			foreach (var dir in directions)
			{
				var next = current + dir;
				// Convert to offset to test walkability
				var offset = AxialToOffset(new Vector2I(next.X, next.Y));
				if (!(GetCellAtlasCoords(offset) == BlankAtlasCoords || GetCellAtlasCoords(offset) == HazardAtlasCoords))
					continue;
				if (avoidHazards && 
				GetCellAtlasCoords(offset) == HazardAtlasCoords)
					continue;

				int newCost = costSoFar[current] + 1;
				if (!costSoFar.ContainsKey(next) || newCost < costSoFar[next])
				{
					costSoFar[next] = newCost;
					int priority = newCost + CubeDistance(next, endCube);
					frontier.Enqueue(next, priority);
					cameFrom[next] = current;
				}
			}
		}

		// Reconstruct cube path
		var path = new List<Vector3I>();
		if (!cameFrom.ContainsKey(endCube))
		{
			GD.Print("No path found from ", startCube, " to ", endCube);
			return path;
		}
		var step = endCube;
		while (step != startCube)
		{
			path.Add(step);
			step = cameFrom[step];
		}
		path.Add(startCube);
		path.Reverse();
		return path;
	}
	private List<Vector2I> HexLine(Vector3I a, Vector3I b)
	{
		int N = CubeDistance(a, b);
		var results = new List<Vector2I>();
		for (int i = 0; i <= N; i++)
		{
			var t = N == 0 ? 0f : (float)i / N;
			var lerped = CubeLerp(a, b, t);
			var rounded = CubeRound(lerped);
			var axial = new Vector2I(rounded.X, rounded.Y);
			results.Add(AxialToOffset(axial));
		}
		return results;
	}
	// Helper: linear interp between cubes
	private Vector3 CubeLerp(Vector3I a, Vector3I b, float t) =>
		new Vector3(
			Mathf.Lerp(a.X, b.X, t),
			Mathf.Lerp(a.Y, b.Y, t),
			Mathf.Lerp(a.Z, b.Z, t)
		);
	// Round to nearest integer cube coords, adjusting to satisfy x+y+z=0
	private Vector3I CubeRound(Vector3 v)
	{
		int rx = Mathf.RoundToInt(v.X);
		int ry = Mathf.RoundToInt(v.Y);
		int rz = Mathf.RoundToInt(v.Z);
		var dx = Mathf.Abs(rx - v.X);
		var dy = Mathf.Abs(ry - v.Y);
		var dz = Mathf.Abs(rz - v.Z);
		if (dx > dy && dx > dz) rx = -ry - rz;
		else if (dy > dz)      ry = -rx - rz;
		else                    rz = -rx - ry;
		return new Vector3I(rx, ry, rz);
	}
	private int CubeDistance(Vector3I a, Vector3I b) =>
		(Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y) + Mathf.Abs(a.Z - b.Z)) / 2;
		
	// Generates all cube coordinates within radius N of center that can be reached
	private HashSet<Vector3I> ComputeReachableRadiusCubes(Vector3I startCube, int maxRadius, bool allowHazards = false)
	{
		var reachable = new HashSet<Vector3I>();
		// Maps visited cube → distance from start
		var visited = new Dictionary<Vector3I, int> { [startCube] = 0 };
		var queue   = new Queue<Vector3I>();
		queue.Enqueue(startCube);

		// Six cube‐direction vectors
		Vector3I[] dirs = {
			new(1, -1,  0), new(1,  0, -1), new(0,  1, -1),
			new(-1, 1,  0), new(-1, 0,  1), new(0, -1,  1)
		};

		while (queue.Count > 0)
		{
			var cube = queue.Dequeue();
			int dist = visited[cube];

			// Exclude the start cell itself if you wish:
			if (dist > 0)
				reachable.Add(cube);

			if (dist == maxRadius)
				continue;

			foreach (var d in dirs)
			{
				var nbCube = cube + d;
				if (visited.ContainsKey(nbCube))
					continue;

				// Convert to offset to test walkability
				var axial    = new Vector2I(nbCube.X, nbCube.Y);
				var nbOffset = AxialToOffset(axial);

				// Only traverse blank tiles
				if (!(GetCellAtlasCoords(nbOffset) == BlankAtlasCoords || GetCellAtlasCoords(nbOffset) == HazardAtlasCoords))
					continue;
				if (!allowHazards && GetCellAtlasCoords(nbOffset) == HazardAtlasCoords)
					continue;

				visited[nbCube] = dist + 1;
				queue.Enqueue(nbCube);
			}
		}

		return reachable;
	}


	
	private Vector3I OffsetToCube(Vector2I offset)
	{
		Vector2I axial = OffsetToAxial(offset);
		Vector3I cube = AxialToCube(axial);
		return cube;
	}

	private Vector2I OffsetToAxial(Vector2I offset)
	{
		int r = offset.Y;
		int q = offset.X - ((r - (r & 1)) >> 1);
		return new Vector2I(q, r);
	}

	private Vector2I AxialToOffset(Vector2I axial)
	{
		int q = axial.X;
		int r = axial.Y;
		int col = q + ((r - (r & 1)) >> 1);
		return new Vector2I(col, r);
	}
	/// <summary>
	/// Given axial (q,r) coordinates, computes the cube s coordinate so that q+r+s=0.
	/// </summary>
	/// <param name="axial">A Vector2I where x= q and y= r.</param>
	/// <returns>A Vector3I (q,r,s) with q= axial.x, r= axial.y, s= -q-r.</returns>
	private Vector3I AxialToCube(Vector2I axial)
	{
   	 	int q = axial.X;
		int r = axial.Y;
		int s = -q - r;
		return new Vector3I(q, r, s);
	}

	 public static bool IsMobile()
	{
		// Works for native Android/iOS
		if (OS.HasFeature("mobile"))
			return true;

		// If you export to web and care about mobile browsers:
		if (OS.HasFeature("web_android") || OS.HasFeature("web_ios"))
			return true;

		return false;
	}

}
