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
    public float AnimSpeed = 40f;

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
    private float cellW, cellH;      // 单个棋盘格的世界宽/高(保持照片宽高比, 严丝合缝)
    private float startTime;
    private bool timerRunning;
    private int currentImageIndex = 0;

    private AudioSource audioSource;
    private AudioSource bgmSource;     // 循环 BGM（与一次性音效分离）
    private AudioClip snapClip, winClip;
    private Camera mainCam;

    // === 视觉主题（菜单/对局统一色板） ===
    private readonly Color themeInk = new Color(0.14f, 0.17f, 0.24f);   // 主按钮文字：深蓝灰
    private readonly Color themePrimary = new Color(1f, 0.73f, 0.29f);  // 主按钮：暖橙
    private readonly Color themePrimaryPress = new Color(0.90f, 0.62f, 0.22f);
    private readonly Color themeGhost = new Color(1f, 1f, 1f, 0.16f);   // 次按钮：半透明白
    private readonly Color themeGhostPress = new Color(1f, 1f, 1f, 0.30f);
    private readonly Color themeSub = new Color(0.72f, 0.78f, 0.86f);   // 次级文字：蓝灰
    private readonly Color pageBg = new Color(0.10f, 0.12f, 0.17f);     // 页面背景：深蓝灰
    private Sprite roundedSprite;                                       // 程序生成圆角矩形（9-slice）
    private SpriteRenderer boardPlate;                                  // 棋盘底板（衬托拼图）
    private UnityEngine.UI.Image pressedBtnImg;                         // 当前按下的菜单按钮


    // Swipe
    private Vector2 touchStart;
    private bool isSwiping;

    // 长按看原图
    private SpriteRenderer ghost;   // 半透明原图叠加层
    private float pressTime;
    private bool isLongPress;

    // 模式：经典 / 每日挑战 / 我的照片
    private bool isDaily;
    private string dailyDate;
    private GameObject menuPanel;
    private RectTransform classicBtnRT, dailyBtnRT, photoBtnRT;
    private Sprite customPhotoSprite;   // 「我的照片」玩家自选拼图图

    void Start()
    {
        mainCam = Camera.main;
        // 强制相机视口比例 = 画布比例, 防止模拟器/真机 canvas 尺寸上报错乱导致的拉伸
        if (Screen.height > 0)
            mainCam.aspect = (float)Screen.width / Screen.height;
        // 页面背景：统一深蓝灰（白字/UI 的对比度底），替代默认灰蓝
        mainCam.clearFlags = CameraClearFlags.SolidColor;
        mainCam.backgroundColor = pageBg;
        EnsureUI();
        EnsureAudio();

        // 从 PlayerPrefs 读上次选择的难度, 并钳制到合法范围(3~5)
        gridSize = Mathf.Clamp(PlayerPrefs.GetInt("GridSize", 4), 3, 5);
        currentImageIndex = PlayerPrefs.GetInt("ImageIndex", 0);

        if (puzzleImages == null || puzzleImages.Length == 0)
        {
            // 无图则用默认photo
            var defaultSprite = Resources.Load<Sprite>("photo");
            if (defaultSprite != null) puzzleImages = new Sprite[] { defaultSprite };
        }

        gameState = GameState.Menu;
        InitBoard();
        UpdateBestRecord();
        EnsureMenu();
        UpdateStatusText("");

        // 启动完成上报（进入菜单、玩家可交互）——微信后台据此统计真实启动耗时，是启动优化的数据前提。
        // 插件侧逻辑：callMain 后 60s 内若未收到本上报，会打「[开发阶段提示]请使用自定义上报能力 WX.ReportGameStart」（线上版跳过该检查）。
        // WX 类仅存在于 WebGL / 微信小游戏平台（插件源码用 #if UNITY_WEBGL || WEIXINMINIGAME || UNITY_EDITOR 守卫），故此处同步守卫，
        // 避免 Android/iOS/Standalone 构建引用到不存在的类型。此处用全限定名，省掉一个会污染其他平台的条件 using。
#if UNITY_WEBGL || WEIXINMINIGAME
        WeChatWASM.WX.ReportGameStart();
#endif
    }

    void InitBoard()
    {
        // 兜底：任何情况下不允许空图集进初始化（空数组会让取模运算崩溃）
        if (puzzleImages == null || puzzleImages.Length == 0)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.SetPixels32(new[] { new Color32(72, 145, 220, 255), new Color32(72, 145, 220, 255),
                                   new Color32(56, 118, 178, 255), new Color32(56, 118, 178, 255) });
            tex.Apply();
            puzzleImages = new Sprite[] { Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f) };
        }

        // 清理旧拼图
        foreach (var p in pieces) if (p) Destroy(p);
        pieces.Clear();

        matrix = new Piece[gridSize, gridSize];

        Sprite img = customPhotoSprite != null
            ? customPhotoSprite
            : puzzleImages[currentImageIndex % puzzleImages.Length];
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

        // 长按看原图的半透明叠加层（尺寸跟随棋盘，见 ScalePieces）
        if (ghost != null) Destroy(ghost.gameObject);
        ghost = new GameObject("ghost-preview").AddComponent<SpriteRenderer>();
        ghost.sprite = img;
        ghost.sortingOrder = 10;
        ghost.color = new Color(1f, 1f, 1f, 0.55f);
        ghost.transform.position = Vector3.zero;
        ghost.enabled = false;

        // 棋盘底板：圆角深色板衬托拼块，给棋盘「容器」感（尺寸随棋盘，见 ScalePieces）
        if (boardPlate == null)
        {
            boardPlate = new GameObject("board-plate").AddComponent<SpriteRenderer>();
            boardPlate.sprite = RoundedSprite();
            boardPlate.drawMode = SpriteDrawMode.Sliced;   // 9-slice：任意尺寸不失圆角
            boardPlate.sortingOrder = -1;
            boardPlate.color = new Color(1f, 1f, 1f, 0.06f);
            boardPlate.transform.position = Vector3.zero;
        }

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
        // 视口比例变化(模拟器/真机 resize)时强制纠正相机 aspect 并重摆棋盘
        float targetAspect = (float)Screen.width / Mathf.Max(1, Screen.height);
        if (mainCam != null && Mathf.Abs(mainCam.aspect - targetAspect) > 0.001f)
        {
            mainCam.aspect = targetAspect;
            ScalePieces();
        }

        switch (gameState)
        {
            case GameState.Menu:
                // 菜单态自管点击（见 HandleMenuTap 注释）；按下先高亮按钮给即时反馈
                if (Input.GetMouseButtonDown(0))
                    SetMenuPressed(Input.mousePosition);
                if (Input.GetMouseButtonUp(0))
                {
                    RestoreMenuPressed();
                    HandleMenuTap(Input.mousePosition);
                }
                break;
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
                    UpdateStatusText($"步数 {moveCount} · 错位 {CountMisplaced()} · {Mathf.FloorToInt(elapsed/60):00}:{Mathf.FloorToInt(elapsed%60):00}");
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
            isLongPress = false;
            pressTime = 0f;
        }
        if (Input.GetMouseButton(0))
        {
            pressTime += Time.deltaTime;
            if (!isSwiping && !isLongPress && pressTime > 0.4f)
            {
                isLongPress = true;
                if (ghost != null) ghost.enabled = true;
            }
            Vector2 delta = (Vector2)Input.mousePosition - touchStart;
            if (delta.magnitude > 30f && !isSwiping)
            {
                isSwiping = true;
                if (isLongPress)
                {
                    isLongPress = false;
                    if (ghost != null) ghost.enabled = false;
                }
                HandleSwipe(delta);
            }
        }
        if (Input.GetMouseButtonUp(0))
        {
            if (isLongPress)
            {
                // 长按只看原图，松开不算走子
                isLongPress = false;
                if (ghost != null) ghost.enabled = false;
            }
            else if (!isSwiping)
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
            WeChatWASM.WX.VibrateShort(new WeChatWASM.VibrateShortOption());
            if (snapClip != null) audioSource.PlayOneShot(snapClip);
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

    int CountMisplaced()
    {
        var board = new int[gridSize, gridSize];
        for (int i = 0; i < gridSize; i++)
            for (int j = 0; j < gridSize; j++)
                board[i, j] = matrix[i, j] == null
                    ? -1
                    : matrix[i, j].OriginalI * gridSize + matrix[i, j].OriginalJ;
        return PuzzleRules.CountMisplaced(board, gridSize);
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
        if (winClip != null) audioSource.PlayOneShot(winClip);
        SpawnWinParticles();
        WeChatWASM.WX.VibrateLong(new WeChatWASM.VibrateLongOption());
        // 评级/错位计数逻辑抽到 PuzzleRules（纯逻辑，EditMode 可测）
        string rating = PuzzleRules.Rating(moveCount, elapsed, gridSize);
        string tail = "点击换图重玩";
        if (isDaily)
        {
            int streak = 1;
            string yesterday = System.DateTime.Now.AddDays(-1).ToString("yyyyMMdd");
            if (PlayerPrefs.GetString("daily_last", "") == yesterday)
                streak = PlayerPrefs.GetInt("daily_streak", 0) + 1;
            PlayerPrefs.SetString("daily_last", dailyDate);
            PlayerPrefs.SetInt("daily_streak", streak);
            PlayerPrefs.Save();
            tail = $"每日挑战完成！连续 {streak} 天\n点击返回菜单";
        }
        // 结算层级化：评级大字（金）→ 步数/用时 → 操作提示（灰），比三行同权重纯文本清晰
        // TMP 富文本直接生效；结算文案下移居中（对局态的顶部小字在场景重载时自动复位）
        UpdateStatusText(
            $"<size=96><color=#FFD166>评级 {rating}</color></size>\n" +
            $"<size=42>步数 {moveCount} · 用时 {Mathf.FloorToInt(elapsed/60):00}:{Mathf.FloorToInt(elapsed%60):00}</size>\n" +
            $"<size=30><color=#AAB4C0>{tail}</color></size>");
        var srt = statusText.rectTransform;
        srt.anchoredPosition = new Vector2(0, -330);
        srt.sizeDelta = new Vector2(1000, 760);
        SaveBestRecord(moveCount, elapsed);
        UpdateBestRecord();
    }

    void SaveBestRecord(int moves, float time)
    {
        string key = isDaily ? "best_daily" : $"best_{gridSize}";
        int bestMoves = PlayerPrefs.GetInt(key + "_moves", int.MaxValue);
        float bestTime = PlayerPrefs.GetFloat(key + "_time", float.MaxValue);
        if (moves < bestMoves) PlayerPrefs.SetInt(key + "_moves", moves);
        if (time < bestTime) PlayerPrefs.SetFloat(key + "_time", time);
        PlayerPrefs.Save();
    }

    void UpdateBestRecord()
    {
        if (bestRecordText == null) return;
        string key = isDaily ? "best_daily" : $"best_{gridSize}";
        int bm = PlayerPrefs.GetInt(key + "_moves", 0);
        float bt = PlayerPrefs.GetFloat(key + "_time", 0);
        if (bm > 0)
            bestRecordText.text = $"最佳: {bm}步 {Mathf.FloorToInt(bt/60):00}:{Mathf.FloorToInt(bt%60):00}";
        else
            bestRecordText.text = "最佳: 暂无";
    }

    // === 模式菜单（uGUI 视觉 + 自管命中测试） ===
    // 小游戏适配层的输入不走 uGUI EventSystem（注入/真机点击都到不了 Button.onClick），
    // 故保留 uGUI 层级做视觉，点击用项目自有的 Input 轮询通道做矩形命中。

    void EnsureMenu()
    {
        if (menuPanel != null) return;
        var canvas = statusText.transform.parent;
        menuPanel = new GameObject("MenuPanel", typeof(RectTransform));
        menuPanel.transform.SetParent(canvas, false);
        var mrt = (RectTransform)menuPanel.transform;
        mrt.anchorMin = Vector2.zero; mrt.anchorMax = Vector2.one;
        mrt.offsetMin = mrt.offsetMax = Vector2.zero;

        // 半透明压暗层（同时挡住对棋盘的误触）
        var dim = menuPanel.AddComponent<UnityEngine.UI.Image>();
        dim.color = new Color(0f, 0f, 0f, 0.45f);
        dim.raycastTarget = true;

        // 标题（与小程序注册名保持一致，不含个人姓名）
        MakeText("MenuTitle", "智慧拼图", 84, Color.white,
            new Vector2(0.5f, 1f), new Vector2(0, -420), new Vector2(900, 140));
        // 副标题：一句话说明玩法
        MakeText("MenuSub", "滑动拼块 · 还原原图", 34, themeSub,
            new Vector2(0.5f, 1f), new Vector2(0, -555), new Vector2(900, 60));

        // 主操作置顶：经典模式；次操作半透明白
        classicBtnRT = CreateMenuButton("Btn_Classic", "经典模式", "滑动还原 · 由易到难", -760, true);
        dailyBtnRT = CreateMenuButton("Btn_Daily", "每日挑战", "全网同局 · 4×4", -940, false);
        photoBtnRT = CreateMenuButton("Btn_Photo", "我的照片", "相册 / 拍照自定义", -1120, false);

        // 菜单态也展示最佳成绩：把底部常驻的最佳成绩文本提到遮罩之上
        bestRecordText.transform.SetAsLastSibling();
    }

    // 在菜单面板上创建一个 TMP 文本（局部工具函数，统一对齐/尺寸写法）
    void MakeText(string objName, string content, float size, Color color,
        Vector2 anchor, Vector2 pos, Vector2 sizeDelta)
    {
        var obj = new GameObject(objName, typeof(RectTransform));
        obj.transform.SetParent(menuPanel.transform, false);
        var t = obj.AddComponent<TextMeshProUGUI>();
        var rt = (RectTransform)obj.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = sizeDelta;
        t.alignment = TextAlignmentOptions.Center;
        t.fontSize = size;
        t.color = color;
        t.raycastTarget = false;
        t.text = content;
    }


    void HandleMenuTap(Vector2 screenPos)
    {
        RestoreMenuPressed();
        // 依次命中「每日挑战 / 我的照片」；其余任意位置兜底进经典模式
        // （保证任何情况下点一下就能开局，按钮命中失败也不会卡在菜单）
        if (dailyBtnRT != null &&
            RectTransformUtility.RectangleContainsScreenPoint(dailyBtnRT, screenPos, null))
            OnDaily();
        else if (photoBtnRT != null &&
                 RectTransformUtility.RectangleContainsScreenPoint(photoBtnRT, screenPos, null))
            OnMyPhoto();
        else
            OnClassic();
    }

    // 菜单按钮按压反馈：按下变深，抬起恢复（提升可点性感知）
    Color pressedBase;
    void SetMenuPressed(Vector2 screenPos)
    {
        pressedBtnImg = null;
        var (img, primary) = MenuBtnAt(screenPos);
        if (img == null) return;
        pressedBtnImg = img;
        pressedBase = img.color;
        img.color = primary ? themePrimaryPress : themeGhostPress;
    }

    void RestoreMenuPressed()
    {
        if (pressedBtnImg != null) pressedBtnImg.color = pressedBase;
        pressedBtnImg = null;
    }

    (UnityEngine.UI.Image, bool) MenuBtnAt(Vector2 screenPos)
    {
        if (dailyBtnRT != null &&
            RectTransformUtility.RectangleContainsScreenPoint(dailyBtnRT, screenPos, null))
            return (dailyBtnRT.GetComponent<UnityEngine.UI.Image>(), false);
        if (photoBtnRT != null &&
            RectTransformUtility.RectangleContainsScreenPoint(photoBtnRT, screenPos, null))
            return (photoBtnRT.GetComponent<UnityEngine.UI.Image>(), false);
        if (classicBtnRT != null &&
            RectTransformUtility.RectangleContainsScreenPoint(classicBtnRT, screenPos, null))
            return (classicBtnRT.GetComponent<UnityEngine.UI.Image>(), true);
        return (null, false);
    }


    RectTransform CreateMenuButton(string name, string label, string sub, float y, bool primary)
    {
        var btnObj = new GameObject(name, typeof(RectTransform),
            typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
        btnObj.transform.SetParent(menuPanel.transform, false);
        var brt = (RectTransform)btnObj.transform;
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
        brt.anchoredPosition = new Vector2(0, y);
        brt.sizeDelta = new Vector2(640, 150);

        // 圆角按钮：程序生成 9-slice 圆角矩形，主按钮实心主题色 / 次按钮半透明白
        var img = btnObj.GetComponent<UnityEngine.UI.Image>();
        img.sprite = RoundedSprite();
        img.type = UnityEngine.UI.Image.Type.Sliced;
        img.color = primary ? themePrimary : themeGhost;

        // 主文字（有副标题时略上移）
        var txtObj = new GameObject("Text", typeof(RectTransform));
        txtObj.transform.SetParent(btnObj.transform, false);
        var txt = txtObj.AddComponent<TextMeshProUGUI>();
        var trt = (RectTransform)txtObj.transform;
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
        trt.sizeDelta = new Vector2(600, 70);
        trt.anchoredPosition = new Vector2(0, sub != null ? 22 : 0);
        txt.alignment = TextAlignmentOptions.Center;
        txt.fontSize = 46;
        txt.color = primary ? themeInk : Color.white;
        txt.raycastTarget = false;
        txt.text = label;

        // 副标题：说明该模式的玩法/范围
        if (sub != null)
        {
            var subObj = new GameObject("Sub", typeof(RectTransform));
            subObj.transform.SetParent(btnObj.transform, false);
            var st = subObj.AddComponent<TextMeshProUGUI>();
            var srt = (RectTransform)subObj.transform;
            srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
            srt.sizeDelta = new Vector2(600, 36);
            srt.anchoredPosition = new Vector2(0, -38);
            st.alignment = TextAlignmentOptions.Center;
            st.fontSize = 22;
            st.color = primary ? new Color(0.42f, 0.33f, 0.14f) : themeSub;
            st.raycastTarget = false;
            st.text = sub;
        }
        return brt;
    }

    // 程序生成圆角矩形 sprite（96×48 @100PPU，四边 16px 圆角），按 9-slice 拉伸复用
    Sprite RoundedSprite()
    {
        if (roundedSprite != null) return roundedSprite;
        int w = 96, h = 48, rad = 16;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color32[w * h];
        const float aa = 1.5f;   // 边缘抗锯齿宽度（像素）
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                // 圆角矩形 SDF：点到「内芯矩形」的距离减去圆角半径，<0 在内部
                float cx = Mathf.Clamp(x, rad, w - rad);
                float cy = Mathf.Clamp(y, rad, h - rad);
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - rad;
                float a = Mathf.Clamp01(0.5f - d / aa);
                px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(px);
        tex.Apply();
        roundedSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f,
            0, SpriteMeshType.FullRect, new Vector4(rad, rad, rad, rad));
        return roundedSprite;
    }


    void HideMenu() { if (menuPanel != null) menuPanel.SetActive(false); }

    void OnClassic()
    {
        isDaily = false;
        gridSize = Mathf.Clamp(PlayerPrefs.GetInt("GridSize", 4), 3, 5);
        BeginRound();
    }

    void OnDaily()
    {
        isDaily = true;
        dailyDate = System.DateTime.Now.ToString("yyyyMMdd");
        gridSize = 4;   // 每日挑战固定 4×4，保证全网同局
        BeginRound();
    }

    void OnMyPhoto()
    {
        // 相册/拍照选一张图，直接变成拼图（不经 CDN、零包体成本）
        WeChatWASM.WX.ChooseImage(new WeChatWASM.ChooseImageOption
        {
            count = 1,
            sizeType = new[] { "compressed" },
            sourceType = new[] { "album", "camera" },
            success = res =>
            {
                var paths = res.tempFilePaths;
                if (paths == null || paths.Length == 0) return;
                byte[] bytes = WeChatWASM.WX.GetFileSystemManager().ReadFileSync(paths[0]);
                if (bytes == null || bytes.Length == 0) return;
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(bytes)) return;
                customPhotoSprite = Sprite.Create(tex,
                    new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f), tex.width / 2f);
                isDaily = false;
                gridSize = Mathf.Clamp(PlayerPrefs.GetInt("GridSize", 4), 3, 5);
                BeginRound();
            }
        });
    }

    void BeginRound()
    {
        HideMenu();
        InitBoard();
        if (isDaily)
            ApplyBoard(PuzzleRules.ShuffledBoard(gridSize, int.Parse(dailyDate)));
        else
            Shuffle();
        moveCount = 0;
        gameState = GameState.Playing;
        startTime = Time.time;
        timerRunning = true;
        int streak = PlayerPrefs.GetInt("daily_streak", 0);
        UpdateStatusText(isDaily
            ? (streak > 0 ? $"每日挑战！连续 {streak} 天" : "每日挑战！")
            : "");
    }

    // 按布局数组落位（board[i,j]=拼图块编号，-1 为空格）——每日挑战的确定性洗牌
    void ApplyBoard(int[,] board)
    {
        var placements = new List<(Piece p, int i, int j)>();
        for (int i = 0; i < gridSize; i++)
            for (int j = 0; j < gridSize; j++)
            {
                int t = board[i, j];
                if (t < 0) continue;
                var p = FindPieceByOriginal(t / gridSize, t % gridSize);
                if (p != null) placements.Add((p, i, j));
            }
        foreach (var (p, _, _) in placements)
            matrix[p.CurrentI, p.CurrentJ] = null;
        foreach (var (p, i, j) in placements)
        {
            matrix[i, j] = p;
            p.CurrentI = i; p.CurrentJ = j;
            p.GameObject.transform.position = GetCellWorldPos(i, j);
        }
    }

    Piece FindPieceByOriginal(int oi, int oj)
    {
        for (int i = 0; i < gridSize; i++)
            for (int j = 0; j < gridSize; j++)
                if (matrix[i, j] != null && matrix[i, j].OriginalI == oi && matrix[i, j].OriginalJ == oj)
                    return matrix[i, j];
        return null;
    }

    // === UI / Audio / FX ===

    void EnsureUI()
    {
        if (statusText == null)
        {
            var canvas = new GameObject("UICanvas", typeof(Canvas), typeof(CanvasScaler));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            if (UnityEngine.EventSystems.EventSystem.current == null)
                new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.EventSystems.StandaloneInputModule));
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

        // AI 生成音频（Assets/Resources/Audio）；缺失时优雅回退到合成音
        var bgm = Resources.Load<AudioClip>("Audio/bgm");
        if (bgm != null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.clip = bgm;
            bgmSource.loop = true;
            bgmSource.volume = 0.6f;
            bgmSource.Play();
        }
        snapClip = Resources.Load<AudioClip>("Audio/snap");
        winClip = Resources.Load<AudioClip>("Audio/win");
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
        // 用相机视口真实世界边界, 不依赖 Screen 尺寸(模拟器/真机会报错值)
        var bl = mainCam.ViewportToWorldPoint(new Vector3(0, 0, 0));
        var tr = mainCam.ViewportToWorldPoint(new Vector3(1, 1, 0));
        float worldW = Mathf.Abs(tr.x - bl.x);
        float worldH = Mathf.Abs(tr.y - bl.y);
        float pieceW = sr.sprite.bounds.size.x;
        float pieceH = sr.sprite.bounds.size.y;
        // 棋盘保持照片宽高比(块是长方形), 在视口内取最大可容纳尺寸 → 块与块严丝合缝
        float maxBoardW = Mathf.Min(worldW, worldH * pieceW / pieceH);
        cellW = maxBoardW / gridSize;
        cellH = cellW * pieceH / pieceW;
        foreach (var p in pieces)
        {
            p.transform.localScale = new Vector3(cellW / pieceW, cellH / pieceH, 1f);
            var piece = GetPieceByGO(p);
            if (piece != null)
                p.transform.position = GetCellWorldPos(piece.CurrentI, piece.CurrentJ);
        }

        if (ghost != null && ghost.sprite != null)
        {
            var b = ghost.sprite.bounds;
            ghost.transform.localScale = new Vector3(
                cellW * gridSize / b.size.x, cellH * gridSize / b.size.y, 1f);
        }

        // 底板随棋盘缩放：四周各留 0.25 世界单位内边距
        if (boardPlate != null)
            boardPlate.size = new Vector2(cellW * gridSize + 0.5f, cellH * gridSize + 0.5f);
    }

    Piece GetPieceByGO(GameObject go)
    {
        for (int i = 0; i < gridSize; i++)
            for (int j = 0; j < gridSize; j++)
                if (matrix[i, j] != null && matrix[i, j].GameObject == go)
                    return matrix[i, j];
        return null;
    }

    Vector3 GetCellWorldPos(int i, int j)
    {
        // 棋盘以屏幕中心为原点, 格子尺寸统一 → 块与块严丝合缝
        float x = (j - (gridSize - 1) / 2f) * cellW;
        float y = ((gridSize - 1) / 2f - i) * cellH;
        return new Vector3(x, y, 0);
    }
}
