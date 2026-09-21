using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(SpriteRenderer))]
public class PlayerController : MonoBehaviour
{
    [Header("Referanslar (boşsa otomatik bulunur)")]
    public MazeGenerator mazeGenerator;
    public ExitFromToMaze exitManager;

    [Header("Hareket")]
    [Tooltip("Kayma hızı (tile/saniye). 1 tile = 1 birim. Büyük değer = hızlı.")]
    public float moveSpeed = 11f;

    [Tooltip(
        "Köşelerde otomatik dönsün mü? Açık: koridorda tek yol varken takip eder, sadece KAVŞAKLARDA durup seçim bekler. Kapalı: her yön değişiminde durur."
    )]
    public bool followCorners = true;

    [Header("Hareket Noktaları (White Dots)")]
    public bool showMoveDots = true;
    public Color dotColor = Color.white;

    [Tooltip("Nokta görsel boyutu (Unity birimi). 1 tile = 1 birim.")]
    public float dotScale = 0.30f;
    public int dotSortingOrder = 50;

    [Tooltip("Boş bırakırsan otomatik yumuşak beyaz daire üretilir.")]
    public Sprite dotSprite;

    [Header("Swipe (Mobil)")]
    [Tooltip("Swipe sayılması için gereken min. piksel mesafesi.")]
    public float swipeThreshold = 40f;

    [Tooltip("Hareket noktasina dokunma alaninin tile cinsinden yaricapi.")]
    public float dotTapRadius = 0.5f;

    [Header("Tamamlanınca")]
    public bool notifyOnComplete = true;

    private static readonly Vector3Int[] Dirs =
    {
        new Vector3Int(1, 0, 0),
        new Vector3Int(-1, 0, 0),
        new Vector3Int(0, 1, 0),
        new Vector3Int(0, -1, 0),
    };

    private Vector3Int currentCell;
    private bool isMoving;
    private bool placed;

    // Kayma yolu (polyline) – tüm slide boyunca sabit hızla, duraksamadan ilerlenir
    private readonly List<Vector3> pathPoints = new List<Vector3>();
    private int pathIndex;

    private sealed class PooledDot
    {
        public GameObject gameObject;
        public Vector3Int direction;
    }

    private readonly List<PooledDot> dotPool = new List<PooledDot>(4);
    private Transform dotPoolRoot;
    private Camera mainCamera;

    private Vector2 dragStart;
    private bool dragging;

    [System.Obsolete]
    private void Awake()
    {
        if (!mazeGenerator)
            mazeGenerator = FindObjectOfType<MazeGenerator>();
        if (!exitManager)
            exitManager = FindObjectOfType<ExitFromToMaze>();

        if (dotSprite == null)
            dotSprite = GenerateDotSprite();

        mainCamera = Camera.main;
        InitializeDotPool();
    }

    private void OnEnable()
    {
        if (mazeGenerator != null)
            mazeGenerator.OnMazeGenerated += HandleMazeGenerated;
    }

    private void OnDisable()
    {
        if (mazeGenerator != null)
            mazeGenerator.OnMazeGenerated -= HandleMazeGenerated;
        ClearDots();
    }

    private void HandleMazeGenerated()
    {
        PlaceAtStart();
    }

    public void PlaceAtStart()
    {
        if (mazeGenerator == null)
            return;

        currentCell = mazeGenerator.StartCell;
        transform.position = mazeGenerator.CellToWorld(currentCell);
        isMoving = false;
        placed = true;
        pathPoints.Clear();
        RefreshDots();
    }

    private void Update()
    {
        if (Time.timeScale == 0f || FirstLaunchTutorial.IsTutorialInputBlocked || !placed || mazeGenerator == null)
            return;

        // Kayma sürerken hiçbir girişi işleme: slide bitene kadar dokunuşları yok say.
        // (Aksi halde hareket sırasında ekrana dokunmak istenmeyen hareket üretiyordu.)
        if (isMoving)
        {
            AnimateMove();
            return;
        }

        // Sadece player DURUYORKEN giriş kabul et
        Vector3Int dir = ReadKeyboardDir();
        if (dir == Vector3Int.zero)
            dir = ReadSwipeDir();

        if (dir != Vector3Int.zero)
            TryMove(dir);
    }

    // --- Girişler ---

    private Vector3Int ReadKeyboardDir()
    {
        // Slide modelinde her basış = bir kayma. O yüzden GetKeyDown.
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            return new Vector3Int(1, 0, 0);
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            return new Vector3Int(-1, 0, 0);
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            return new Vector3Int(0, 1, 0);
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            return new Vector3Int(0, -1, 0);
        return Vector3Int.zero;
    }

    private Vector3Int ReadSwipeDir()
    {
        Vector3Int result = Vector3Int.zero;

        if (Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began)
            {
                if (
                    EventSystem.current != null
                    && EventSystem.current.IsPointerOverGameObject(t.fingerId)
                )
                {
                    dragging = false;
                    return result;
                }

                dragStart = t.position;
                dragging = true;
            }
            else if (t.phase == TouchPhase.Ended && dragging)
            {
                result = ReadTapOrSwipe(t.position, t.position - dragStart);
                dragging = false;
            }
            else if (t.phase == TouchPhase.Canceled)
            {
                dragging = false;
            }
        }
        else
        {
            if (Input.GetMouseButtonDown(0))
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                {
                    dragging = false;
                    return result;
                }

                dragStart = Input.mousePosition;
                dragging = true;
            }
            else if (Input.GetMouseButtonUp(0) && dragging)
            {
                Vector2 endPosition = Input.mousePosition;
                result = ReadTapOrSwipe(endPosition, endPosition - dragStart);
                dragging = false;
            }
        }

        return result;
    }

    private Vector3Int ReadTapOrSwipe(Vector2 endPosition, Vector2 delta)
    {
        if (delta.magnitude < swipeThreshold)
        {
            Vector3Int dotDirection = GetDotDirectionAtScreenPosition(endPosition);
            if (dotDirection != Vector3Int.zero)
                return dotDirection;
        }

        return SwipeToDir(delta);
    }

    private Vector3Int SwipeToDir(Vector2 delta)
    {
        if (delta.magnitude < swipeThreshold)
            return Vector3Int.zero; // küçük dokunuş = nokta tıklaması olabilir, swipe değil

        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            return delta.x > 0 ? new Vector3Int(1, 0, 0) : new Vector3Int(-1, 0, 0);
        return delta.y > 0 ? new Vector3Int(0, 1, 0) : new Vector3Int(0, -1, 0);
    }

    // --- Kayma (slide) hareketi ---

    // Verilen yönde, kavşağa / duvara / çıkışa kadar gidilecek hücre listesini hesaplar.
    private List<Vector3Int> ComputeSlidePath(Vector3Int startCell, Vector3Int startDir)
    {
        var path = new List<Vector3Int>();
        Vector3Int d = startDir;
        Vector3Int cur = startCell;

        // İlk adım yürünebilir değilse hiç kımıldama
        if (!mazeGenerator.IsCellWalkable(cur + d))
            return path;

        while (true)
        {
            Vector3Int next = cur + d;
            if (!mazeGenerator.IsCellWalkable(next))
                break; // önümüz duvar -> dur

            cur = next;
            path.Add(cur);

            if (cur == mazeGenerator.EndCell)
                break; // çıkışa vardık

            // Geldiğimiz yön hariç açık yönleri say
            Vector3Int back = -d;
            int exitCount = 0;
            Vector3Int onlyExit = Vector3Int.zero;
            foreach (var dir in Dirs)
            {
                if (dir == back)
                    continue;
                if (mazeGenerator.IsCellWalkable(cur + dir))
                {
                    exitCount++;
                    onlyExit = dir;
                }
            }

            if (exitCount == 1)
            {
                if (onlyExit == d)
                    continue; // düz koridor -> devam
                if (followCorners)
                {
                    d = onlyExit; // tek yol bir köşe -> otomatik dön
                    continue;
                }
                break; // followCorners kapalı -> köşede dur
            }

            // 0 = çıkmaz sokak, 2+ = kavşak -> dur ve seçimi kullanıcıya bırak
            break;
        }

        return path;
    }

    private void TryMove(Vector3Int dir)
    {
        List<Vector3Int> cells = ComputeSlidePath(currentCell, dir);
        if (cells.Count == 0)
            return; // o yön kapalı

        pathPoints.Clear();
        for (int i = 0; i < cells.Count; i++)
            pathPoints.Add(mazeGenerator.CellToWorld(cells[i]));

        pathIndex = 0;
        currentCell = cells[cells.Count - 1];
        isMoving = true;
        if (exitManager != null)
            exitManager.ReportPlayerMove();

        // Devam eden bir dokunma/swipe jestini iptal et ki kayma sırasında
        // başlamış bir jest sonradan istenmeyen harekete dönüşmesin.
        dragging = false;
        ClearDots();
    }

    private void AnimateMove()
    {
        float step = moveSpeed * Time.deltaTime;

        while (step > 0f && pathIndex < pathPoints.Count)
        {
            Vector3 target = pathPoints[pathIndex];
            Vector3 toTarget = target - transform.position;
            float dist = toTarget.magnitude;

            if (dist <= step)
            {
                transform.position = target;
                step -= dist;
                pathIndex++;
            }
            else
            {
                transform.position += toTarget.normalized * step;
                step = 0f;
            }
        }

        if (pathIndex >= pathPoints.Count)
        {
            isMoving = false;
            OnArrived();
        }
    }

    private void OnArrived()
    {
        if (currentCell == mazeGenerator.EndCell)
        {
            ClearDots();
            placed = false; // yeni maze gelene kadar girişi kilitle
            if (notifyOnComplete && exitManager != null)
                exitManager.OnMazeCompleted();
            return;
        }

        RefreshDots();
    }

    // --- Hareket noktaları ---

    private void RefreshDots()
    {
        ClearDots();
        if (!showMoveDots || dotSprite == null)
            return;

        int poolIndex = 0;
        foreach (var d in Dirs)
        {
            Vector3Int n = currentCell + d;
            if (!mazeGenerator.IsCellWalkable(n))
                continue;

            PooledDot dot = dotPool[poolIndex++];
            dot.direction = d;
            dot.gameObject.transform.position = mazeGenerator.CellToWorld(n);
            dot.gameObject.SetActive(true);
        }
    }

    private void ClearDots()
    {
        for (int i = 0; i < dotPool.Count; i++)
            dotPool[i].gameObject.SetActive(false);
    }

    private void InitializeDotPool()
    {
        dotPoolRoot = new GameObject("MoveDotsPool").transform;
        dotPoolRoot.SetParent(transform.parent, false);

        for (int i = 0; i < Dirs.Length; i++)
        {
            GameObject go = new GameObject($"MoveDot_{i}");
            go.transform.SetParent(dotPoolRoot, false);
            go.transform.localScale = Vector3.one * dotScale;

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = dotSprite;
            sr.color = dotColor;
            sr.sortingOrder = dotSortingOrder;

            go.SetActive(false);
            dotPool.Add(new PooledDot { gameObject = go });
        }
    }

    private Vector3Int GetDotDirectionAtScreenPosition(Vector2 screenPosition)
    {
        if (!mainCamera)
            mainCamera = Camera.main;
        if (!mainCamera)
            return Vector3Int.zero;

        for (int i = 0; i < dotPool.Count; i++)
        {
            PooledDot dot = dotPool[i];
            if (!dot.gameObject.activeSelf)
                continue;

            Vector3 worldPosition = dot.gameObject.transform.position;
            Vector3 screenCenter = mainCamera.WorldToScreenPoint(worldPosition);
            Vector3 screenEdge = mainCamera.WorldToScreenPoint(
                worldPosition + Vector3.right * dotTapRadius
            );
            float radiusPixels = Mathf.Abs(screenEdge.x - screenCenter.x);

            if (
                ((Vector2)screenCenter - screenPosition).sqrMagnitude
                <= radiusPixels * radiusPixels
            )
                return dot.direction;
        }

        return Vector3Int.zero;
    }

    // Boş bırakılırsa kullanılacak yumuşak beyaz daire sprite üret
    private Sprite GenerateDotSprite()
    {
        int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float r = size * 0.5f;
        Vector2 c = new Vector2(r, r);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c) / r;
                float a = Mathf.Clamp01(1f - dist);
                a = Mathf.SmoothStep(0f, 1f, a);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
