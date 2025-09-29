using Godot;
using System;
using System.Collections.Generic;

public partial class TileMapLayerController : TileMapLayer
{
	//Constants
	const int MainLayer = 0;
	const int MainAtlasID = 1;
	private static readonly Vector2I PlayerAtlasCoords = new Vector2I(0,2);
	private static readonly Vector2I BlankAtlasCoords  = new Vector2I(0, 0);
	private const int MovementHighlightAltIndex = 1;
	private const int PathLineAltIndex = 2;
	private const int CursorAltIndex   = 3;
	private const int MoveRadius = 7;
	
	private HashSet<Vector2I> _moveRadiusHighlighted = new HashSet<Vector2I>();
	private List<Vector2I> _lastMovePath = new List<Vector2I>();
	private bool isMoving = false;
	private Vector2I _playerStartOffset;
	
	private Vector2I _currentAtlasCoords;
	
	public override void _Process(double delta)
	{
		if (!isMoving) return;
		var mousePos = GetGlobalMousePosition();
		var cursorOffset = LocalToMap(ToLocal(mousePos));
		var atlas = GetCellAtlasCoords(cursorOffset);
		if (atlas == BlankAtlasCoords && _moveRadiusHighlighted.Contains(cursorOffset))
		{
			// Valid target: clear any previous alt-2/alt-3 marks, then:
			DrawPathLine(_playerStartOffset, cursorOffset);
		}
	}
	
	 public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseButton mb &&
		mb.ButtonIndex == MouseButton.Left && 
		mb.IsPressed())
		{
			// old var offsetClicked = LocalToMap(ToLocal(mb.Position));
			var worldPoint    = GetGlobalMousePosition();
			var localPoint    = ToLocal(worldPoint);
		 	var offsetClicked = LocalToMap(localPoint);
			var axialClicked  = OffsetToAxial(offsetClicked);
			var cubeCenter    = AxialToCube(axialClicked);
			var atlasCoords = GetCellAtlasCoords(offsetClicked);
			if (isMoving && _moveRadiusHighlighted.Contains(offsetClicked))
			{
				ClearLastPath();
				ClearMoveRadius();
				_moveRadiusHighlighted.Clear();
				SetCell(offsetClicked, MainAtlasID, PlayerAtlasCoords, 0);
				SetCell(_playerStartOffset, MainAtlasID, BlankAtlasCoords, 0);
			} else
			{
				if (atlasCoords == PlayerAtlasCoords)
				{
					isMoving = true;
					_playerStartOffset = offsetClicked;
					isMoving = true;
					// Highlight all blank neighbors within radius
					_moveRadiusHighlighted.Clear();
					foreach (var cube in ComputeReachableRadiusCubes(cubeCenter, MoveRadius))
					{
						// Skip the center (the player)
						if (cube == cubeCenter)
							continue;

						// Convert cube → axial → offset
						var axial  = new Vector2I(cube.X, cube.Y);
						var offset = AxialToOffset(axial);

						if (GetCellAtlasCoords(offset) == BlankAtlasCoords)
						{
							SetCell(offset, MainAtlasID, BlankAtlasCoords, MovementHighlightAltIndex);
							_moveRadiusHighlighted.Add(offset);
						}
					}
				}
			}
			
			GD.Print($"Offset: {offsetClicked}, Axial: {axialClicked}, Cube: {cubeCenter}");
			GD.Print($"Atlas: {atlasCoords}, IsMoving: {isMoving}");
		 }
	}
	
	private void ClearMoveRadius()
	{
		foreach (var moveOfs in _moveRadiusHighlighted)
		{
			// Only reset blanks (avoid overwriting player or other marks)
			if (GetCellAtlasCoords(moveOfs) == BlankAtlasCoords)
				SetCell(moveOfs, MainAtlasID, BlankAtlasCoords, 0);
		}
	}
	
	private void ClearLastPath()
	{
		foreach (var oldOfs in _lastMovePath)
		{
			// Only reset blanks (avoid overwriting player or other marks)
			if (GetCellAtlasCoords(oldOfs) == BlankAtlasCoords)
				SetCell(oldOfs, MainAtlasID, BlankAtlasCoords, MovementHighlightAltIndex);
		}
	}
	
	private void DrawPathLine(Vector2I startOffset, Vector2I endOffset)
	{
		var startCube = OffsetToCube(startOffset);
		var endCube   = OffsetToCube(endOffset);
		var cubePath  = FindPathCubes(startCube, endCube);

		ClearLastPath();
		_lastMovePath.Clear();

		for (int i = 1; i < cubePath.Count; i++)
		{
			var cube = cubePath[i];
			var ofs  = AxialToOffset(new Vector2I(cube.X, cube.Y));  // or track inverse of OffsetToCube
			int alt  = (i == cubePath.Count - 1) ? CursorAltIndex : PathLineAltIndex;
			SetCell(ofs, MainAtlasID, BlankAtlasCoords, alt);
			_lastMovePath.Add(ofs);
		}
	}

	private List<Vector3I> FindPathCubes(Vector3I startCube, Vector3I endCube)
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
				if (GetCellAtlasCoords(offset) != BlankAtlasCoords)
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
			return path;

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
	private HashSet<Vector3I> ComputeReachableRadiusCubes(Vector3I startCube, int maxRadius)
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
				if (GetCellAtlasCoords(nbOffset) != BlankAtlasCoords)
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
	/// <summary>
	/// Converts odd-r offset (odd rows shifted right) to axial (q,r).
	/// Uses col = q + (r–(r&1))/2  ⇒  q = col – (r–(r&1))/2
	/// </summary>
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
}
