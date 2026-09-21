using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class MazeGenerator : MonoBehaviour
{
    private static readonly Vector3Int[] WallDirections =
    {
        Vector3Int.right,
        Vector3Int.left,
        Vector3Int.up,
        Vector3Int.down,
    };

    [Header("Mantıksal Maze Boyutu (hücre sayısı)")]
    public int logicalWidth = 10;
    public int logicalHeight = 6;

    [Header("Tilemap Referansları")]
    public Tilemap floorTilemap;
    public Tilemap wallTilemap;

    [Header("Sprite Referansları (Artık Tile değil, Sprite)")]
    public Sprite floorSprite;
    public Sprite wallSprite;

    [Header("Seed (0 = rastgele)")]
    public int seed = 0;

    // Maze her üretildiğinde tetiklenir. Player bunu dinleyip kendini başa konumlandırır.
    public event Action OnMazeGenerated;

    private bool[,] visited;
    private int gridWidth;
    private int gridHeight;
    private Vector3Int startCell = new Vector3Int(1, 1, 0);
    private Vector3Int endCell = new Vector3Int(1, 1, 0);

    // Runtime'da oluşturulan Tile'lar
    private Tile _floorTile;
    private Tile _wallTile;

    public int GridWidth => gridWidth;
    public int GridHeight => gridHeight;

    // Başlangıç ve bitiş hücreleri (OpenStartAndEnd ile aynı koordinatlar)
    public Vector3Int StartCell => startCell;
    public Vector3Int EndCell => endCell;

    private void Awake()
    {
        EnsureRuntimeTiles();
    }

    private void EnsureRuntimeTiles()
    {
        if (_floorTile == null && floorSprite != null)
        {
            _floorTile = ScriptableObject.CreateInstance<Tile>();
            _floorTile.sprite = floorSprite;
            _floorTile.color = Color.white;
            _floorTile.colliderType = Tile.ColliderType.None;
        }

        if (_wallTile == null && wallSprite != null)
        {
            _wallTile = ScriptableObject.CreateInstance<Tile>();
            _wallTile.sprite = wallSprite;
            _wallTile.color = Color.white;
            _wallTile.colliderType = Tile.ColliderType.None;
        }
    }

    public void Start()
    {
        Debug.Log(
            $"[MazeGenerator] Start -> logicalWidth={logicalWidth}, logicalHeight={logicalHeight}, seed={seed}"
        );
    }

    public void InitAndGenerate(int width, int height, int seed = 0)
    {
        InitAndGenerate(width, height, seed, MazeEndpointPattern.BottomLeftToTopRight, 0f);
    }

    public void InitAndGenerate(
        int width,
        int height,
        int seed,
        MazeEndpointPattern endpointPattern
    )
    {
        InitAndGenerate(width, height, seed, endpointPattern, 0f);
    }

    public void InitAndGenerate(
        int width,
        int height,
        int seed,
        MazeEndpointPattern endpointPattern,
        float extraOpeningChance
    )
    {
        logicalWidth = Mathf.Max(2, width);
        logicalHeight = Mathf.Max(2, height);
        this.seed = seed;
        SetEndpointCells(endpointPattern);
        EnsureRuntimeTiles();

        if (!floorTilemap || !wallTilemap || _floorTile == null || _wallTile == null)
        {
            Debug.LogError("MazeGenerator: Tilemap veya Sprite referansları eksik!");
            return;
        }

        visited = new bool[logicalWidth, logicalHeight];

        floorTilemap.ClearAllTiles();
        wallTilemap.ClearAllTiles();

        gridWidth = logicalWidth * 2 + 1;
        gridHeight = logicalHeight * 2 + 1;

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                floorTilemap.SetTile(new Vector3Int(x, y, 0), _floorTile);
            }
        }

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                wallTilemap.SetTile(new Vector3Int(x, y, 0), _wallTile);
            }
        }

        System.Random rng = (seed == 0) ? new System.Random() : new System.Random(seed);
        Stack<Vector2Int> stack = new Stack<Vector2Int>();

        Vector2Int current = new Vector2Int(0, 0);
        visited[0, 0] = true;
        stack.Push(current);

        while (stack.Count > 0)
        {
            current = stack.Peek();
            List<Vector2Int> neighbors = GetUnvisitedNeighbors(current);

            if (neighbors.Count > 0)
            {
                Vector2Int chosen = neighbors[rng.Next(neighbors.Count)];
                CarvePassage(current, chosen);

                visited[chosen.x, chosen.y] = true;
                stack.Push(chosen);
            }
            else
            {
                stack.Pop();
            }
        }

        OpenStartAndEnd();
        AddExtraOpenings(rng, Mathf.Clamp01(extraOpeningChance));

        // Oyuncu, labirent oluşturulduğunda başlangıç hücresine döner.
        OnMazeGenerated?.Invoke();
    }

    List<Vector2Int> GetUnvisitedNeighbors(Vector2Int cell)
    {
        List<Vector2Int> list = new List<Vector2Int>();

        Vector2Int[] dirs = new[]
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
        };

        foreach (var d in dirs)
        {
            Vector2Int n = cell + d;
            if (
                n.x >= 0
                && n.x < logicalWidth
                && n.y >= 0
                && n.y < logicalHeight
                && !visited[n.x, n.y]
            )
            {
                list.Add(n);
            }
        }

        return list;
    }

    void CarvePassage(Vector2Int a, Vector2Int b)
    {
        Vector3Int tileA = new Vector3Int(a.x * 2 + 1, a.y * 2 + 1, 0);
        Vector3Int tileB = new Vector3Int(b.x * 2 + 1, b.y * 2 + 1, 0);

        Vector3Int between = new Vector3Int((tileA.x + tileB.x) / 2, (tileA.y + tileB.y) / 2, 0);

        wallTilemap.SetTile(tileA, null);
        wallTilemap.SetTile(tileB, null);
        wallTilemap.SetTile(between, null);
    }

    void OpenStartAndEnd()
    {
        wallTilemap.SetTile(startCell, null);
        wallTilemap.SetTile(endCell, null);
    }

    private void AddExtraOpenings(System.Random rng, float chance)
    {
        if (chance <= 0f)
            return;

        for (int x = 2; x < gridWidth - 1; x += 2)
        {
            for (int y = 1; y < gridHeight - 1; y += 2)
            {
                TryOpenExtraWall(new Vector3Int(x, y, 0), rng, chance);
            }
        }

        for (int x = 1; x < gridWidth - 1; x += 2)
        {
            for (int y = 2; y < gridHeight - 1; y += 2)
            {
                TryOpenExtraWall(new Vector3Int(x, y, 0), rng, chance);
            }
        }
    }

    private void TryOpenExtraWall(Vector3Int cell, System.Random rng, float chance)
    {
        if (wallTilemap.GetTile(cell) == null)
            return;

        if (rng.NextDouble() > chance || WouldCreateSingleCellWall(cell))
            return;

        wallTilemap.SetTile(cell, null);
    }

    private bool WouldCreateSingleCellWall(Vector3Int opening)
    {
        if (opening.x % 2 == 0)
        {
            return WouldIsolatePillar(new Vector3Int(opening.x, opening.y - 1, 0), opening)
                || WouldIsolatePillar(new Vector3Int(opening.x, opening.y + 1, 0), opening);
        }

        return WouldIsolatePillar(new Vector3Int(opening.x - 1, opening.y, 0), opening)
            || WouldIsolatePillar(new Vector3Int(opening.x + 1, opening.y, 0), opening);
    }

    private bool WouldIsolatePillar(Vector3Int pillar, Vector3Int opening)
    {
        foreach (Vector3Int direction in WallDirections)
        {
            Vector3Int neighbor = pillar + direction;
            if (neighbor != opening && HasWall(neighbor))
                return false;
        }

        return true;
    }

    private bool HasWall(Vector3Int cell)
    {
        return wallTilemap.GetTile(cell) != null;
    }

    private void SetEndpointCells(MazeEndpointPattern endpointPattern)
    {
        Vector3Int bottomLeft = new Vector3Int(1, 1, 0);
        Vector3Int bottomRight = new Vector3Int(logicalWidth * 2 - 1, 1, 0);
        Vector3Int topLeft = new Vector3Int(1, logicalHeight * 2 - 1, 0);
        Vector3Int topRight = new Vector3Int(logicalWidth * 2 - 1, logicalHeight * 2 - 1, 0);

        switch (endpointPattern)
        {
            case MazeEndpointPattern.TopLeftToBottomRight:
                startCell = topLeft;
                endCell = bottomRight;
                break;
            case MazeEndpointPattern.BottomRightToTopLeft:
                startCell = bottomRight;
                endCell = topLeft;
                break;
            case MazeEndpointPattern.TopRightToBottomLeft:
                startCell = topRight;
                endCell = bottomLeft;
                break;
            default:
                startCell = bottomLeft;
                endCell = topRight;
                break;
        }
    }

    /// Bir grid hücresinin dünya merkez noktası.
    public Vector3 CellToWorld(Vector3Int cell)
    {
        return floorTilemap.GetCellCenterWorld(cell);
    }

    /// Grid hücre koordinatına göre yürünebilirlik (sınır + zemin var + duvar yok).
    public bool IsCellWalkable(Vector3Int cell)
    {
        if (cell.x < 0 || cell.y < 0 || cell.x >= gridWidth || cell.y >= gridHeight)
            return false;

        bool hasWall = wallTilemap.GetTile(cell) != null;
        bool hasFloor = floorTilemap.GetTile(cell) != null;
        return hasFloor && !hasWall;
    }

    public bool IsWall(Vector3 worldPos)
    {
        Vector3Int cell = wallTilemap.WorldToCell(worldPos);
        return wallTilemap.GetTile(cell) != null;
    }

    public bool IsWalkable(Vector3 worldPos)
    {
        Vector3Int cell = wallTilemap.WorldToCell(worldPos);
        bool hasWall = wallTilemap.GetTile(cell) != null;
        bool hasFloor = floorTilemap.GetTile(cell) != null;
        return hasFloor && !hasWall;
    }

    public int GetMinimumSlideMoves(bool followCorners)
    {
        if (gridWidth <= 0 || gridHeight <= 0)
            return 0;

        Queue<Vector3Int> queue = new Queue<Vector3Int>();
        Dictionary<Vector3Int, int> distances = new Dictionary<Vector3Int, int>();

        queue.Enqueue(startCell);
        distances[startCell] = 0;

        Vector3Int[] dirs =
        {
            new Vector3Int(1, 0, 0),
            new Vector3Int(-1, 0, 0),
            new Vector3Int(0, 1, 0),
            new Vector3Int(0, -1, 0),
        };

        while (queue.Count > 0)
        {
            Vector3Int current = queue.Dequeue();
            int nextDistance = distances[current] + 1;

            for (int i = 0; i < dirs.Length; i++)
            {
                Vector3Int next = SimulateSlide(current, dirs[i], followCorners, dirs);
                if (next == current || distances.ContainsKey(next))
                    continue;

                distances[next] = nextDistance;
                if (next == endCell)
                    return nextDistance;

                queue.Enqueue(next);
            }
        }

        return 0;
    }

    private Vector3Int SimulateSlide(
        Vector3Int start,
        Vector3Int startDir,
        bool followCorners,
        Vector3Int[] dirs
    )
    {
        Vector3Int d = startDir;
        Vector3Int cur = start;

        if (!IsCellWalkable(cur + d))
            return start;

        while (true)
        {
            Vector3Int next = cur + d;
            if (!IsCellWalkable(next))
                break;

            cur = next;
            if (cur == endCell)
                break;

            Vector3Int back = -d;
            int exitCount = 0;
            Vector3Int onlyExit = Vector3Int.zero;
            for (int i = 0; i < dirs.Length; i++)
            {
                if (dirs[i] == back)
                    continue;

                if (IsCellWalkable(cur + dirs[i]))
                {
                    exitCount++;
                    onlyExit = dirs[i];
                }
            }

            if (exitCount == 1)
            {
                if (onlyExit == d)
                    continue;

                if (followCorners)
                {
                    d = onlyExit;
                    continue;
                }
            }

            break;
        }

        return cur;
    }
}

public enum MazeEndpointPattern
{
    BottomLeftToTopRight,
    TopLeftToBottomRight,
    BottomRightToTopLeft,
    TopRightToBottomLeft,
}
