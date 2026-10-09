# Tower Defense Codex

> **Tài liệu tham chiếu kỹ thuật · đọc từ mã nguồn 25/08/2026**
>
> Toàn bộ kiến trúc, thuật toán, đánh đổi thiết kế và định nghĩa đang dùng trong project —
> dựng lại từ việc đọc thẳng 114 file mã nguồn, không dựa vào changelog.

| Chỉ số | Giá trị |
|---|---|
| File C# | 116 |
| Dòng code | 7.886 |
| Trong đó code chết | ~750 dòng |
| Scene | 3 |
| Config SO | 6 |
| Event bus | 14 event |
| Commit | 74 |

## Mục lục

1. [Kiến trúc bốn tầng](#01--kiến-trúc-bốn-tầng)
2. [Vòng đời khởi động](#02--vòng-đời-khởi-động)
3. [Grid và hệ toạ độ](#03--grid-và-hệ-toạ-độ)
4. [Sinh map — Maze, A*, đa cổng](#04--sinh-map--maze-a-đa-cổng)
5. [Wave engine](#05--wave-engine)
6. [Hệ chiến đấu](#06--hệ-chiến-đấu)
7. [Deploy và Retreat](#07--deploy-và-retreat)
8. [Event bus, VFX và Audio](#08--event-bus-vfx-và-audio)
9. [Tầng UI](#09--tầng-ui)
10. [Data-driven config](#10--data-driven-config)
11. [Đánh đổi thiết kế](#11--đánh-đổi-thiết-kế)
12. [Từ điển thuật ngữ](#12--từ-điển-thuật-ngữ)
13. [Nợ kỹ thuật và bug](#13--nợ-kỹ-thuật-và-bug)
14. [Bản đồ file](#14--bản-đồ-file)

---

## 01 · Kiến trúc bốn tầng

> Nguyên tắc chi phối toàn bộ codebase: tầng Control là C# thuần, không kế thừa MonoBehaviour. Mọi thứ khác là hệ quả của lựa chọn đó.

| Tầng | Vai trò | File tiêu biểu |
|---|---|---|
| **Model** | Dữ liệu và cấu hình. Không biết gì về scene, không tham chiếu GameObject. | `TDGridDTO` · `TDPathGroup` · `OperatorData` · `TowerData` · `EnemyData` · `LevelConfig` · `TDConstant` |
| **Control** | Logic thuần C#, truy cập qua property tĩnh `.api`. Không có `Awake/Update`. | `TDEnemyPathMainControl` · `TDMazePathGenerator` · `TDGoldControl` · `TDTowerMainControl` · `TDOperatorRegistry` |
| **View** | MonoBehaviour. Chỉ hiển thị, nhận input, và gọi xuống Control. | `TDEnemyView` · `TDOperatorView` · `TDGameplayHUDView` · `TDDiamondPanelView` · `TDStageSelectView` |
| **Services** | Trục ngang cắt qua mọi tầng. Static, sống suốt vòng đời app. | `TDGameEventBus` · `TDEffectManager` · `TDAudioService` · `TDResourceObject` |

### Vì sao Control không phải MonoBehaviour

Lợi ích thực tế, theo thứ tự quan trọng:

- **Không phụ thuộc vòng đời Unity.** Logic wave, pathfinding, gold, state không cần một GameObject nào tồn tại. Khi reload scene, chỉ cần `new` lại object thay vì đi tìm component trong hierarchy.
- **Kiểm soát được thứ tự khởi tạo.** `TDControl.InitOtherControl()` dựng đúng 18 control theo thứ tự tường minh. Nếu là MonoBehaviour thì thứ tự `Awake` do Unity quyết định — nguồn bug kinh điển.
- **Về lý thuyết là testable.** Không vướng Unity lifecycle nên viết test không cần dựng scene. Thực tế chưa có test nào, nhưng rào cản kỹ thuật đã được gỡ.

### Cái giá phải trả

Hơn 20 class dùng singleton tĩnh `.api`. Đây là **hidden dependency**: đọc chữ ký một hàm không biết nó phụ thuộc vào cái gì, và không mock được. Đổi lại là truy cập gọn và zero boilerplate. Ở quy mô một người làm thì đánh đổi này hợp lý; nhiều người làm song song thì nó vỡ (chi tiết ở mục [11](#11--đánh-đổi-thiết-kế)).

> **CHI TIẾT DỄ BỎ SÓT**
>
> Trong `InitOtherControl()`, chỉ duy nhất `TDGameStateControl` dùng `??=`:
>
> ```csharp
> TDPauseControl.api     = new TDPauseControl();       // tạo mới mỗi lần
> TDGameStateControl.api ??= new TDGameStateControl();  // GIỮ instance cũ
> ```
>
> Lý do: nó giữ `SelectedStageId`. Nếu tạo mới thì bấm "Next" sau khi thắng sẽ mất màn đã chọn. Đây là chủ đích, không phải nhầm.

---

## 02 · Vòng đời khởi động

> Ba scene, nạp chồng (additive), không bao giờ nạp đơn lẻ. Đây là chuỗi phải thuộc — phần lớn bug về sau đều là bug thứ tự.

1. **SCENE · Init** — `InitView.Start()` bật `runInBackground`, đặt `targetFrameRate = 60`, nạp `DTLoadFirst` additive rồi tự huỷ scene `Init`.
2. **SCENE · DTLoadFirst · vĩnh viễn** — `TDLoadFirst.Start()` → `TDControl.api.Init()`. Scene này giữ `TDResourceObject` (registry asset), `TDSceneController` (camera menu + lớp fade), và không bao giờ bị unload.
3. **BOOTSTRAP** — `TDAudioService.Init()` dựng BGM player + pool 8 AudioSource (`DontDestroyOnLoad`) → `InitOtherControl()` dựng 18 control → `TDEffectManager.Init()` + `TDGameplayAudioContext.Init()`.
4. **SCENE · DTMainMenu** — Nạp additive. `TDSceneController.OnSceneLoaded` bật camera menu và kích BGM menu. Main Menu → Stage Select (cùng scene, chỉ đổi `CanvasGroup`).
5. **CHUYỂN CẢNH** — `GoToGameplay(stageId)`: fade đen → unload menu → `ReinitControls()` → `SelectStage(stageId)` → nạp `DTGamePlay` → fade sáng.
6. **SCENE · DTGamePlay** — `TDGameplayMainView.Start()` → `TDInitializeModel.Reset()` → đọc kích thước map từ Renderer → dựng grid → `TDEnemyPathMainView.Initialize()`.

### Điểm mấu chốt: thứ tự SelectStage

`SelectStage()` phải được gọi **sau** `ReinitControls()`, không phải trước:

```csharp
// TDSceneController.RetryGameplayAsync
await FadeToBlack();
await WaitForOp(SceneManager.UnloadSceneAsync(SCENE_GAMEPLAY));
TDControl.api.ReinitControls();                       // ← dựng lại control
if (nextStageId != null)
    TDGameStateControl.api.SelectStage(nextStageId);  // ← RỒI mới chọn màn
await WaitForOp(SceneManager.LoadSceneAsync(SCENE_GAMEPLAY, Additive));
```

Đảo thứ tự thì bấm "Next" sẽ nạp lại đúng màn vừa thắng. Từng là bug thật.

### Chống kẹt khi chuyển cảnh

Popup Victory/GameOver gọi `TDPauseControl.Pause()` → `Time.timeScale = 0`. Mọi tween không gắn `SetUpdate(true)` sẽ đứng yên vĩnh viễn, và vì hàm fade là `await` trên `TaskCompletionSource` của tween đó, cả chuỗi chuyển cảnh treo luôn. Hai lớp phòng vệ:

- `EnsureUnpaused()` chạy trước mọi lệnh chuyển cảnh — ép `timeScale = 1`
- `FadeAsync()` luôn gắn `.SetUpdate(true)`

---

## 03 · Grid và hệ toạ độ

> Có **hai** cấu trúc grid song song. Hiểu sai chỗ này là hiểu sai một nửa codebase.

| | TDGridMainModel | TDGridDTO |
|---|---|---|
| Kiểu dữ liệu | `Vector3[,]` | `IGridCellDTO[,]` |
| Chứa gì | Vị trí world của tâm ô, cờ đã-chiếm, tập ô TowerZone | Cờ `isWalkable`, `CellType` |
| Dùng cho | Đổi world ↔ cell, kiểm tra đặt được hay không | Thuật toán maze và A* |
| Ai tạo | `TDGameplayMainView` từ bounds của Renderer | `new TDGridDTO(w, h)` sau khi Main model có kích thước |

> **⚠ ĐÂY LÀ MỘT DESIGN SMELL — BIẾT ĐỂ MÀ NÓI**
>
> Hai cấu trúc phải luôn đồng bộ nhưng **không có gì bắt buộc điều đó**. Một ô có thể `isWalkable = true` trong DTO mà lại `occupied` trong Main model. Codebase xử lý bằng cách lọc thủ công — xem `BuildGridZones()` có hẳn một vòng lặp đếm "path cells leaked" rồi cảnh báo. Đó là triệu chứng, không phải giải pháp.

### Kích thước grid suy ra từ hình học, không hardcode

```csharp
Vector3 mapSize = m_MapVisualize.GetComponent<Renderer>().bounds.size;
TDGridMainModel.Initialize(mapSize, planePosition);

// bên trong CreateGrid():
width  = floor(mapSize.x / cellSize);   // cellSize = 2
height = floor(mapSize.z / cellSize);
offsetX = planePos.x - mapSize.x/2 + cellSize/2;  // tâm ô [0,0]
```

Kéo to plane `GameMapVisualize` trong Editor thì grid tự lớn theo. Với bounds 40×30 hiện tại → grid 20×15 = 300 ô.

### Đồng bộ bất đồng bộ: TaskCompletionSource

Grid được dựng trong `Start()` của một view, còn maze cần grid xong mới chạy được — nhưng nằm ở view khác. Giải bằng một promise:

```csharp
// TDGridMainModel.CreateGrid() — cuối hàm
TDInitializeModel.api.createGridCompletion.SetResult(true);

// TDEnemyPathMainView.ImplementPath() — đầu hàm
await TDInitializeModel.api.createGridCompletion.Task;
```

> **VÌ SAO CÓ `TDInitializeModel.Reset()`**
>
> `TaskCompletionSource.SetResult()` gọi lần thứ hai sẽ **ném exception**. Chơi lại màn là chạy `CreateGrid()` lần nữa → phải có một TCS mới tinh. `Reset()` gán `m_api = null` để lần truy cập sau tạo instance mới. Không có dòng đó thì retry sẽ crash — và đó từng là bug thật.

---

## 04 · Sinh map — Maze, A*, đa cổng

> Phần thuật toán nặng nhất và cũng là thứ đáng khoe nhất của project.

### Ý tưởng cốt lõi

Một thuật toán sinh ra **cả hai** thứ mà tower defense cần:

| Trong mê cung | Trong game |
|---|---|
| Ô hành lang (`isWalkable = true`) | Đường đi của enemy + chỗ đặt operator cận chiến |
| Ô tường (`isWalkable = false`) | Nền cao — chỗ đặt tower và operator tầm xa |

Không cần đánh dấu thủ công vùng nào đặt được gì. Nó là hệ quả trực tiếp của cấu trúc mê cung.

### Quy ước chẵn/lẻ — chìa khoá của thuật toán

```csharp
IsValidRoomCell(x, y) => x % 2 == 0 && y % 2 == 0;

int[] dx = { 0, 2, 0, -2 };   // bước nhảy 2 ô
int[] dy = { 2, 0, -2, 0 };

// đục thông ô tường NẰM GIỮA hai phòng
grid.GetCell(cur.x + dx[d]/2, cur.y + dy[d]/2).isWalkable = true;
```

Ô toạ độ chẵn là "phòng", ô lẻ là "tường giữa hai phòng". Đi từ phòng sang phòng luôn nhảy 2 ô và đục thông ô ở giữa. Đây là lý do `TDGatePlacer` luôn phải đặt cổng ở toạ độ chẵn — cổng nằm ở ô lẻ thì mê cung không bao giờ chạm tới.

### Lặp thay vì đệ quy

```csharp
var stack = new Stack<Vector2Int>();
stack.Push(start);
while (stack.Count > 0) {
    var cur = stack.Peek();
    Shuffle(dirs);                    // Fisher–Yates
    foreach (int d in dirs) { ... stack.Push(next); moved = true; break; }
    if (!moved) stack.Pop();          // quay lui
}
```

Recursive Backtracker bản gốc là đệ quy, độ sâu bằng số ô. Grid lớn sẽ tràn stack. Đổi sang `Stack` tường minh thì độ sâu chuyển vào heap, không giới hạn thực tế.

### Ba bước sinh một map hoàn chỉnh

1. **BƯỚC 1 · mỗi corridor** — `SetAllWalls()` → `CarveFrom(startCell)` → chặn ô cổng của các group khác → `A*.ComputePath(start, end)`.
2. **BƯỚC 2 · retry** — Nếu đường ngắn hơn `grid.width`, làm lại. Tối đa `MAZE_MAX_ATTEMPTS = 10` lần, sau đó lấy đường dài nhất trong các lần thử. **Không bao giờ trả về thất bại.**
3. **BƯỚC 3 · gộp** — Lặp bước 1–2 cho tới khi đủ `CONFIG_NUM_PATHS = 3` corridor mỗi group. Gom tất cả ô đường vào một `HashSet`, `SetAllWalls()` lần cuối rồi mở lại đúng các ô đó.

> **✔ BA EDGE CASE ĐÃ XỬ LÝ — DẤU HIỆU CODE ĐÃ CHẠY THẬT**
>
> **Gộp nhiều group:** mỗi lần sinh mê cung đều `SetAllWalls()`, sẽ xoá sạch kết quả của group trước. Nên phải gom hết vào `combinedPathCells` rồi khôi phục một lần ở cuối.
>
> **Chặn cổng chéo:** trước khi chạy A* cho một group, ô cổng của group khác bị set `isWalkable = false` — nếu không, đường của group này sẽ chui qua cổng group kia.
>
> **Ép cổng luôn đi được:** ở pass cuối, ô start/end của mọi group được ép `isWalkable = true` bất kể mê cung ra sao.

### A* — cấu hình thực tế

| Thành phần | Giá trị | Ghi chú |
|---|---|---|
| Heuristic | `\|Δx\| + \|Δy\|` | Manhattan — admissible vì chỉ đi 4 hướng |
| Chi phí bước | `1.0` | Đồng nhất, không có ô đắt/rẻ |
| Láng giềng | `4 hướng` | Không đi chéo; lọc `isWalkable` và `≠ Obstacle` |
| openSet | `List + OrderBy().First()` | **O(n²)** quét tuyến tính mỗi vòng lặp |

> **⚠ CÂU HỎI TỰ ĐẶT CHO MÌNH**
>
> Mê cung sinh ra là **perfect maze** — giữa hai ô bất kỳ chỉ tồn tại đúng một đường. Nghĩa là ở bước trích đường, **BFS cho ra kết quả y hệt A\* mà đơn giản hơn**. A\* ở đây có giá trị vì nó nằm sau interface `IPathFinder` và sẽ đúng hơn khi grid có ô chi phí khác nhau. Nhưng nói cho sòng phẳng: hiện tại nó là công cụ mạnh hơn mức bài toán cần.

### Đa cổng — TDGatePlacer

Chia biên thành `count` đoạn bằng nhau, mỗi đoạn chọn ngẫu nhiên một toạ độ *chẵn*. Đảm bảo cổng vừa rải đều vừa rơi đúng ô phòng của mê cung.

Ghép start↔end: `groupCount = max(startCount, endCount)`, ghép theo modulo. Riêng trường hợp 2 start + 2 end thì `endCells.Reverse()` — ghép chéo để đường ngoằn ngoèo hơn theo trục Y.

Sáu bố cục trong `MapLayout`: bốn hướng thẳng và hai đường chéo, mỗi cái ánh xạ ra một cặp `BorderSide`.

---

## 05 · Wave engine

> Lập kế hoạch wave trước, rồi chạy bằng async/await có huỷ được.

### Lập kế hoạch — BuildWavePlans

Toàn bộ wave được tính **trước khi** spawn con enemy đầu tiên. Lý do quan trọng: HUD cần biết tổng số enemy để hiện `killed/total` và để phán định thắng.

```csharp
float r = totalEnemies / (regularWaveCount + bossWaveCount * bossWaveMult);
int regularSize  = round(r);
int bossWaveSize = round(r * bossWaveMult);

// vị trí wave boss rải đều trên toàn bộ chuỗi wave
idx = ceil(waveCount * k / bossWaveCount) - 1;
```

Trong mỗi wave: chia Normal/Fast/Tank theo tỉ lệ độ khó, **xáo Fisher–Yates**, rồi mới *append* boss vào cuối — nên boss luôn ra sau cùng trong wave boss.

### Bảng tỉ lệ độ khó

| Độ khó | Normal | Fast | Tank | Boss | ×HP | ×Speed | Wave boss |
|---|---|---|---|---|---|---|---|
| Easy | 80% | 15% | 4% | 1% | 0.8 | 0.9 | 1×1 |
| Normal | 60% | 25% | 12% | 3% | 1.0 | 1.0 | 1×1 |
| Hard | 45% | 30% | 18% | 7% | 1.2 | 1.1 | 2×1 |
| Extreme | 30% | 30% | 25% | 15% | 1.5 | 1.2 | 3×1 |
| Nightmare | 15% | 25% | 35% | 25% | 2.0 | 1.5 | 5×2 |

Từ Extreme trở lên, `waveCount` bị ép tối thiểu 15 và `totalEnemies` tối thiểu 75 — chặn việc config ra một màn "Nightmare" chỉ có 3 wave.

### Vòng lặp wave

```csharp
public async Task StartWaveLoop(groups, wavePlans, config, CancellationToken ct)
{
    for (int waveIdx = 0; waveIdx < wavePlans.Count; waveIdx++) {
        ct.ThrowIfCancellationRequested();
        TDGameEventBus.WaveStarted(waveIdx);

        var assignments = m_Strategy.SelectForWave(groups, waveIdx);
        // chia đều enemy cho từng cổng được chọn
        var spawnTasks = new List<Task>();
        foreach (assignment) spawnTasks.Add(SpawnBatch(...));

        await Task.WhenAll(spawnTasks);           // nhiều cổng spawn SONG SONG
        await PauseAwareDelay(config.waveInterval, ct);
    }
    TDGameStateControl.api?.OnAllWavesSpawned();
}
catch (OperationCanceledException) { /* huỷ là bình thường, không phải lỗi */ }
```

### PauseAwareDelay — vì sao không dùng Task.Delay

```csharp
while (elapsed < seconds) {
    ct.ThrowIfCancellationRequested();
    await Task.Yield();
    elapsed += Time.deltaTime;    // = 0 khi timeScale = 0
}
```

`Task.Delay` đếm theo đồng hồ hệ thống nên vẫn chạy khi game đang pause — pause 10 giây xong sẽ có một loạt enemy đổ ra cùng lúc. Cộng dồn `Time.deltaTime` thì delay tự đóng băng theo `timeScale`, và tự tăng tốc gấp đôi khi bấm nút x2. Một dòng giải quyết hai chuyện.

Đánh đổi: đây là vòng lặp yield mỗi frame, không phải timer thật. Chi phí không đáng kể ở đây nhưng nên biết.

### Fire-and-forget có kiểm soát

```csharp
TDEnemyPathMainControl.api.StartWaveLoop(groups, wavePlans, config, cts.Token)
                          .Forget("StartWaveLoop");
```

`TaskExtensions.Forget()` gắn continuation `OnlyOnFaulted`, gỡ `AggregateException` lấy exception thật, và bỏ qua `OperationCanceledException` vì huỷ là tín hiệu điều khiển chứ không phải lỗi. Đây là cách đúng để chạy một `Task` mà không await — thay vì dùng `async void` vốn nuốt mất mọi exception.

### Bốn chiến lược chọn cổng

| Strategy | Hành vi | Số cổng mỗi wave |
|---|---|---|
| RoundRobin | Wave i → group `i % groupCount` | 1 |
| Random | Group ngẫu nhiên mỗi wave | 1 |
| PerWave | Giống RoundRobin — **trùng lặp** | 1 |
| Simultaneous | Mọi group spawn song song | tất cả |

`PerWaveStrategy` và `RoundRobinStrategy` hiện có **thân hàm giống hệt nhau**. Hoặc gộp lại, hoặc làm cho PerWave thực sự khác (ví dụ: cố định một group cho cả wave thay vì đổi corridor ngẫu nhiên).

---

## 06 · Hệ chiến đấu

> Tầm đánh theo ô, chặn đường kiểu Arknights, và hai đường sát thương khác nhau cho cận chiến với tầm xa.

### Tầm đánh là danh sách offset, không phải bán kính

Đây là quyết định thiết kế quan trọng nhất của hệ chiến đấu. Tầm đánh được định nghĩa bằng `Vector2Int[] rangeOffsets` — các ô tương đối so với vị trí unit, quy ước khi unit quay mặt về +X.

```csharp
// TDOffsetRangeDTO.RotateOffset — ánh xạ theo 4 hướng chính
0°   => ( dy,  dx)      // mặt về +Z
90°  => ( dx,  dy)      // mặt về +X — không gian định nghĩa
180° => (-dy, -dx)      // mặt về -Z
270° => (-dx, -dy)      // mặt về -X
```

Vì sao dùng offset thay vì bán kính: cho phép vẽ hình dạng tầm đánh **bất đối xứng** — hình nón, hình chữ L, một hàng ngang — đúng như Arknights. Bán kính chỉ cho ra hình tròn.

> **✔ LỢI ÍCH LỚN NHẤT: HIGHLIGHT VÀ SÁT THƯƠNG DÙNG CHUNG MỘT NGUỒN**
>
> `GetCellsInRange()` vừa dùng để vẽ ô sáng khi người chơi đặt unit, vừa dùng để quét tìm mục tiêu khi đánh. **Cái người chơi nhìn thấy đúng bằng cái thực sự xảy ra.** Trước đây `TowerZoneOperatorBehavior` dùng `Physics.OverlapSphere` để tìm mục tiêu — hình tròn vô hướng — nên tầm hiển thị và tầm thật là hai thứ khác nhau. Đó là một bug thật đã sửa.

### Cơ chế chặn đường

`TDOperatorRegistry` giữ bốn map song song theo ô: tập ô hợp lệ, sức chứa chặn hiện tại, danh sách enemy đang bị chặn, và tham chiếu tới view của operator.

1. **ENEMY TỚI Ô** — Trong `Update()`, khi `Distance < 0.1` tới waypoint: hỏi `CanBlock(arrivedCell)`.
2. **BỊ CHẶN** — Snap chính xác vào tâm ô (để phép kiểm tra tầm đánh chuẩn), đặt `m_IsBlocked`, gọi `OnEnemyBlocked` tăng bộ đếm.
3. **ĐÁNH NHAU** — Enemy phản công operator qua `GetOperatorView(cell).TakeDamage()`. Operator đánh lại qua `GetBlockedEnemies(cell)`. Hai chiều.
4. **OPERATOR CHẾT** — `UnregisterOperator()` gọi `ForceUnblock()` cho toàn bộ enemy đang bị giữ. Hàm này `m_CurrentPathIndex++` — bỏ qua ô operator vừa chết để enemy đi tiếp thay vì kẹt.

### Hai hành vi operator — Strategy Pattern

| | PathCellOperatorBehavior | TowerZoneOperatorBehavior |
|---|---|---|
| Class | Knight, Defender, Striker | Ranger, Mage |
| Đặt ở | Ô đường đi | Ô tường (nền cao) |
| Chặn | 1–3 enemy | Không |
| Tìm mục tiêu | Danh sách đang bị chặn | Quét registry, lọc theo ô trong tầm, chọn gần nhất |
| Gây sát thương | Ngay trong `TryAttack` | Trong `ExecuteHit` — gọi từ Animation Event |

> **VÌ SAO TẦM XA PHẢI TÁCH `TryAttack` VÀ `ExecuteHit`**
>
> Cận chiến thì đòn đánh là tức thời — vung kiếm là trúng. Tầm xa có độ trễ: bắn cung, mũi tên bay, rồi mới trúng. Nếu trừ máu ngay lúc bấm đánh thì VFX trúng đích sẽ nổ ở chỗ enemy *đã rời khỏi*.
>
> Giải pháp: `TryAttack()` lưu `m_PendingTarget` và chỉ phát animation. Một **AnimationEvent** đặt giữa clip tấn công gọi ngược lại `TDOperatorView.OnAttackHit()` → `ExecuteHit()` → lúc đó mới trừ máu và bắn VFX tại vị trí enemy *hiện tại*. Timing khớp với hình ảnh.

### Tower chọn mục tiêu bằng PathProgress

```csharp
public float PathProgress => (float)m_CurrentPathIndex / m_PathsPosition.Count;

// TDTowerWeaponView.ScanForTargets — mỗi 0.2 giây
m_CandidateBuffer.Sort((a, b) => b.PathProgress.CompareTo(a.PathProgress));
```

Sắp giảm dần theo tiến độ → tower luôn tập trung bắn **con gần đích nhất**. Đây là hành vi đúng cho tower defense: con sắp thoát nguy hiểm hơn con vừa spawn.

> **⚠ MỘT CHI TIẾT DỄ GÂY HIỂU NHẦM CHO NGƯỜI CHƠI**
>
> Tower *xoay hình* về phía mục tiêu (`RotateTowardsPrimary`) nhưng khi kiểm tra tầm đánh lại luôn dùng `m_OriQuaternion` — hướng lúc đặt. Nghĩa là **xoay chỉ là hiệu ứng thị giác, tầm đánh đứng yên**. Đúng về mặt thiết kế (không thì đặt hướng thành vô nghĩa) nhưng người chơi nhìn tower quay có thể tưởng tầm đánh cũng quay theo.

### Ba kiểu tấn công

| AttackType | Cơ chế | Sát thương ở đâu |
|---|---|---|
| Single | 1 đạn → 1 mục tiêu, tự bám | `TDAttackVFX.OnImpact` |
| Multiple | N đạn → N mục tiêu đầu bảng, mỗi viên tự bám | Mỗi viên khi trúng |
| AOE | 1 đạn bay tới vị trí, nổ lan theo `rangeOffsets` | `ApplyAOEDamage()` quét registry |

Đạn tự bám: mỗi frame cập nhật `m_TargetPos` theo mục tiêu. Nếu mục tiêu chết giữa chừng, đạn bay nốt tới vị trí cuối cùng biết được thay vì biến mất — nhìn tự nhiên hơn.

---

## 07 · Deploy và Retreat

> File gameplay lớn nhất project (`TDDeployController`, 531 dòng) và là chỗ có nhiều bài học UX nhất.

### State machine

```
Idle ──(PointerDown trên slot)──> Dragging
Dragging ──(thả trên ô hợp lệ)──> DirectionSelect     [Drop()]
Dragging ──(thả trên ô sai)────> Idle                [CancelPlacement]
DirectionSelect ──(thả ngoài dead-zone)──> Committing [CommitPlacement]
DirectionSelect ──(thả trong dead-zone)──> Idle       [CancelPlacement]
```

> **✔ BÀI HỌC THIẾT KẾ ĐÁNG GIÁ NHẤT TRONG PROJECT**
>
> Bản đầu dùng **một** cử chỉ, phân biệt hai pha bằng timer ẩn: giữ 0,35 giây mới "arm", cộng bộ lọc 0,2 giây cho micro-lift. Hai hậu quả: code đầy timer phòng thủ, và người chơi *không biết* phải giữ rồi mới vuốt.
>
> Bản hai dùng **hai** cử chỉ tường minh: ngón A kéo và thả để chốt ô, ngón B (một touch *mới*) kéo để chọn hướng. Kết quả: xoá khoảng 70 dòng timer, và bug micro-lift **tự biến mất** — vì pha 2 chờ một touch mới nên hoàn toàn không quan tâm touch cũ kết thúc kiểu gì.
>
> *Nguyên tắc rút ra: khi bug input dai dẳng, xem lại mô hình tương tác trước khi thêm guard.*

### Micro-lift là gì

Cảm biến điện dung Android mất tiếp xúc vài mili-giây khi ngón tay kéo chậm hoặc khô. Hệ điều hành bắn `TouchPhase.Ended` dù ngón tay vẫn còn trên màn hình, rồi ngay lập tức bắn `TouchPhase.Began`. Với logic "Ended = đặt unit", unit tự đặt giữa chừng khi người chơi đang kéo.

### Ba chi tiết mobile thật

| Vấn đề | Cách xử lý |
|---|---|
| Ngón tay che mất ghost | Dịch điểm raycast lên `Screen.height × 0.10` trước khi bắn tia. Hằng số `TOUCH_SCREEN_Y_OFFSET_RATIO` — *phải tune theo thiết bị thật*, nên để một chỗ chứ không rải rác. |
| Nhiều ngón tay làm rối thứ tự touch | Khoá theo `touchId`: `m_DragFingerId` cho pha 1, `m_DirFingerId` cho pha 2. Không đọc `activeTouches[0]`. |
| Desktop và mobile khác nhau hoàn toàn | `#if UNITY_ANDROID && !UNITY_EDITOR`. Desktop giữ chuột + E/Q xoay để dev nhanh trong Editor. |

### Diamond panel — một prefab, hai chế độ

`TDDiamondPanelView` dùng chung cho cả deploy (chọn hướng) lẫn retreat (rút quân). Cùng ngôn ngữ hình ảnh nên **không thể chồng lên nhau** — đây chính là cách xoá cả một lớp bug về overlap giữa hai hệ thống UI song song.

Nó là **Canvas UI thuần**, không phải world-space. Mỗi `LateUpdate` chiếu vị trí world lên màn hình qua `WorldAnchoredUI.PositionAt()`. Button là Unity Button bình thường nên click chạy tự nhiên, không cần raycast tự chế.

> **BA LỚP PHÒNG VỆ CHỐNG "DIAMOND MA" Ở GÓC MÀN HÌNH**
>
> Triệu chứng cũ: thỉnh thoảng một diamond thứ hai hiện ở góc trái dưới và rung. Nguyên nhân: `m_CenterWorld` mặc định là `Vector3.zero`, mà gốc world của map chiếu lên màn hình rơi đúng góc đó.
>
> - **Lớp 1 — Awake:** `gameObject.SetActive(false)` ngay lập tức. Prefab ship ở trạng thái active để designer nhìn thấy trong Editor, nhưng runtime luôn bắt đầu ẩn.
> - **Lớp 2 — Awake:** `LayoutElement.ignoreLayout = true`. Container cha có LayoutGroup sẽ kéo diamond về ô layout, rồi `LateUpdate` kéo ngược lại — rung mỗi frame.
> - **Lớp 3 — LateUpdate:** nếu đang ở mode `Hidden` mà vẫn active và alpha ≈ 0 thì ép tắt. Bắt trường hợp `OnComplete` của tween bị bỏ qua do một `Show` chen vào giết tween.

> **⚠ COMMENT KHÔNG KHỚP CODE**
>
> Comment trong `PlayHideRootAnim()` nói "dùng `DOTween.Sequence` để `SetActive(false)` chạy cả trong `OnKill`". Code thực tế dùng `.OnComplete(FinishHide)` trên một tween `DOScale`, **không phải Sequence, không có OnKill**. Lớp phòng vệ số 3 đang gánh trường hợp này. Nên sửa comment cho khớp, hoặc cài đúng như comment mô tả.

---

## 08 · Event bus, VFX và Audio

> Cơ chế giữ cho gameplay không biết gì về hiệu ứng và âm thanh.

### 14 event, chia bốn nhóm

| Nhóm | Event | Ai nghe |
|---|---|---|
| Enemy | `OnEnemyDied` · `OnEnemySpawned` | EffectManager |
| Tower/Operator | `OnTowerAttacked` · `OnOperatorAttacked` · `OnOperatorImpacted` · `OnOperatorDied` | EffectManager |
| Player | `OnLifeLost` · `OnVictory` · `OnGameOver` | EffectManager, AudioContext, DeployController, SelectionView |
| Wave / lifecycle | `OnWaveStarted` · `OnGameplayStarted` | EffectManager, AudioContext |
| UI | `OnUnitPickup` · `OnTowerPlaced` · `OnDeployDrop` | EffectManager, TutorialView, SelectionView |

Giá trị thực tế: `TDEnemyView.Die()` chỉ gọi `TDGameEventBus.EnemyDied(pos, type)`. Nó không biết ai đang nghe. **Thêm SFX mới cho sự kiện đó = thêm một entry vào `Effect Config.asset`, không sửa một dòng gameplay nào.**

### Chuỗi từ event tới hiệu ứng

1. Gameplay bắn event kèm vị trí world và loại unit.
2. `TDEffectManager` ánh xạ sang `GameEventKey` (16 giá trị) bằng switch expression.
3. Tra `Dictionary<GameEventKey, EffectDef>` nạp sẵn lúc `Init()` từ ScriptableObject.
4. Spawn VFX từ pool riêng theo prefab (`Queue<GameObject>`), tự trả về pool sau khi hết thời lượng ParticleSystem.
5. Gọi `TDAudioService.SFX.Play()` và rung camera nếu `EffectDef.cameraShake`.

Thời lượng VFX được đọc từ chính ParticleSystem: `main.duration + main.startLifetime.constantMax` — không hardcode con số nào.

### Audio ba tầng

| Tầng | Vai trò | File |
|---|---|---|
| **Tầng 1 · Audio** | Thuần, không biết scene nào tồn tại. Tất cả `DontDestroyOnLoad`. | `TDBGMPlayer` · `TDSFXPlayer` · `TDAudioPrefs` · `TDAudioService` (facade) |
| **Tầng 2 · Context** | Chỉ làm một việc: nối event của scene vào tầng 1. | `TDGameplayAudioContext` · `TDMainMenuAudioContext` |
| **Tầng 3 · Effect** | Nghe event bus, tra config, gọi xuống facade. | `TDEffectManager` |

**Ánh xạ SOLID:** *SRP* — `TDBGMPlayer` chỉ play/stop/fade, không biết "victory" là gì. *OCP* — thêm scene mới chỉ cần thêm một AudioContext, không đụng tầng 1. *DIP* — tầng 3 gọi qua facade, không biết `AudioSource` nào đang phát.

### Hai chi tiết chống vỡ tiếng

- **Pool 8 AudioSource.** `AudioSource.PlayClipAtPoint` tạo một GameObject mới mỗi lần gọi — 5–10 lần mỗi giây khi tower đang bắn thì đủ gây GC spike. Pool đưa alloc về 0.
- **Rate limit 0,05 giây theo clip.** Năm tower bắn cùng frame sẽ phát năm lần cùng một clip, tổng âm lượng vượt ngưỡng và rè trên loa điện thoại. Chặn theo `clip.GetInstanceID()`, các clip khác nhau vẫn độc lập.

> **⚠ RỦI RO CỐ HỮU CỦA STATIC EVENT**
>
> Quên `-=` là rò rỉ subscriber, và **không có gì báo lỗi** — nó chỉ âm thầm giữ tham chiếu. Hiện tại mọi nơi đăng ký đều có chỗ huỷ tương ứng (`OnDestroy` cho View, `Cleanup()` cho static manager). Quy mô lớn hơn thì nên bọc bằng `IDisposable` hoặc dùng bus tiêm qua DI để tự huỷ theo vòng đời.

---

## 09 · Tầng UI

> Một quy ước duy nhất quyết định toàn bộ cách UI được nối.

### Quy ước tham chiếu — không dùng SerializeField cho con của chính mình

| Loại tham chiếu | Cách lấy | Vì sao |
|---|---|---|
| View tìm con của chính nó | `transform.Find(TDConstant.PATH_*)` | Không phụ thuộc scene wiring — nguồn bug lớn nhất khi refactor UI |
| Prefab tự tham chiếu nội bộ | `[SerializeField]` | Prefab là một đơn vị đóng gói, wiring nằm trong chính nó |
| Asset (prefab, config, sprite) | `TDResourceObject.GetResource<T>()` | Tham chiếu trực tiếp, Unity theo dõi được dependency |
| Giá trị tinh chỉnh | `TDConstant` | Một chỗ duy nhất, dễ tune |

> **✔ CHUYỂN SANG Find ĐÃ LỘ RA MỘT BUG ĐANG ẨN**
>
> Sau khi migrate, Play mode báo `ResolveReferences failed`. Điều tra ra scene có **hai** `TDMainMenuView` — một đúng chỗ và một component rác trên Canvas. Bản dùng SerializeField cũ đã im lặng che giấu nó suốt: fields null, không ai gọi, không ai biết. Chính việc migrate là thứ làm nó lộ ra.

### TDResourceObject — không phải Resources.Load

```csharp
public class TDResourceObject : MonoBehaviour {
    [SerializeField] private List<Object> objects;   // tham chiếu TRỰC TIẾP
    public static T GetResource<T>(string name) where T : Object {
        var realName = Path.GetFileNameWithoutExtension(name);
        foreach (var obj in Instance.objects)
            if (obj != null && obj.name.Equals(realName)) return obj as T;
        Debug.LogError($"Resource not found: '{realName}'");
        return null;
    }
}
```

Chuỗi truyền vào chỉ dùng để *so tên*, không phải đường dẫn. Toàn codebase chỉ còn **đúng một** lời gọi `Resources.Load` thật (trong `TDStageMaterialCache`, lấy một material).

Hệ quả: chuyển sang Addressables chỉ phải sửa trong một class. Nhưng lưu ý cho sòng phẳng — asset vẫn *nằm trong* thư mục `Resources` nên vẫn bị force-include vào build. Chính vì hiểu cơ chế này mà việc chuyển 69 MB mesh *ra khỏi* Resources giúp APK giảm từ 116 xuống 102 MB.

### Ba lỗi UI đã sửa và bài học chung

| Triệu chứng | Nguyên nhân gốc | Cách sửa |
|---|---|---|
| Số vàng phình to dần rồi kẹt | `DOTween.Kill(t)` mặc định dừng tween giữa chừng, `localScale` đứng lại ở giá trị vọt lố. Kích lại liên tục thì trôi dần khỏi 1. | `SafePunch()`: Kill → reset `localScale = one` → punch |
| Hint tutorial xếp dọc một ký tự mỗi dòng ở mép trái | Container cha có LayoutGroup, ép hint vào luồng layout | `LayoutElement.ignoreLayout = true` |
| Panel menu lệch vị trí ở frame đầu | `PlayIntro()` đọc `anchoredPosition` trước khi layout được tính | `Canvas.ForceUpdateCanvases()` + `LayoutRebuilder.ForceRebuildLayoutImmediate()` trong `Start()` |

**Mẫu chung của cả ba:** hệ thống layout của Unity UI chạy *sau* code của bạn trong cùng frame. Bất cứ khi nào đọc hoặc ghi vị trí thủ công mà cha có LayoutGroup, phải hoặc thoát khỏi layout, hoặc ép layout tính trước.

### Tutorial — không cần dựng gì trong scene

`TDTutorialView` được `AddComponent` bởi `TDSlotHolderMainView.Start()`, và `TDFloatingHint` tự dựng UI bằng code. Zero scene setup. Trạng thái lưu bằng PlayerPrefs với namespace `td_tutorial_v1_*` — đặt tiền tố `v1` để sau này muốn bật lại tutorial cho người chơi cũ thì chỉ cần đổi thành `v2`.

Bốn hành vi: hint khi nhấc unit (tối đa 3 lần), hint khi thả xuống ô (3 lần), hint về retreat sau lần đặt đầu tiên (2 lần), và bàn tay hoạt hoạ khi người chơi đứng im 5 giây ở lần chơi đầu.

---

## 10 · Data-driven config

> Sáu ScriptableObject. Đây là thứ cho phép cân bằng game mà không đụng code — điều kiện cần của mọi studio vận hành game live.

| ScriptableObject | Chứa gì | Truy cập |
|---|---|---|
| Stage Repository | Danh sách stage, thứ tự, stage mặc định | `TDStageRepository.api` |
| Stage Config (×N) | Số cổng, MapLayout, GateMode, mô tả, BGM, khoá/mở | qua repository |
| Level Config | Độ khó, tổng enemy, số wave, nhịp spawn | `TDLevelConfigSettings.api` |
| Enemy Data Config | HP, tốc độ, sát thương, vàng thưởng, prefab, thời lượng chết | `TDFlyweightEnemyDataSettings.api` |
| Melee Operator Config | Tên, class, kiểu đánh, HP, block, giá, offset tầm, icon | `TDFlyweightOperatorDataSettings.api` |
| Tower Bullet Config | Prefab tower + đạn, sát thương, tốc độ, kiểu đánh, offset tầm | `TDFlyweightTowerDataSettings.api` |
| Effect Config | 16 `EffectDef`: VFX đánh, VFX trúng, SFX, âm lượng, rung camera | `TDEffectConfig.api` |

Cả sáu dùng **chung một mẫu** — nhất quán là điểm nên nói ra:

```csharp
private static T m_api;
public static T api => m_api ??= TDResourceObject.GetResource<T>(TDConstant.CONFIG_X);
```

### Suy diễn thay vì cấu hình — chống sai từ gốc

```csharp
// OperatorData — deployZone KHÔNG phải field, mà là property tính ra
public DeployZone deployZone =>
    operatorType is OperatorType.Ranger or OperatorType.Mage
        ? DeployZone.TowerZone
        : DeployZone.PathCell;
```

Nếu `deployZone` là field serialize thì sẽ có ngày ai đó đặt Ranger thành PathCell và tạo ra một bug rất khó hiểu. Làm nó thành property suy ra từ class thì **trạng thái sai không tồn tại được**. Cùng tinh thần: `MaxTargets` của operator tự trả 0 khi ở TowerZone, và bị `Clamp(1,3)` khi ở PathCell.

### Editor drawer tự viết

Ba `PropertyDrawer` (`OperatorDataDrawer`, `TowerDataDrawer`, `EffectDefDrawer`) làm Inspector gọn hơn. `EffectDefDrawer` có cờ `onlySfx` — bật lên thì ẩn luôn các field VFX và camera shake. Đây là chi tiết nhỏ nhưng đúng tinh thần công cụ: giảm thứ designer phải nhìn.

### Slot bar dựng lúc chạy

`BuildSlots()` gộp tower và operator từ hai config, và nếu tổng vượt `CONFIG_MAX_SLOTS = 8` thì **xáo Fisher–Yates rồi lấy 8 cái đầu**. Nghĩa là mỗi ván chơi bộ unit khả dụng có thể khác nhau. Đây là một quyết định thiết kế game, không phải giới hạn kỹ thuật — nên biết rõ để trả lời khi bị hỏi "sao mỗi lần vào lại khác".

---

## 11 · Đánh đổi thiết kế

> Mỗi dòng là một quyết định có ý thức. Cột cuối là điều kiện để đổi ý — đây mới là phần quan trọng.

| Quyết định | Được gì | Mất gì | Khi nào nên đổi |
|---|---|---|---|
| Static `.api` thay cho DI | Truy cập gọn, zero boilerplate, kiểm soát thứ tự khởi tạo | Hidden dependency, không mock được, không test được | Khi có nhiều hơn 2–3 người sửa cùng lúc, hoặc khi bắt đầu viết test |
| Control là C# thuần | Không vướng Unity lifecycle, reload scene sạch | Phải tự quản vòng đời, không dùng được Inspector để debug | Hầu như không — đây là lựa chọn tốt và nên giữ |
| Tầm đánh bằng offset thay vì bán kính | Tầm bất đối xứng, highlight khớp sát thương tuyệt đối | Không có tầm liên tục; xoay chỉ hỗ trợ 4 hướng chính | Khi cần tower xoay tự do 360° |
| Event bus static | Decouple hoàn toàn VFX/Audio/UI khỏi gameplay | Rò rỉ nếu quên huỷ đăng ký; khó truy vết ai đang nghe | Khi số subscriber vượt tầm nhớ, hoặc khi debug event trở thành thường xuyên |
| Hai grid song song | Mỗi cấu trúc tối ưu cho việc của nó | Phải đồng bộ thủ công; đã sinh bug "path cell leak" | **Nên hợp nhất ngay khi có dịp** — đây là nợ thật |
| A* thay vì BFS cho maze | Tái sử dụng được khi grid có chi phí khác nhau | Phức tạp hơn mức cần; hiện là O(n²) | Đổi openSet sang priority queue khi grid lớn hơn nhiều |
| Resources + lớp bọc | Đơn giản, không cần setup Addressables | Force-include toàn bộ thư mục vào build | Khi build size thành vấn đề, hoặc cần tải nội dung từ xa |
| `transform.Find` thay SerializeField | Không phụ thuộc scene wiring, refactor UI an toàn | Đổi tên node trong scene là gãy lúc chạy, không phải lúc compile | Chấp nhận được vì đường dẫn tập trung ở `TDConstant` |
| async/await thay Coroutine | Có `CancellationToken`, xử lý lỗi tử tế, viết tuần tự | Không tự dừng theo GameObject; phải tự lo pause | Giữ nguyên — đã xử lý cả hai nhược điểm |

---

## 12 · Từ điển thuật ngữ

> Chỉ những khái niệm *thực sự* đang dùng trong project này, kèm chỗ dùng cụ thể.

### Pattern & kiến trúc

**Strategy Pattern** — Đóng gói nhiều thuật toán sau cùng một interface, chọn cái nào ở runtime.
*Hai chỗ:* `IGateAssignmentStrategy` (4 impl) và `IOperatorBehavior` (2 impl). Factory nằm ở `TDControl.CreateStrategy/CreateOperatorBehavior`.

**Flyweight** — Chia sẻ phần dữ liệu chung giữa nhiều instance thay vì mỗi instance giữ một bản.
*Ở đây:* `OperatorData`/`EnemyData`/`TowerData` nằm trong SO; mọi unit cùng loại trỏ vào một bản. `TDOperatorView` cache `m_Data` lúc Init để tránh `List.Find` mỗi frame.

**Object Pool** — Tái dùng object đã tạo thay vì Instantiate/Destroy liên tục.
*Bốn chỗ:* enemy (`ObjectPool<T>` mỗi `EnemyType`), đạn (mỗi `TowerType`), VFX (`Queue` mỗi prefab), SFX (mảng 8 AudioSource).

**Observer / Event Bus** — Nơi phát không biết ai nghe; người nghe tự đăng ký. Dạng tập trung gọi là event aggregator.
*Ở đây:* `TDGameEventBus` — static class, 14 event, mỗi event có một raise helper để nơi gọi không phải null-check.

**Facade** — Một điểm truy cập gọn che đi hệ thống con phức tạp hơn.
*Ở đây:* `TDAudioService.BGM` / `.SFX` — nơi gọi không biết `AudioSource` nào đang phát.

**Registry** — Danh bạ trung tâm của các đối tượng đang sống, để tra cứu thay vì đi tìm.
*Hai cái:* `TDEnemyRegistry` (tower quét thay vì dùng trigger collider) và `TDOperatorRegistry` (trạng thái chặn theo ô).

**DTO** — Đối tượng chỉ chứa dữ liệu, không chứa hành vi.
*Ở đây:* `TDPathGroup`, `TDGridCellDTO`, `TDTowerSlotInfo` (struct).

**DIP** — Module cấp cao phụ thuộc abstraction, không phụ thuộc implementation.
*Ví dụ sạch nhất:* `TDMazePathGenerator(IPathFinder)` — nhận qua constructor, không biết A* tồn tại.

**SRP** — Một class chỉ nên có một lý do để phải sửa.
*Ví dụ mạnh nhất:* tách `TDDeployController` khiến `TDSlotHolderMainView` từ 754 xuống 116 dòng.

**OCP** — Mở rộng được mà không sửa code cũ.
*Ở đây:* thêm chế độ cổng mới = thêm một class implement `IGateAssignmentStrategy`, không đụng `StartWaveLoop`.

**Finite State Machine** — Tập trạng thái hữu hạn, mỗi lúc ở đúng một trạng thái, chuyển theo sự kiện xác định.
*Ở đây:* `DeployState { Idle, Dragging, DirectionSelect, Committing }`.

**Singleton tĩnh** — Một instance duy nhất truy cập toàn cục.
*Ở đây:* hơn 20 class dùng `.api`. Hai biến thể: gán thẳng trong `InitOtherControl()`, hoặc lazy `??=`.

### Thuật toán

**A\*** — Tìm đường trên đồ thị, chọn node theo `f = g + h` — g là chi phí đã đi, h là ước lượng còn lại.
*Ở đây:* 4 hướng, cost 1.0, heuristic Manhattan, openSet là List.

**Admissible heuristic** — Heuristic không bao giờ ước lượng *cao hơn* chi phí thật — điều kiện để A\* đảm bảo tìm đường tối ưu.
*Vì sao Manhattan admissible ở đây:* chỉ cho đi 4 hướng nên không có đường tắt chéo nào rẻ hơn.

**Recursive Backtracker** — Sinh mê cung bằng cách đi sâu ngẫu nhiên, đục tường khi sang ô chưa thăm, quay lui khi bí.
*Ở đây:* cài bằng `Stack` lặp thay đệ quy để tránh tràn stack.

**Perfect maze** — Mê cung không có vòng lặp — giữa hai ô bất kỳ tồn tại đúng một đường đi.
*Hệ quả:* chính vì vậy mà BFS cũng cho ra kết quả y hệt A\* ở bước trích đường.

**Fisher–Yates shuffle** — Xáo mảng ngẫu nhiên đều: duyệt từ cuối về đầu, hoán đổi mỗi phần tử với một vị trí ngẫu nhiên phía trước nó.
*Ba chỗ:* xáo hướng khi sinh mê cung, xáo thứ tự enemy trong wave, xáo slot khi vượt 8.

**Footprint radius** — Bán kính ô mà một vật cản chiếm, tính từ kích thước hình học thật.
*Ở đây:* `ComputeFootprintRadii()` instantiate tạm prefab, gộp `Renderer.bounds`, chia cellSize. Không hardcode.

**PathProgress** — Tiến độ 0→1 của enemy trên đường đi của nó.
*Dùng để:* tower sắp mục tiêu giảm dần → luôn bắn con gần đích nhất.

### Unity & async

**async void vs async Task** — `async void` không await được và **nuốt exception**. `async Task` trả Task để caller xử lý.
*Ở đây:* wave loop là `async Task` + `.Forget()`. Còn đúng một `async void` ở `ImplementPath()` — điểm vào async từ MonoBehaviour.

**CancellationToken** — Cơ chế chuẩn .NET để báo tác vụ bất đồng bộ dừng có kiểm soát.
*Ở đây:* `m_WaveCts` huỷ trong `OnDestroy` → wave loop dừng sạch thay vì spawn vào scene đã unload.

**TaskCompletionSource** — Một Task tự tay hoàn thành — cầu nối giữa callback và await.
*Hai chỗ:* `createGridCompletion` (chờ grid dựng xong) và `FadeAsync` (chờ tween DOTween xong).

**timeScale** — Hệ số tốc độ thời gian của Unity. 0 = dừng, 1 = thường, 2 = gấp đôi.
*Ở đây:* pause đặt 0, nút tua đặt 2. `Resume()` khôi phục về `SpeedMultiplier` chứ không hardcode 1.

**SetUpdate(true)** — Bảo tween DOTween chạy theo thời gian thực, bỏ qua `timeScale`.
*Bắt buộc cho:* mọi UI hiện khi game pause — popup, diamond, fade chuyển cảnh, hint.

**Animation Event** — Điểm đánh dấu trên clip animation, gọi một hàm public trên GameObject khi phát tới đó.
*Ở đây:* `OnAttackHit` đặt giữa clip bắn cung/phép → gọi `ExecuteHit()` để trừ máu đúng lúc hình ảnh chạm.

**EnhancedTouch** — API New Input System cho phép theo dõi từng ngón tay riêng qua `touchId`.
*Vì sao cần:* pha 2 phải nhận diện "một touch mới", không thể chỉ đọc `activeTouches[0]`.

**Safe Area** — Vùng màn hình không bị notch, camera đục lỗ, hay thanh cử chỉ che.
*Ở đây:* mọi UI gameplay nằm dưới `Canvas/SafeArea/Container`.

**LayoutElement.ignoreLayout** — Bảo LayoutGroup cha bỏ qua phần tử này, để nó tự định vị.
*Hai chỗ:* diamond panel và floating hint — cả hai từng bị layout kéo về sai chỗ.

**Billboard** — Object luôn hướng về camera bất kể cha xoay thế nào.
*Ở đây:* `TDHPBarView.LateUpdate` ghi đè rotation mỗi frame — *cố ý*, vì cha (enemy) xoay theo `LookRotation`.

### Hiệu năng & build

**Draw call / Batch** — Một lệnh CPU gửi GPU yêu cầu vẽ. Càng nhiều lệnh, CPU càng tốn thời gian chuẩn bị.
*Kết quả:* 2.792 → 887 (−68%).

**SetPass call** — Số lần GPU phải đổi shader/material state — đắt hơn draw call thường.
*Kết quả:* 78 → 70.

**Static batching** — Unity gộp mesh của object *không di chuyển* và *cùng material* thành một batch lúc build.
*Ở đây:* bật `StaticEditorFlags` cho environment → tiết kiệm 1.079 batch. Object spawn lúc chạy không batch được.

**GC alloc / GC spike** — Cấp phát bộ nhớ managed; khi đủ nhiều, garbage collector chạy và gây khựng một frame.
*Đã sửa:* pool AudioSource. *Còn lại:* `GetBlockedEnemies()` trả `new List` mỗi frame; lambda trong `Sort()`.

**Shader variant** — Mỗi tổ hợp keyword của shader compile thành một bản riêng. Nhiều keyword → bùng nổ tổ hợp.
*Thủ phạm chính:* shader chiếm 169 MB vì build gánh cả Vulkan lẫn GLES3 — mỗi variant compile hai lần.

**Force-include (Resources)** — Mọi thứ trong thư mục tên `Resources` đều vào build, kể cả không ai tham chiếu.
*Khai thác được:* chuyển 69 MB mesh Sidekick ra ngoài → APK 116 → 102 MB.

**Streaming vs DecompressOnLoad** — Streaming đọc audio dần từ disk; DecompressOnLoad giải nén toàn bộ vào RAM khi load.
*Ở đây:* hai track BGM đổi sang Streaming → 91 MB RAM còn ~256 KB.

**Mesh Read/Write** — Cờ cho phép CPU đọc dữ liệu mesh runtime — khi bật, mesh tồn tại hai bản: RAM và VRAM.
*Ở đây:* tắt cho 355/2.360 model. Cờ do publisher asset store bake sẵn, không phải team bật.

**ASTC** — Định dạng nén texture cho GPU mobile — giữ nguyên dạng nén trong VRAM thay vì giải nén.
*Ở đây:* ASTC 6×6 cho texture Android, kèm hạ `maxTextureSize`.

---

## 13 · Nợ kỹ thuật và bug

> Phát hiện bằng cách đọc mã nguồn ngày 25/08/2026. Mấy mục đầu chưa từng được ghi ở đâu.

### Bug logic

> **✕ BA SAO LÀ BẤT KHẢ THI**
>
> ```csharp
> // TDConstant
> CONFIG_PLAYER_STARTING_LIVES = 20;
>
> // TDGameplayHUDView.CalculateStars
> if (lives >= 30) return 3;   // ← không bao giờ đạt được
> if (lives >= 15) return 2;
> return 1;
> ```
>
> Người chơi bắt đầu với **20** mạng và chỉ có thể mất đi, nên `lives >= 30` không bao giờ đúng. **Điểm tối đa thực tế là 2 sao.** Ngưỡng có lẽ được viết khi số mạng khởi đầu là 40. Sửa: đổi ngưỡng sang tỉ lệ, ví dụ `lives >= starting * 0.9` → 3 sao, `>= starting * 0.5` → 2 sao.

### Code chết — đã kiểm chứng bằng grep, không suy đoán

| Vị trí | Dòng | Tình trạng |
|---|---|---|
| `Services/Utils/Gradient2.cs` | 535 | **0 tham chiếu** |
| `Services/Utils/Gradient.cs` | 127 | **0 tham chiếu** |
| `TDaStarPathControl.FindMultiplePaths()` | ~28 | **không ai gọi** — tàn dư trước khi có maze |
| `IPathFinderDTO` + `FindPath()` + `onGetPath` + `onGetFinalPath` + `SetIndex` | ~20 | **hệ waypoint cũ** |
| `DifficultyRatioTable.Distribute()` | ~12 | **không ai gọi** — wave tự tính tỉ lệ riêng |
| `Services/Utils/Helpers.cs` | 30 | ⚠ **không ai gọi + `using UnityEditor;`** |
| `TDPathGeneratorControl.cs` · `TDObstacleControl.cs` | 2 | **file bia mộ** — chỉ còn một dòng comment |
| `PREFAB_MELEE_OPERATOR`, `CONFIG_MAZE_EXTRA_PASSAGE_RATE`, `MIN_PHASE2_DURATION`, `MIN_CELL_HOLD_DURATION`, `DIRECTION_THRESHOLD_RATIO` | 5 | **hằng số mồ côi** — còn sót sau khi bỏ hệ timer |

**Tổng khoảng 750 dòng, gần 10% codebase.** Xoá hết là thao tác gần như không rủi ro và làm file lớn nhất project trở thành `TDDeployController.cs` — một file gameplay thật.

> **⚠ CẦN KIỂM CHỨNG: Helpers.cs CÓ THỂ CHẶN PLAYER BUILD**
>
> `Services/Utils/Helpers.cs` có `using UnityEditor;` mà **không** bọc `#if UNITY_EDITOR`, và thư mục `2.Scripts` không có asmdef nên nó nằm trong Assembly-CSharp. Về nguyên tắc điều này gây lỗi compile khi build cho thiết bị, vì `UnityEditor` không tồn tại trong player.
>
> Nhưng APK đã build được, nên hoặc file này thêm vào sau lần build cuối, hoặc có gì đó mình chưa thấy. **Cách xử lý gọn nhất né được câu hỏi:** class này không ai gọi — xoá luôn.

### Nợ kiến trúc

| Món nợ | Mức | Chi tiết |
|---|---|---|
| Không có unit test | Cao | Ba hàm thuần dễ test nhất và đáng test nhất: `BuildWavePlans`, `RotateOffset`, maze generator. Cả ba không cần dựng scene. |
| Hai grid song song | Cao | `TDGridMainModel` và `TDGridDTO` phải đồng bộ thủ công. Đã sinh bug "path cell leak" đang được vá bằng vòng lọc thủ công. |
| Hai cơ chế event song song | Trung bình | `TDGameEventBus` (static event) và `Action<T>` callback trực tiếp trên control. Một mình quản được, nhiều người thì không. |
| Hơn 20 static singleton | Trung bình | Hidden dependency, không test được. Đã cân nhắc và chọn có ý thức. |
| `GoldControl.Tick()` nằm trong `HUDView.Update()` | Thấp | View đang chạy logic game. Vi phạm SRP nhưng vô hại ở quy mô này. |
| GC alloc còn ở hot path | Thấp | `GetBlockedEnemies()` trả `new List` mỗi frame; lambda trong `ScanForTargets().Sort()` mỗi 0,2 giây mỗi tower. |
| `PerWaveStrategy` trùng `RoundRobinStrategy` | Thấp | Thân hàm giống hệt nhau. Gộp lại hoặc làm cho khác thật. |
| `Debug.Log` có tag màu trong production | Thấp | Nên bọc `#if UNITY_EDITOR` hoặc dùng `[Conditional]`. |
| Comment không khớp code ở `PlayHideRootAnim` | Thấp | Comment nói Sequence + OnKill, code dùng tween + OnComplete. |
| `TDGateEndView` hiện `"♥ {lives}"` | Thấp | Ký tự text thay vì sprite icon — không đồng bộ với HUD. |

### Thứ tự nên xử lý

1. **Xoá code chết** — nửa tiếng, gần như không rủi ro, hiệu ứng rõ ngay
2. **Sửa ngưỡng tính sao** — vài dòng, đang làm hỏng một tính năng người chơi nhìn thấy
3. **Viết ba unit test** cho ba hàm thuần — mở đường cho mọi refactor về sau
4. **Hợp nhất hai grid** — việc lớn nhất, nhưng gỡ được cả một lớp bug

---

## 14 · Bản đồ file

> Muốn sửa một thứ thì mở file nào.

| Muốn đổi… | Mở file |
|---|---|
| Hình dạng map, độ dài đường đi, số corridor | `TDMazePathGenerator.cs` · `TDConstant.CONFIG_NUM_PATHS` |
| Vị trí và số lượng cổng | `TDGatePlacer.cs` · Stage Config SO |
| Nhịp wave, số enemy, độ khó | `TDEnemyPathMainControl.BuildWavePlans` · `DifficultyRatioTable` · Level Config SO |
| Chỉ số enemy | Enemy Data Config SO |
| Chỉ số operator, tầm đánh, giá | Melee Operator Config SO |
| Cách tính tầm đánh | `TDOffsetRangeDTO.cs` |
| Cơ chế chặn đường | `TDOperatorRegistry.cs` · `TDEnemyView.Update()` |
| Cử chỉ đặt unit | `TDDeployController.cs` |
| Giao diện chọn hướng / rút quân | `TDDiamondPanelView.cs` · `DiamondPanel.prefab` |
| VFX và SFX theo sự kiện | Effect Config SO · `TDEffectManager.cs` |
| Nhạc nền | Stage Config SO · `TDGameplayAudioContext.cs` |
| HUD, popup thắng/thua | `TDGameplayHUDView.cs` · `TDConstant.PATH_*` |
| Menu chính, chọn màn | `TDMainMenuView.cs` · `TDStageSelectView.cs` |
| Tutorial | `TDTutorialView.cs` · `TDFloatingHint.cs` |
| Chuyển cảnh, fade | `TDSceneController.cs` |
| Thứ tự khởi tạo | `TDControl.InitOtherControl()` |
| Mọi hằng số tinh chỉnh | `TDConstant.cs` |

---

*Tài liệu dựng bằng cách đọc trực tiếp mã nguồn tại `G:\Unity\Tower Defense\Assets\2.Scripts`, không lấy từ changelog. Số liệu codebase đếm trực tiếp: 116 file C#, 7.886 dòng, 74 commit — ngày 25/08/2026. Các chỉ số render và build size lấy từ nhật ký đo đạc trong `RECENT_SESSIONS_CHANGELOG.txt` và chưa được đo lại ở lần đọc này.*
