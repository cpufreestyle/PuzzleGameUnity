using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Assets.Scripts;

public class Game : MonoBehaviour
{
    [Header("Pieces")]
    public GameObject piecePrefab;
    public Sprite[] puzzleImages;
    public float AnimSpeed = 14f;

    [Header("UI (auto-created if null)")]
    public TMP_Text statusText;
    public TMP_Text bestRecordText;

    private GameState gameState;
    private int gridSize = 4;
    private List<GameObject> pieces = new List<GameObject>();
    private Piece[,] matrix;
    private Piece pieceToAnimate;
    private int toAnimateI, toAnimateJ;
    private Vector3 screenPosToAnimate;

    private int moveCount;
    private float startTime;
    private bool timerRunning;
    private int currentImageIndex = 0;

    private AudioSource audioSource;
    private Camera mainCam;

    // Swipe
    private Vector2 touchStart;
    private bool isSwiping;

    void Start()
    {
        mainCam = Camera.main;
        EnsureUI();
        EnsureAudio();

        // 从 PlayerPrefs 读上次选择的难度
        gridSize = PlayerPrefs.GetInt("GridSize", 4);
        currentImageIndex = PlayerPrefs.GetInt("ImageIndex", 0);

        if (puzzleImages == null || puzzleImages.Length == 0)
        {
            // 无图则用默认photo
            var defaultSprite = Resources.Load<Sprite>("photo");
            if (defaultSprite != null) puzzleImages = new Sprite[] { defaultSprite };
        }

        gameState = GameState.Start;
        InitBoard();
        UpdateBestRecord();
        UpdateStatusText("点击屏幕开始！");
    }

    void InitBoard()
    {
        // 清理旧拼图
        foreach (var p in pieces) if (p) Destroy(p);
        pieces.Clear();

        matrix = new Piece[gridSize, gridSize];
        float cellSize = 1f / gridSize;

        Sprite img = puzzleImages[currentImageIndex % puzzleImages.Length];
        int idx = 0;

        // 创建 gridSize*gridSize-1 个拼图块
        for (int i = 0; i < gridSize; i++)
        {
            for (int j = 0; j < gridSize; j++)
            {
                if (i == gridSize - 1 && j == gridSize - 1) continue; // 右下角为空

                var obj = piecePrefab != null
                    ? Instantiate(piecePrefab)
                    : CreatePieceObject(img, i, j, gridSize);

                if (piecePrefab != null)
                    SetupPieceSprite(obj, img, i, j, gridSize);

                obj.name = $"piece-{i}-{j}";
                obj.transform.position = GetCellWorldPos(i, j);

                if (obj.GetComponent<BoxCollider2D>() == null)
                    obj.AddComponent<BoxCollider2D>();

                var piece = new Piece { GameObject = obj, OriginalI = i, OriginalJ = j, CurrentI = i, CurrentJ = j };
                matrix[i, j] = piece;
                pieces.Add(obj);
                idx++;
            }
        }
        matrix[gridSize - 1, gridSize - 1] = null; // 空格
        ScalePieces();
    }

    GameObject CreatePieceObject(Sprite sprite, int row, int col, int size)
    {
        var obj = new GameObject($"piece-{row}-{col}");
        var sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        // 裁剪 sprite 的 1/size 区域
        Rect subRect = new Rect(
            sprite.texture.width * col / size,
            sprite.texture.height * (size - 1 - row) / size,
            sprite.texture.width / size,
            sprite.texture.height / size
        );
        var subSprite = Sprite.Create(sprite.texture, subRect, new Vector2(0.5f, 0.5f), sprite.texture.width / 2f);
        sr.sprite = subSprite;
        return obj;
    }

    void SetupPieceSprite(GameObject obj, Sprite sprite, int row, int col, int size)
    {
        var sr = obj.GetComponent<SpriteRenderer>();
        if (sr == null) sr = obj.AddComponent<SpriteRenderer>();
        Rect subRect = new Rect(
            sprite.texture.width * col / size,
            sprite.texture.height * (size - 1 - row) / size,
            sprite.texture.width / size,
            sprite.texture.height / size
        );
        sr.sprite = Sprite.Create(sprite.texture, subRect, new Vector2(0.5f, 0.5f), sprite.texture.width / 2f);
    }

    void Update()
    {
        switch (gameState)
        {
            case GameState.Start:
                if (Input.GetMouseButtonUp(0))
                {
                    Shuffle();
                    gameState = GameState.Playing;
                    startTime = Time.time;
                    timerRunning = true;
                    UpdateStatusText("");
                }
                break;
            case GameState.Playing:
                if (timerRunning)
                {
                    float elapsed = Time.time - startTime;
                    UpdateStatusText($"步数: {moveCount}   {Mathf.FloorToInt(elapsed/60):00}:{Mathf.FloorToInt(elapsed%60):00}");
                }
                CheckInput();
                break;
            case GameState.Animating:
                AnimateMovement(pieceToAnimate, Time.deltaTime);
                CheckAnimationEnd();
                break;
            case GameState.End:
                if (Input.GetMouseButtonUp(0))
                {
                    // 换图重玩
                    currentImageIndex = (currentImageIndex + 1) % (puzzleImages != null ? puzzleImages.Length : 1);
                    PlayerPrefs.SetInt("ImageIndex", currentImageIndex);
                    SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                }
                break;
        }
    }

    void CheckInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            touchStart = Input.mousePosition;
            isSwiping = false;
        }
        if (Input.GetMouseButton(0))
        {
            Vector2 delta = (Vector2)Input.mousePosition - touchStart;
            if (delta.magnitude > 30f && !isSwiping)
            {
                isSwiping = true;
                HandleSwipe(delta);
            }
        }
        if (Input.GetMouseButtonUp(0))
        {
            if (!isSwiping)
                HandleTap();
            isSwiping = false;
        }
    }

    void HandleSwipe(Vector2 delta)
    {
        int dx = Mathf.Abs(delta.x) > Mathf.Abs(delta.y) ? (delta.x > 0 ? 1 : -1) : 0;
        int dy = dx == 0 ? (delta.y > 0 ? 1 : -1) : 0;

        // 找空格位置
        int emptyI = -1, emptyJ = -1;
        FindEmpty(out emptyI, out emptyJ);

        // 滑动方向 = 移动哪个块到空格
        int fromI = emptyI - dy; // 向上滑=空格上方块下移
        int fromJ = emptyJ + dx; // 向右滑=空格右侧块左移...实际相反
        // 修正: 手指滑动方向 = 拼图块移动方向
        fromI = emptyI + (dy > 0 ? -1 : dy < 0 ? 1 : 0);
        fromJ = emptyJ + (dx > 0 ? -1 : dx < 0 ? 1 : 0);

        if (fromI >= 0 && fromI < gridSize && fromJ >= 0 && fromJ < gridSize && matrix[fromI, fromJ] != null)
        {
            StartMove(fromI, fromJ, emptyI, emptyJ);
        }
    }

    void HandleTap()
    {
        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
        var hit = Physics2D.Raycast(ray.origin, ray.direction);
        if (hit.collider == null) return;

        string[] parts = hit.collider.gameObject.name.Split('-');
        if (parts.Length < 3) return;
        int iPart = int.Parse(parts[1]);
        int jPart = int.Parse(parts[2]);

        int iFound = -1, jFound = -1;
        for (int i = 0; i < gridSize && iFound == -1; i++)
            for (int j = 0; j < gridSize && iFound == -1; j++)
                if (matrix[i, j] != null && matrix[i, j].OriginalI == iPart && matrix[i, j].OriginalJ == jPart)
                { iFound = i; jFound = j; }

        if (iFound == -1) return;

        // 找相邻空格
        int[] di = { -1, 0, 1, 0 };
        int[] dj = { 0, -1, 0, 1 };
        for (int d = 0; d < 4; d++)
        {
            int ni = iFound + di[d], nj = jFound + dj[d];
            if (ni >= 0 && ni < gridSize && nj >= 0 && nj < gridSize && matrix[ni, nj] == null)
            {
                StartMove(iFound, jFound, ni, nj);
                break;
            }
        }
    }

    void StartMove(int fromI, int fromJ, int toI, int toJ)
    {
        toAnimateI = toI;
        toAnimateJ = toJ;
        screenPosToAnimate = GetCellWorldPos(toI, toJ);
        pieceToAnimate = matrix[fromI, fromJ];
        gameState = GameState.Animating;
        PlaySound(440f, 0.06f, 0.12f);
    }

    void AnimateMovement(Piece toMove, float dt)
    {
        toMove.GameObject.transform.position = Vector2.MoveTowards(
            toMove.GameObject.transform.position, screenPosToAnimate, dt * AnimSpeed);
    }

    void CheckAnimationEnd()
    {
        if (Vector2.Distance(pieceToAnimate.GameObject.transform.position, screenPosToAnimate) < 0.1f)
        {
            SwapPositions(pieceToAnimate.CurrentI, pieceToAnimate.CurrentJ, toAnimateI, toAnimateJ);
            moveCount++;
            gameState = GameState.Playing;
            CheckVictory();
        }
    }

    void SwapPositions(int i1, int j1, int i2, int j2)
    {
        var tmp = matrix[i1, j1];
        matrix[i1, j1] = matrix[i2, j2];
        matrix[i2, j2] = tmp;

        if (matrix[i1, j1] != null) { matrix[i1, j1].CurrentI = i1; matrix[i1, j1].CurrentJ = j1; matrix[i1, j1].GameObject.transform.position = GetCellWorldPos(i1, j1); }
        if (matrix[i2, j2] != null) { matrix[i2, j2].CurrentI = i2; matrix[i2, j2].CurrentJ = j2; matrix[i2, j2].GameObject.transform.position = GetCellWorldPos(i2, j2); }
    }

    void FindEmpty(out int ei, out int ej)
    {
        ei = ej = -1;
        for (int i = 0; i < gridSize; i++)
            for (int j = 0; j < gridSize; j++)
                if (matrix[i, j] == null) { ei = i; ej = j; return; }
    }

    void Shuffle()
    {
        for (int n = 0; n < gridSize * gridSize * 10; n++)
        {
            FindEmpty(out int ei, out int ej);
            var dirs = new List<(int, int)>();
            if (ei > 0) dirs.Add((-1, 0));
            if (ei < gridSize - 1) dirs.Add((1, 0));
            if (ej > 0) dirs.Add((0, -1));
            if (ej < gridSize - 1) dirs.Add((0, 1));
            var (di, dj) = dirs[Random.Range(0, dirs.Count)];
            SwapPositions(ei + di, ej + dj, ei, ej);
        }
    }

    void CheckVictory()
    {
        for (int i = 0; i < gridSize; i++)
            for (int j = 0; j < gridSize; j++)
            {
                if (matrix[i, j] == null) continue;
                if (matrix[i, j].CurrentI != matrix[i, j].OriginalI ||
                    matrix[i, j].CurrentJ != matrix[i, j].OriginalJ)
                    return;
            }

        gameState = GameState.End;
        timerRunning = false;
        float elapsed = Time.time - startTime;
        StartCoroutine(PlayWinSound());
        SpawnWinParticles();
        UpdateStatusText($"🎉 通关！\n步数: {moveCount}  时间: {Mathf.FloorToInt(elapsed/60):00}:{Mathf.FloorToInt(elapsed%60):00}\n点击换图重玩");
        SaveBestRecord(moveCount, elapsed);
        UpdateBestRecord();
    }

    void SaveBestRecord(int moves, float time)
    {
        string key = $"best_{gridSize}";
        int bestMoves = PlayerPrefs.GetInt(key + "_moves", int.MaxValue);
        float bestTime = PlayerPrefs.GetFloat(key + "_time", float.MaxValue);
        if (moves < bestMoves) PlayerPrefs.SetInt(key + "_moves", moves);
        if (time < bestTime) PlayerPrefs.SetFloat(key + "_time", time);
        PlayerPrefs.Save();
    }

    void UpdateBestRecord()
    {
        if (bestRecordText == null) return;
        string key = $"best_{gridSize}";
        int bm = PlayerPrefs.GetInt(key + "_moves", 0);
        float bt = PlayerPrefs.GetFloat(key + "_time", 0);
        if (bm > 0)
            bestRecordText.text = $"最佳: {bm}步 {Mathf.FloorToInt(bt/60):00}:{Mathf.FloorToInt(bt%60):00}";
        else
            bestRecordText.text = "最佳: 暂无";
    }

    // === UI / Audio / FX ===

    void EnsureUI()
    {
        if (statusText == null)
        {
            var canvas = new GameObject("UICanvas", typeof(Canvas), typeof(CanvasScaler));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var sc = canvas.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1080, 1920);

            var tObj = new GameObject("StatusText", typeof(RectTransform));
            tObj.transform.SetParent(canvas.transform, false);
            statusText = tObj.AddComponent<TextMeshProUGUI>();
            var rt = tObj.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -50);
            rt.sizeDelta = new Vector2(900, 100);
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.fontSize = 32;
            statusText.color = Color.white;
            statusText.raycastTarget = false;

            var brObj = new GameObject("BestRecord", typeof(RectTransform));
            brObj.transform.SetParent(canvas.transform, false);
            bestRecordText = brObj.AddComponent<TextMeshProUGUI>();
            var brt = brObj.GetComponent<RectTransform>();
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0f);
            brt.anchoredPosition = new Vector2(0, 30);
            brt.sizeDelta = new Vector2(800, 60);
            bestRecordText.alignment = TextAlignmentOptions.Center;
            bestRecordText.fontSize = 24;
            bestRecordText.color = new Color(1f, 0.85f, 0f);
            bestRecordText.raycastTarget = false;
        }
    }

    void EnsureAudio()
    {
        audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
    }

    void PlaySound(float freq, float dur, float vol = 0.25f)
    {
        if (audioSource == null) return;
        int len = (int)(dur * 22050);
        var clip = AudioClip.Create("sfx", len, 1, 22050, false);
        var s = new float[len];
        for (int i = 0; i < len; i++)
            s[i] = Mathf.Sin(2 * Mathf.PI * freq * i / 22050f) * vol * (1f - (float)i / len);
        clip.SetData(s, 0);
        audioSource.PlayOneShot(clip);
    }

    IEnumerator PlayWinSound()
    {
        float[] notes = { 523, 659, 784, 1047, 1319 };
        for (int i = 0; i < notes.Length; i++)
        {
            PlaySound(notes[i], 0.25f, 0.3f);
            yield return new WaitForSeconds(0.13f);
        }
    }

    void SpawnWinParticles()
    {
        var ps = new GameObject("WinParticles", typeof(ParticleSystem));
        var p = ps.GetComponent<ParticleSystem>();
        var main = p.main;
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.2f), new Color(0.2f, 0.8f, 1f));
        main.startSize = 0.25f;
        main.startSpeed = 4f;
        main.startLifetime = 2f;
        main.maxParticles = 120;
        var em = p.emission;
        em.rateOverTime = 0;
        em.SetBurst(0, new ParticleSystem.Burst(0f, 120));
        var shape = p.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.1f;
        var vel = p.velocityOverLifetime;
        vel.y = new ParticleSystem.MinMaxCurve(2f);
        Destroy(ps, 4f);
    }

    void UpdateStatusText(string msg) { if (statusText) statusText.text = msg; }

    void ScalePieces()
    {
        if (pieces.Count == 0) return;
        var sr = pieces[0].GetComponent<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return;
        float screenH = mainCam.orthographicSize * 2f;
        float screenW = screenH / Screen.height * Screen.width;
        float pieceW = sr.sprite.bounds.size.x;
        float scale = Mathf.Min(screenW / (pieceW * gridSize), screenH / (pieceW * gridSize)) * 0.9f;
        foreach (var p in pieces)
            p.transform.localScale = new Vector3(scale, scale, 1f);
    }

    Vector3 GetCellWorldPos(int i, int j)
    {
        float step = 1f / gridSize;
        Vector3 point = mainCam.ViewportToWorldPoint(new Vector3(step * j + step * 0.5f, 1 - step * i - step * 0.5f, 0));
        point.z = 0;
        return point;
    }
}
