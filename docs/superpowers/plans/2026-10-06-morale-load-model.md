# Morale — Mô hình tải (Tầng 0 + 1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Thay vùng áp lực 3×3, ngưỡng `1 + blockCount` và đồng hồ nền N2 bằng một mô hình tải dựa trên nền kinh tế chặn. Mô hình gồm: trần triển khai chung, sự kiện lọt chia 7/3, chỉ số địch / operator / turret và level × độ khó được cân lại, cùng hai loại địch mới. Có đo và hiệu chỉnh bằng bot.

**Architecture:**
- Model thuần (`TDOperatorMorale`, `TDLeakShare`, `TDDeployCap`, `DifficultyRatioTable`) chứa luật và được test bằng Editor validator.
- Thế giới game chỉ gọi vào model ở ba điểm:
  - `TDEnemyView`: phát hiện lọt.
  - `TDTowerFactoryControl` và `IPlacedUnit.OnRemove`: đếm trần.
  - `TDEnemyPathMainView`: khởi tạo trần và vàng theo level × độ khó.
- Công cụ thiết kế và đo (`TDLoadModel`, `TDCalibrationBot`) nằm hoàn toàn trong Editor assembly.

**Tech Stack:** Unity 2022.3.55f1, C# (Assembly-CSharp), Editor validator (`Tools ▸ TD ▸ …`), Unity MCP (`script-execute`, `assets-refresh`, `console-get-logs`, `screenshot-game-view`, `profiler-*`).

**Spec:** [`docs/superpowers/specs/2026-10-06-morale-load-model-design.md`](../specs/2026-10-06-morale-load-model-design.md). Mọi số liệu trong plan này chép từ spec. Khi plan và spec lệch nhau thì **spec thắng**, và phải sửa plan.

## Global Constraints

- Test là Editor validator. Không có NUnit. Pass nghĩa là cả hai dòng sau đều 0 failures, chạy bằng MCP `script-execute`:
  ```csharp
  public class V { public static string Main() {
    var m = TDMoraleValidator.Run(); var b = TDBalanceValidator.Run();
    return $"TDMoraleValidator: {(m.Count == 0 ? "PASS" : "FAIL")} — {m.Count} failures\n{string.Join("\n", m)}\n" +
           $"TDBalanceValidator: {(b.Count == 0 ? "PASS" : "FAIL")} — {b.Count} failures\n{string.Join("\n", b)}";
  } }
  ```
- Sau mỗi lần sửa code: MCP `assets-refresh`, rồi `console-get-logs`. Console không được có lỗi compile.
- Vào hoặc thoát Play Mode làm MCP mất kết nối khoảng 15–20 giây. Đợi bằng `sleep` chạy nền, rồi kiểm `list_engine_instances`.
- Vào Play Mode: `EditorApplication.isPlaying = true`, sau đó `TDSceneController.api.GoToGameplay("DEMO-1")`. Chơi lại: `RetryGameplay()`.
- Mọi lệnh ghi stress đi qua `TDOperatorMorale.SetValue`.
- Hằng số của spec, chép nguyên văn:
  - `STRESS_PER_LEAK = 10`
  - `LEAK_SHARE_MELEE = 0.7`
  - `HERALD_LEAK_MULT = 2`
  - `HERALD_RADIUS = 4` ô, đo khoảng cách Euclid, không cộng dồn
  - Hệ số band 1 / 1,5 / 2 nhân vào nhịp lọt
  - `HORDE_PACK_SPAWN_INTERVAL = 0.2`, một bầy 5 con
  - `SPAWN_INTERVAL_FLOOR = 0.8` s; `mixRamp` 1,0 / 0,8 / 0,4 (D14)
- Turret và operator đang suy sụp **không bao giờ** nhận phần lọt. Địch được thả (`ForceUnblock`) **không** tính là lọt.
- Công cụ đo, bot và mô hình ρ chỉ nằm trong `Assets/2.Scripts/Editor/`. Không thêm MonoBehaviour công cụ vào code sản phẩm.
- File `.cs` dùng CRLF. Nếu `Write` tạo ra LF thì chuyển bằng `sed -i 's/$/\r/'`.
- Comment tiếng Anh, giải thích *vì sao*, cùng giọng với code xung quanh. Nhãn UI tiếng Anh viết hoa.
- Mỗi task một commit trên `feature/morale-system`, và chỉ `git add` đúng các đường dẫn đã sửa. **Không bao giờ** stage `Packages/`, `ProjectSettings/PackageManagerSettings.asset`, `Assets/Plugins/NuGet*`, `.mcp.json`, `.claude/`, `*.bak`, `TDEffectManager.cs`, `DOTweenSettings.asset`.
- Mọi commit message kết thúc bằng `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

1. **Bộ đếm trần bị lệch** khi một unit rời sân theo đường lạ: chết giữa clip Die, bấm Retreat lên người đang suy sụp (bị từ chối), gỡ turret. Bộ đếm phải luôn bằng số unit thật trên sân, và không bao giờ âm. Kiểm bằng `CAP_*` và bước đếm trong Play Mode ở Task 3.
2. **Chơi lại hoặc qua màn:** trận mới phải bắt đầu ở `0/Limit`, với `Limit` lấy từ level mới. Kiểm ở Task 3, bước 6.
3. **Tổng địch nhỏ khi có độ tăng wave:** mọi wave ≥ 1 con, wave boss ≥ `bossPerWave + 1`, tổng luôn đúng với tổng địch từ 8 tới 200. Kiểm bằng `GROWTH_*` ở Task 7.
4. **Lọt qua melee đang suy sụp mà không có xạ thủ phủ ô:** không ai nhận stress, không có exception, và con địch vẫn đánh người suy sụp một đòn khi đi qua. Kiểm bằng `SPLIT_NOBODY` và bước Play Mode (d) ở Task 2.
5. **Xạ thủ quay hướng khác, hoặc rời sân giữa wave**, thì không còn nhận phần lọt của ô đó nữa. Kiểm ở Task 2, bước Play Mode (c).
6. **Khoảng cách sinh ở cấu hình biên:** `spawnInterval` của level đã dưới sàn, `waveGrowth < 1`, level chỉ có 1 wave. Không wave nào được thưa hơn `spawnInterval`, và không bị nâng lên sàn. Kiểm bằng `DENSITY_*` ở Task 9.
7. **Mô hình ρ và game lệch nhau:** vòng sinh wave và `TDLoadModel` phải lấy khoảng cách sinh từ **cùng** `SpawnIntervalFor`, thành phần từ **cùng** `Distribute`. Kiểm ở Task 9, bước 7.

---

### Task 0: Đóng Task 3 của plan M1 cũ

Task 3 (slow-mo khi chọn lính, sửa selection bị kẹt sau khi huỷ Rescue) đã code xong nhưng chưa commit. Các mốc sau dựa trên trạng thái đó.

**Files:** đã sửa sẵn trong working tree, không sửa thêm:
- `Assets/2.Scripts/Model/Config/Constant/TDConstant.cs`
- `Assets/2.Scripts/Control/Gameplay/TDSpeedControl.cs`
- `Assets/2.Scripts/Control/Gameplay/TDPauseControl.cs`
- `Assets/2.Scripts/View/GamePlay/Tower/TDOperatorSelectionView.cs`
- `Assets/2.Scripts/Editor/TDMoraleValidator.cs`

- [ ] **Step 1: Hai dòng kiểm còn thiếu của Task 3 cũ, bước 6.** Play Mode DEMO-1, đọc qua `script-execute`:
  - Huỷ chọn mục tiêu Rescue. Expected: `timeScale` = 1, không ai được chọn, `picking = false`.
  - Bấm lại người cứu. Expected: diamond Retreat hiện.
  - Chọn một lính rồi gọi `TDGameEventBus.Victory()`. Expected: `timeScale` = 0.
- [ ] **Step 2: Thoát Play Mode, chạy validator.** Expected: hai dòng PASS. Ghi kết quả vào `.superpowers/sdd/2026-10-05-morale-system-update/validator-task-3.txt`.
- [ ] **Step 3: Commit** đúng 5 file trên, message `feat(morale): slow-mo while an operator is selected; cancelling a rescue pick deselects`.
- [ ] **Step 4: Ghi ledger** `task-done 3`, với base `2bf3d6e2683a6e9176c314cd0bb6143ab3185cb9`.

---

## Mốc A — Trần, sự kiện lọt, xạ thủ vào hệ, gỡ hệ cũ

### Task 1: Model — lọt thay N1 / N2

**Files:**
- Modify: `Assets/2.Scripts/Model/Info/Morale/TDOperatorMorale.cs`: `TDMoraleContext` (10-29), `NetRate` (103-136), thêm `OnLeak`, thay `SecondsToBreak` (321-349)
- Modify: `Assets/2.Scripts/Model/Config/Constant/TDConstant.cs`: khối `STRESS_*` (~371-392), comment ~482
- Modify: `Assets/2.Scripts/Control/Tower/OperatorBehavior/IOperatorBehavior.cs`, `PathCellOperatorBehavior.cs`, `TowerZoneOperatorBehavior.cs`
- Modify: `Assets/2.Scripts/View/GamePlay/Tower/TDOperatorView.cs`: `TickMorale` (107-143)
- Modify: `Assets/2.Scripts/View/GamePlay/Tower/TDMoraleDebugOverlay.cs`: dòng 9 và 117
- Test: `Assets/2.Scripts/Editor/TDMoraleValidator.cs`

**Interfaces:**
- Produces:
  - `TDMoraleContext`: bỏ `enemiesInZone`, `tolerance`. Thêm `public bool engaged`. Giữ `calmAlliesAdjacent`, `auraRate`, `secondsSinceHit`, `allyBrokeThisWave`.
  - `public float TDOperatorMorale.OnLeak(float share, float amplifier)`:
    - Trả 0 và không đổi gì nếu `IsBroken` hoặc `share <= 0`.
    - Ngược lại cộng `STRESS_PER_LEAK × share × amplifier × MultiplierOf(State)` qua `SetValue`, rồi trả số điểm **thực** đã cộng (sau clamp).
  - `public int TDOperatorMorale.LeaksToBreak(float share)`:
    - Số lần lọt (amplifier 1, không tính hồi) để từ giá trị hiện tại lên 100. Đi qua từng band, mỗi band nhân hệ số của nó.
    - Trả 0 nếu đang suy sụp, `int.MaxValue` nếu `share <= 0`.
    - Thay hẳn `SecondsToBreak`.
  - `bool IOperatorBehavior.IsEngaged(Vector2Int cell)`:
    - Melee: đang chặn ≥ 1, hoặc có địch trong tầm.
    - Xạ thủ: có địch trong tập ô đang bắn được.
  - Hằng số mới: `STRESS_PER_LEAK = 10f`, `LEAK_SHARE_MELEE = 0.7f`, `HERALD_LEAK_MULT = 2f`, `HERALD_RADIUS = 4f`.
  - Gỡ: `STRESS_BASE_RATE`, `STRESS_PER_OVERLOAD`, `STRESS_ALLY_CALM_RELIEF`, `STRESS_RELIEF_CAP`.

- [ ] **Step 1: Viết lại validator theo luật mới.**
  - Helper `Ctx(bool engaged, int calmAllies = 0, float aura = 0f, float sinceHit = 0f)`.
  - Sửa mọi lời gọi cũ `Ctx(n, tol, …)`: dùng `Ctx(true, …)` khi trước đó `enemies > 0`, ngược lại `Ctx(false, …)`.
  - Xoá `ALLY_1`, `ALLY_CAP`, `ALLY_CANNOT_EAT_AURA`, `RELIEF_SIGN`, `N1_OVER_4`, `N2*`, `MULT_*` dạng tốc độ, và toàn bộ `BreakTimes`.
  - Thêm:

```csharp
// Rates: no clock any more — only the aura accrues, and idling recovers.
Near(f, "ENGAGED_IS_FLAT", new TDOperatorMorale().NetRate(Ctx(true)), 0f);
Near(f, "IDLE_RELIEF", new TDOperatorMorale().NetRate(Ctx(false)), -TDConstant.STRESS_IDLE_RELIEF);
Near(f, "N4_AURA", new TDOperatorMorale().NetRate(Ctx(true, aura: TDConstant.STRESS_AURA_BOSS)), TDConstant.STRESS_AURA_BOSS);
Near(f, "N4_AURA_MULT", At(70f).NetRate(Ctx(true, aura: TDConstant.STRESS_AURA_BOSS)),
     TDConstant.STRESS_AURA_BOSS * TDConstant.STRESS_MULT_STRESSED);
Near(f, "ALLY_NO_STRESS_EFFECT", new TDOperatorMorale().NetRate(Ctx(true, calmAllies: 5)), 0f);

// Leaks: one leak alone = 10 / 15 / 20 by band.
Near(f, "LEAK_CALM", At(10f).OnLeak(1f, 1f), 10f);
Near(f, "LEAK_STEADY", At(50f).OnLeak(1f, 1f), 15f);
Near(f, "LEAK_STRESSED", At(70f).OnLeak(1f, 1f), 20f);
Near(f, "LEAK_AMPLIFIED", At(0f).OnLeak(1f, TDConstant.HERALD_LEAK_MULT), 20f);
var broken = At(TDConstant.STRESS_MAX); int sb = broken.Setbacks;
Near(f, "LEAK_IGNORED_WHEN_BROKEN", broken.OnLeak(1f, 1f), 0f);
if (broken.Setbacks != sb) f.Add("[LEAK_IGNORED_WHEN_BROKEN] a leak on a collapsed operator counted a setback");

// Spec §5.2 table: 8 / 11 / 25 leaks from zero to collapse.
foreach (var (tag, share, want) in new[] { ("LEAKS_ALONE", 1f, 8), ("LEAKS_SUPPORTED", 0.7f, 11), ("LEAKS_RANGED", 0.3f, 25) })
{
    var m = new TDOperatorMorale(); int n = 0;
    while (!m.IsBroken && n < 100) { m.OnLeak(share, 1f); n++; }
    if (n != want) f.Add($"[{tag}] {n} leaks to collapse, want {want}");
    if (new TDOperatorMorale().LeaksToBreak(share) != want) f.Add($"[{tag}_PROJECTION] LeaksToBreak disagrees with the walk");
    if (!m.ConsumeBreak()) f.Add($"[{tag}_EDGE] a leak-induced collapse raised no edge");
}
if (new TDOperatorMorale().LeaksToBreak(0f) != int.MaxValue) f.Add("[LEAKS_NO_SHARE] share 0 reported a finite count");
```
  - `BreakEdge` / `EDGE_FROM_TICK`: tick bằng `Ctx(true, aura: TDConstant.STRESS_AURA_BOSS)`. Aura giờ là nguồn tăng theo thời gian duy nhất còn lại.
  - `MULT_*`: thay bằng ba dòng `LEAK_CALM/STEADY/STRESSED` ở trên. Giữ `BAND` và `SPIKE_UNMULTIPLIED`.

- [ ] **Step 2: Chạy. Xác nhận fail.** Expected: lỗi compile `CS1061 'OnLeak'`, `'LeaksToBreak'`, `'engaged'`.
- [ ] **Step 3: Cài model theo Interfaces.**
  - `NetRate` (nhánh không suy sụp) = `auraRate × MultiplierOf(State) − (engaged ? 0 : STRESS_IDLE_RELIEF)`.
  - Comment phần đầu class: bỏ N1 / N2, chỉ ra rằng lọt là sự kiện có nhân band. Lý do ghi trong spec §5.2: tạm giữ "điểm không quay đầu" tới Tầng 2.
- [ ] **Step 4: Nối view.**
  - `TickMorale` dựng context bằng `engaged = m_Behavior.IsEngaged(m_MyCell)`.
  - Bỏ `enemiesInZone`, `tolerance`.
  - `TDOperatorRoster.k_OffField` để nguyên: `engaged = false` mặc định nghĩa là hồi khi nằm ngoài sân, đúng hành vi cũ.
- [ ] **Step 5: Overlay.** In ra `"{n} leaks→break"`, với `n = LeaksToBreak(op.Data.deployZone == DeployZone.TowerZone ? 1f - TDConstant.LEAK_SHARE_MELEE : 1f)`. Sửa comment dòng 9 và comment `TDConstant` ~482 cho khỏi nhắc `STRESS_BASE_RATE`.
- [ ] **Step 6: Chạy validator.** Expected: hai dòng PASS. Console không có lỗi compile.
- [ ] **Step 7: Commit** các file của task. Message `feat(morale): stress comes from leaks, not from enemies being near`.

### Task 2: Thế giới — sự kiện lọt, xạ thủ vào danh sách morale

**Files:**
- Create: `Assets/2.Scripts/Model/Info/Morale/TDLeakShare.cs`
- Modify: `Assets/2.Scripts/Control/Tower/TDOperatorRegistry.cs`: thêm `ReportLeak` (khối Morale ~166-225)
- Modify: `Assets/2.Scripts/View/GamePlay/Enemy/TDEnemyView.cs`: nhánh `else` (212-233)
- Modify: `Assets/2.Scripts/Control/Tower/OperatorBehavior/IOperatorBehavior.cs`, `PathCellOperatorBehavior.cs`, `TowerZoneOperatorBehavior.cs`: `OnInit`/`OnRemove` (33-43), `Covers`
- Modify: `Assets/2.Scripts/View/GamePlay/Tower/TDOperatorView.cs`: `ReceiveLeak`, `Covers`. Bỏ `TDPressureProbe.Sample` (249) và trường `m_NextPressureSample`
- Modify: `Assets/2.Scripts/Control/Tower/TDPressureProbe.cs`: xoá `Count`, `Tolerance`, `Sample`, các bộ đếm vùng và `Report`. **Giữ `AuraRateAt`**
- Test: `Assets/2.Scripts/Editor/TDMoraleValidator.cs`: section `LeakSplit`

**Interfaces:**
- Consumes (Task 1): `TDOperatorMorale.OnLeak(float, float)`, `TDConstant.LEAK_SHARE_MELEE`.
- Produces:
  - `public static (float melee, float eachRanged) TDLeakShare.Split(bool meleeStanding, int rangedCount)`.
  - `public void TDOperatorRegistry.ReportLeak(Vector2Int cell)`.
  - `private float TDOperatorRegistry.LeakAmplifierAt(Vector2Int cell)`: trả `1f` cho tới Task 13. Đánh dấu `// ponytail:`, trỏ tới task Kẻ gieo sợ.
  - `public float TDOperatorView.ReceiveLeak(float share, float amplifier)`: trả số điểm đã cộng.
  - `public bool TDOperatorView.Covers(Vector2Int cell)`.
  - `bool IOperatorBehavior.Covers(Vector2Int myCell, Vector2Int target)`:
    - Melee: `false`.
    - Xạ thủ: `target` nằm trong `m_RangeDTO.GetCellsInRange(myCell, m_OperatorTransform.rotation)`, đúng tập ô mà `TryAttack` dùng.

- [ ] **Step 1: Viết assertion** (section mới, gọi trong `Run()`):

```csharp
private static void LeakSplit(List<string> f)
{
    void Is(string tag, (float m, float r) got, float m, float r)
    { Near(f, tag + "_MELEE", got.m, m); Near(f, tag + "_RANGED", got.r, r); }

    Is("SPLIT_ALONE",        TDLeakShare.Split(true, 0),  1f,   0f);
    Is("SPLIT_SUPPORTED",    TDLeakShare.Split(true, 1),  0.7f, 0.3f);
    Is("SPLIT_TWO_RANGED",   TDLeakShare.Split(true, 2),  0.7f, 0.15f);
    Is("SPLIT_MELEE_BROKEN", TDLeakShare.Split(false, 2), 0f,   0.5f);
    Is("SPLIT_NOBODY",       TDLeakShare.Split(false, 0), 0f,   0f);

    for (int k = 0; k <= 4; k++)
    {
        var s = TDLeakShare.Split(true, k);
        Near(f, $"SPLIT_SUMS_{k}", s.melee + k * s.eachRanged, 1f);
    }
}
```
- [ ] **Step 2: Chạy. Xác nhận fail.** Expected: `CS0103 'TDLeakShare'`.
- [ ] **Step 3: Cài `TDLeakShare.Split`.** Đây là luật ở bảng §5.2 của spec.
- [ ] **Step 4: Cài `ReportLeak`.**
  - Melee: `GetOperatorView(cell)` nếu chưa suy sụp.
  - Xạ thủ: mọi view trong `m_OperatorViews` có `Data.deployZone == TowerZone`, chưa suy sụp, và `Covers(cell)`.
  - Chia theo `Split`, nhân `LeakAmplifierAt(cell)`, gọi `ReceiveLeak` cho từng người.
  - Một dòng log mỗi lần lọt: `[Leak] {cell} → Knight +10.0, Ginger +3.0` hoặc `[Leak] {cell} → nobody`.
- [ ] **Step 5: Nối nguồn.**
  - Trong nhánh `else` của `TDEnemyView.Update`, **trước** khối đánh người suy sụp: `if (TDOperatorRegistry.api.HasOperatorAt(arrivedCell)) TDOperatorRegistry.api.ReportLeak(arrivedCell);`. Không cần luật riêng cho địch được thả: `ForceUnblock` đẩy chỉ số đường đi qua ô, nên chúng không đi vào nhánh này tại ô đó.
  - `TowerZoneOperatorBehavior.OnInit` gọi `RegisterOperatorView(cell, view)`.
  - `OnRemove` gọi `UnregisterOperator(cell)`. Hàm này an toàn với xạ thủ: không có danh sách chặn, nên phần thả địch return sớm.
  - Xoá mã vùng áp lực trong `TDPressureProbe` và lời gọi `Sample` trong view.
- [ ] **Step 6: Chạy validator.** Expected: hai dòng PASS.
- [ ] **Step 7: Kiểm trong Play Mode** (DEMO-1, đặt quân bằng `TDControl.CreateOperatorBehavior(...).Place(...)`, đọc log `[Leak]` và `Morale.Value`):
  - (a) Knight đứng một mình, chờ wave: mỗi lần lọt +10 khi Calm, +15 khi Steady.
  - (b) Đặt Ginger có tầm phủ ô của Knight: Knight +7, Ginger +3 (khi Calm).
  - (c) Xoay Ginger sang hướng không phủ ô đó (`transform.rotation`): chỉ Knight nhận. Rút Ginger giữa wave: Ginger không còn xuất hiện trong log.
  - (d) `Knight.Morale.AddSpike(100)`, không có xạ thủ: log `→ nobody`, không exception, HP Knight vẫn giảm mỗi lần có địch đi qua.
  - (e) Rút Knight khi đang chặn 2 con: không có dòng `[Leak]` nào tại ô đó cho 2 con được thả.
  
  Ghi kết quả (a)–(e) vào file kết quả của task.
- [ ] **Step 8: Commit.** Message `feat(morale): enemies passing a full blocker are leaks, shared 70/30 with covering ranged`.

### Task 3: Trần triển khai

**Files:**
- Create: `Assets/2.Scripts/Control/Tower/TDDeployCap.cs`
- Modify: `Assets/2.Scripts/Control/TDControl.cs`: `InitOtherControl` (29-59)
- Modify: `Assets/2.Scripts/Control/Tower/TowerFactory/TDTowerFactoryControl.cs`: sau `unit.Init` (24)
- Modify: `Assets/2.Scripts/View/GamePlay/Tower/TDOperatorView.cs`: `IPlacedUnit.OnRemove` (94-98)
- Modify: `Assets/2.Scripts/View/GamePlay/Tower/TDTowerWeaponView.cs`: `IPlacedUnit.OnRemove` (35-39)
- Modify: `Assets/2.Scripts/Control/Tower/PlaceTower/TDPlaceTowerControl.cs`: `CheckPlaceTower` (11-25)
- Modify: `Assets/2.Scripts/View/GamePlay/Tower/TDSlotHolderItemView.cs`: `CanSelect`
- Modify: `Assets/2.Scripts/View/GamePlay/Tower/TDSlotHolderMainView.cs`: refresh khi trần đổi, báo bị từ chối (~105)
- Modify: `Assets/2.Scripts/View/GamePlay/TDGameplayHUDView.cs`, `TDConstant.cs`: `PATH_GAMEPLAY_HUD_DEPLOY_CAP`
- Modify: `Assets/2.Scripts/Model/Config/TDLevelConfigSettings.cs`: `LevelConfig.deployLimit`
- Modify: `Assets/1.Assets/Resources/Configs/Level Config.asset`: `deployLimit: 5` cho cả hai level
- Modify: `Assets/2.Scripts/View/GamePlay/EnemyPath/TDEnemyPathMainView.cs`: sau khi đọc `config` (~156)
- Modify: hierarchy HUD trong scene gameplay: nhân bản `Bottom/Currency` thành `Bottom/DeployCap`
- Test: `Assets/2.Scripts/Editor/TDMoraleValidator.cs`: section `DeployCap`

**Interfaces:**
- Produces:

```csharp
public sealed class TDDeployCap
{
    public static TDDeployCap api;
    public event Action OnChanged;     // OnField or Limit changed
    public event Action OnRejected;    // a placement was refused because the cap is full
    public int Limit { get; private set; } = TDConstant.CONFIG_MAX_SLOTS; // until Initialize
    public int OnField { get; private set; }
    public bool IsFull => OnField >= Limit;
    public void Initialize(int limit); // limit <= 0 → LogWarning, CONFIG_MAX_SLOTS. Resets OnField to 0.
    public void OnUnitPlaced();
    public void OnUnitRemoved();       // floored at 0
    public void NotifyRejected();
    public static int LimitFor(LevelConfig level); // level.deployLimit — Task 7 adds the difficulty delta
}
```

- [ ] **Step 1: Viết assertion:**

```csharp
private static void DeployCap(List<string> f)
{
    var cap = new TDDeployCap();
    cap.Initialize(2);
    if (cap.OnField != 0 || cap.IsFull) f.Add("[CAP_STARTS_EMPTY]");
    cap.OnUnitPlaced(); cap.OnUnitPlaced();
    if (!cap.IsFull) f.Add("[CAP_FULL_AT_LIMIT] 2/2 not full");
    cap.OnUnitRemoved();
    if (cap.IsFull || cap.OnField != 1) f.Add("[CAP_FREES] removing one did not free a slot");
    cap.OnUnitRemoved(); cap.OnUnitRemoved();
    if (cap.OnField != 0) f.Add($"[CAP_NEVER_NEGATIVE] OnField {cap.OnField}");
    cap.Initialize(0);
    if (cap.Limit != TDConstant.CONFIG_MAX_SLOTS) f.Add("[CAP_BAD_LIMIT] limit 0 was accepted");
    cap.OnUnitPlaced(); cap.Initialize(3);
    if (cap.OnField != 0) f.Add("[CAP_RESET_ON_INIT] a new match inherited units");
}
```
- [ ] **Step 2: Chạy. Xác nhận fail.** Expected: `CS0246 'TDDeployCap'`.
- [ ] **Step 3: Cài `TDDeployCap`, rồi nối dây.**
  - `TDControl` tạo `api` cạnh `TDOperatorRoster`.
  - Factory gọi `OnUnitPlaced()` sau `unit.Init`.
  - Hai `OnRemove` gọi `OnUnitRemoved()`. Ở operator, đặt **trong** guard `m_Initialized`, vì `Die` và `DoRetreat` đều đi qua `OnRemove` và chỉ được trừ một lần.
  - `CheckPlaceTower` return sớm và gọi `NotifyRejected()` khi `IsFull`.
  - `CanSelect(gold)` thêm điều kiện `&& !(TDDeployCap.api?.IsFull ?? false)`.
  - `TDSlotHolderMainView`: đăng ký `OnChanged`, gọi `RefreshAvailability`. Khi tap vào một card bị chặn **vì trần đầy**, gọi `NotifyRejected()`.
  - `TDEnemyPathMainView`: ngay sau khi đọc `config`, gọi `TDDeployCap.api?.Initialize(TDDeployCap.LimitFor(config))`.
- [ ] **Step 4: Bộ đếm HUD.**
  - Bằng MCP: nhân bản `Bottom/Currency` thành `Bottom/DeployCap`, đặt bên trái Currency, tắt `Image` icon đồng xu trong bản sao.
  - Thêm `PATH_GAMEPLAY_HUD_DEPLOY_CAP = "Bottom/DeployCap/TxtValue"`.
  - HUD hiện `"{OnField}/{Limit}"` mỗi khi `OnChanged` bắn.
  - `OnRejected` gọi `SafePunch` lên text, và đổi màu sang `#FB2425` trong 0,3 giây.
- [ ] **Step 5: Chạy validator.** Expected: hai dòng PASS.
- [ ] **Step 6: Kiểm trong Play Mode** (DEMO-1, Hard, `Limit` 5):
  - Đặt 5 unit, gồm 1 turret. HUD hiện `5/5`. Lần đặt thứ 6 bị từ chối, card mờ, bộ đếm nháy.
  - `AddSpike(100)` lên một người: vẫn `5/5`.
  - Rút một operator khác: `4/5`, card sáng lại.
  - Gỡ turret: `3/5`.
  - Giết một operator (`TakeDamage(99999)`): `2/5` **ngay** khi clip Die bắt đầu.
  - So `OnField` với `TDOperatorRoster` (số đang deploy) cộng số `TDTowerWeaponView` còn sống: phải bằng nhau.
  - `RetryGameplay()`: HUD `0/5`.
- [ ] **Step 7: Commit.** Message `feat(morale): per-level deploy cap shared by operators and turrets`.

### Task 4: Phản hồi tối thiểu khi lọt

**Files:**
- Modify: `Assets/2.Scripts/View/GamePlay/Tower/TDMoraleIconView.cs`: thêm `Pulse()`
- Create: `Assets/2.Scripts/View/GamePlay/Tower/TDBlockFullMarker.cs`
- Modify: `Assets/2.Scripts/Control/Tower/TDOperatorRegistry.cs`: thêm `IsFullAt`
- Modify: `Assets/2.Scripts/View/GamePlay/Tower/TDOperatorView.cs`: `ReceiveLeak` gọi `Pulse`, `Update` bật / tắt marker

**Interfaces:**
- Consumes (Task 2): `TDOperatorView.ReceiveLeak`.
- Produces:
  - `public void TDMoraleIconView.Pulse()`: phồng thêm 25% rồi về trong 0,2 giây. Gọi lại khi đang phồng thì chạy lại từ đầu, không cộng dồn.
  - `public static TDBlockFullMarker TDBlockFullMarker.Attach(Transform owner)` và `public void SetVisible(bool visible)`:
    - Một quad phẳng dựng bằng code, nằm trên mặt đất dưới chân operator (y + 0,02, rộng 0,9 ô).
    - Shader `TDConstant.SHADER_URP_UNLIT`, màu `#FB2425` alpha 0,6.
    - Cùng cách dựng với `TDMoraleIconView.Attach`.
  - `public bool TDOperatorRegistry.IsFullAt(Vector2Int cell)`: có melee ở ô, `count >= capacity`, và người đó chưa suy sụp.

- [ ] **Step 1: Cài ba phần trên, nối vào view.**
  - Marker chỉ gắn cho melee.
  - Hiện khi `IsFullAt(m_MyCell)` là true.
- [ ] **Step 2: Kiểm trong Play Mode.**
  - Knight chặn đủ 2 con: chụp `screenshot-game-view`, marker phải thấy được.
  - Một con chết: marker tắt.
  - Ngay sau một dòng `[Leak]`: đọc `localScale` của icon, phải lớn hơn scale gốc theo band.
- [ ] **Step 3: Chạy validator.** Expected: hai dòng PASS, vì không đụng gì tới model.
- [ ] **Step 4: Commit.** Message `feat(morale): leak pulse on the icon, full-block marker under blockers`.

---

## Mốc B — Chỉ số

### Task 5: Melee đọc `attackType`, Moon nổ lan

**Files:**
- Modify: `Assets/2.Scripts/Control/Tower/OperatorBehavior/PathCellOperatorBehavior.cs`: `TryAttack` (67-84), comment lớp (19-22)
- Modify: `Assets/2.Scripts/Model/Config/TDFlyweightOperatorDataSettings.cs`: `OperatorData`
- Modify: `Assets/2.Scripts/Control/Tower/OperatorBehavior/TowerZoneOperatorBehavior.cs`: `ExecuteHit` (77-85)
- Modify: `Assets/1.Assets/Resources/Configs/Melee Operator Config.asset`: `attackType` của Defender, Striker, Layla đổi sang `0` (Single). Ace giữ `1`. Moon `splashRadius: 1`
- Test: `Assets/2.Scripts/Editor/TDMoraleValidator.cs`: section `Targets`

**Interfaces:**
- Produces:
  - `public static List<T> PathCellOperatorBehavior.SelectTargets<T>(IReadOnlyList<T> blocked, T inRange, AttackType type) where T : class`:
    - `Multiple` và có địch đang bị chặn: trả tất cả.
    - Có địch đang bị chặn: trả `[blocked[0]]`.
    - Không chặn ai mà `inRange != null`: trả `[inRange]`.
    - Còn lại: rỗng.
  - `public int OperatorData.splashRadius`: tooltip "0 = single target; N = also hits every enemy within N cells (Chebyshev) of the target".

- [ ] **Step 1: Viết assertion:**

```csharp
var two = new List<string> { "a", "b" };
var none = new List<string>();
Seq(f, "TARGETS_SINGLE_BLOCKED", PathCellOperatorBehavior.SelectTargets(two, "c", AttackType.Single), "a");
Seq(f, "TARGETS_MULTI_BLOCKED",  PathCellOperatorBehavior.SelectTargets(two, "c", AttackType.Multiple), "a", "b");
Seq(f, "TARGETS_FREE_IN_RANGE",  PathCellOperatorBehavior.SelectTargets(none, "c", AttackType.Multiple), "c");
Seq(f, "TARGETS_NONE",           PathCellOperatorBehavior.SelectTargets(none, null, AttackType.Single));
```
  Helper `Seq(f, tag, List<string> got, params string[] want)` so sánh theo thứ tự.
- [ ] **Step 2: Chạy. Xác nhận fail.** Expected: `CS0117 'SelectTargets'`.
- [ ] **Step 3: Cài.**
  - `TryAttack` gọi `TakeDamage` cho mọi mục tiêu `SelectTargets` trả về.
  - Comment lớp ghi rõ: `blockCount` là số giữ, `attackType` là số đánh, và chỉ data được quyết định (lý do: lần trước mở rộng mục tiêu làm DPS của Striker tăng gấp 4).
  - `ExecuteHit`: nếu `splashRadius > 0` thì đánh mọi địch có ô cách ô mục tiêu ≤ `splashRadius` (Chebyshev), mục tiêu chỉ trúng một lần.
  - Sửa asset như mục Files.
- [ ] **Step 4: Chạy validator.** Expected: hai dòng PASS.
- [ ] **Step 5: Commit.** Message `feat(combat): melee honours attackType; ranged splash radius`.

### Task 6: Cân lại chỉ số, kiểm "không ai hơn ai", bảng Ý chí

**Files:**
- Modify: `Assets/1.Assets/Resources/Configs/Melee Operator Config.asset`, `Tower Bullet Config.asset`, `Enemy Data Config.asset`
- Test: `Assets/2.Scripts/Editor/TDBalanceValidator.cs`: section `Dominance`
- Test: `Assets/2.Scripts/Editor/TDMoraleValidator.cs`: `k_ResolveTable` (400-409), thông điệp ở 480-481

**Interfaces:**
- Consumes (Task 5): `OperatorData.splashRadius`.

- [ ] **Step 1: Viết assertion `Dominance`.**
  - Nạp config operator và config tower qua `AssetDatabase`, giống `LoadOperatorConfig`.
  - So từng cặp **trong cùng nhóm**: melee với melee, xạ thủ với xạ thủ, turret với turret.
  - Trục so sánh:
    - `blockCount`
    - DPS đơn mục tiêu `damage × attackSpeed`
    - `hp`
    - `cost` (thấp hơn là hơn)
    - số ô tầm (`rangeOffsets.Length`, tối thiểu 1)
    - **số mục tiêu tối đa mỗi đòn**:
      - Operator: `Multiple` thì bằng `blockCount`, có `splashRadius > 0` thì 9, còn lại 1.
      - Turret: `Single` là 1, `Multiple` là `maxTargets`, `AOE` là 99.
    - Phải là con số chứ không phải có/không. Nếu chỉ là có/không thì Catapult (2 mục tiêu, giá 10) sẽ "hơn" MissileG02 (3 mục tiêu, giá 12).
  - Turret bỏ trục block và HP.
  - Lỗi `[DOMINATED] {A} ≥ {B} on every axis` khi A ≥ B ở mọi trục và hơn hẳn ở ít nhất một trục.
- [ ] **Step 2: Chạy. Xác nhận fail trên data hiện tại.** Expected tối thiểu: `[DOMINATED] Ace ≥ Striker` và `[DOMINATED] MissileG03 ≥ MissileG02`.
- [ ] **Step 3: Sửa asset theo spec §5.5–5.6.**

| Operator | cost | hp | damage | attackSpeed | blockCount | khác |
|---|--:|--:|--:|--:|--:|---|
| Defender | 20 | 3000 | 30 | 1.0 | 3 | |
| Tart | 20 | 5000 | 25 | 0.8 | 2 | |
| Knight | 18 | 1200 | 40 | 2.0 | 2 | |
| Striker | 18 | 700 | 100 | 1.5 | 1 | |
| Ace | 20 | 900 | 25 | 1.5 | 3 | attackType Multiple |
| Layla | 10 | 900 | 45 | 1.5 | 1 | |
| Ginger | 18 | 600 | 120 | 0.8 | 0 | |
| Moon | 20 | 700 | 40 | 1.0 | 0 | splashRadius 1 |

| Turret (type) | cost | damage | attackSpeed |
|---|--:|--:|--:|
| Cannon (0) | 5 | 10 | 3 |
| Catapult (1) | 10 | 30 | 1 |
| MissileG02 (2) | **12** | 20 | 1 |
| MissileG03 (3) | 15 | 30 | 1 |
| Mortar (4) | 20 | 30 | 2 |

  Địch: Fast `baseAttackDamage` 5 → **2**, Tank 25 → **50**. `rangeOffsets` và `maxTargets` giữ nguyên.
- [ ] **Step 4: Bảng Ý chí.** Thay `k_ResolveTable` bằng giá trị suy ra từ công thức §07 với chỉ số mới:

```csharp
("Tart", 40.7f), ("Defender", 38.0f), ("Ace", 34.8f), ("Knight", 28.5f),
("Striker", 24.7f), ("Layla", 23.2f), ("Moon", 18.4f), ("Ginger", 13.4f),
```
  Sửa thông điệp `RESOLVE_ORTHOGONAL` thành "Moon (20 gold) out-resolves Knight (18 gold)". Comment trên bảng ghi ngày đổi và nguồn: spec 2026-10-06 §5.6.
- [ ] **Step 5: Chạy validator.** Expected: hai dòng PASS, không còn dòng `DOMINATED`.
- [ ] **Step 6: Commit.** Message `balance: roles for every operator and turret; nobody dominates within a group`.

---

## Mốc C — Level × độ khó

### Task 7: Cấu trúc level × độ khó, độ tăng wave

**Files:**
- Modify: `Assets/2.Scripts/Model/Config/TDLevelConfigSettings.cs`: `LevelConfig`, `RatioRow`, bảng, `Distribute`, thêm `Apportion`
- Modify: `Assets/2.Scripts/Control/PathControl/TDEnemyPathMainControl.cs`: `BuildWavePlans` (~511-610). Xoá `GetBossParams` và luật ép Nightmare 15 / 75
- Modify: `Assets/2.Scripts/View/GamePlay/EnemyPath/TDEnemyPathMainView.cs`: vàng đầu + trần (~156)
- Modify: `Assets/2.Scripts/Control/Tower/TDDeployCap.cs`: `LimitFor` cộng delta
- Modify: `Assets/2.Scripts/Model/Config/Constant/TDConstant.cs`: comment `CONFIG_PLAYER_STARTING_GOLD` ghi "sàn mà generator kiểm"
- Modify: `Assets/1.Assets/Resources/Configs/Level Config.asset`: `waveGrowth: 2.7` cho cả hai level
- Test: `Assets/2.Scripts/Editor/TDBalanceValidator.cs`

**Interfaces:**
- Produces:
  - `LevelConfig`: `public int deployLimit; public float waveGrowth;`.
  - `RatioRow { float normalPct, fastPct, tankPct, hordePct, heraldPct, hpMult, speedMult; int bossWaveCount, bossPerWave; float bossWaveMult; int deployLimitDelta, startingGold; }`. Bỏ `bossPct`.
  - `public static int[] DifficultyRatioTable.Apportion(float[] weights, int total)`: thuật toán Hamilton hiện có, tổng quát cho N trọng số.
  - `public static int[] Distribute(Difficulty d, int total)`: trả `[Normal, Fast, Tank, Horde, Herald]`, bằng `Apportion(pcts, total)`.
  - `public static List<List<EnemyType>> TDEnemyPathMainControl.BuildWavePlans(Difficulty d, int waveCount, int totalEnemies, float waveGrowth)`. Hàm instance `BuildWavePlans(LevelConfig)` gọi tới nó.

Bảng độ khó cho task này. Bầy đàn và Kẻ gieo sợ chưa tồn tại, nên tỉ lệ Normal / Fast / Tank của spec được chuẩn hoá lại trên ba loại. Task 12 và Task 13 sẽ đặt giá trị cuối.

| | normal / fast / tank | horde / herald | hpMult | speedMult | bossWaveCount × bossPerWave, bossWaveMult | deployLimitDelta | startingGold |
|---|---|---|--:|--:|---|--:|--:|
| Normal | 0.647 / 0.235 / 0.118 | 0 / 0 | 1.0 | 1.0 | 1 × 1, 2.0 | +1 | 40 |
| Hard | 0.519 / 0.286 / 0.195 | 0 / 0 | 1.2 | 1.1 | 2 × 1, 2.5 | 0 | 30 |
| Nightmare | 0.373 / 0.299 / 0.328 | 0 / 0 | 1.5 | 1.25 | 3 × 1, 2.5 | −1 | 30 |

- [ ] **Step 1: Sửa và viết assertion trong `TDBalanceValidator`.**
  - `pcts` thành 5 loại. `REMAINDER_INDEX = 0` (Normal).
  - `NO_BOSS` thay bằng `BOSS_PRESENT`: kế hoạch wave có đúng `bossWaveCount × bossPerWave` boss khi `waveCount ≥ bossWaveCount`.
  - Thêm khối dưới đây, chạy với mọi độ khó, `waveCount` 1..10, `total` từ `waveCount` tới 200, `g ∈ {1, 2.7}`:

```csharp
var plan = TDEnemyPathMainControl.BuildWavePlans(d, waves, total, g);
int sum = plan.Sum(w => w.Count);
if (sum != total) f.Add($"[GROWTH_SUM] {d} w={waves} t={total} g={g}: {sum}");
if (plan.Any(w => w.Count < 1)) f.Add($"[GROWTH_MIN1] {d} w={waves} t={total} g={g}");
// boss waves keep room for at least one escort
foreach (var w in plan.Where(w => w.Contains(EnemyType.Boss)))
    if (w.Count < row.bossPerWave + 1) f.Add($"[GROWTH_BOSS_ROOM] {d} w={waves} t={total}");
// regular waves never shrink when g >= 1
var regular = plan.Where(w => !w.Contains(EnemyType.Boss)).Select(w => w.Count).ToList();
for (int i = 1; i < regular.Count; i++)
    if (regular[i] < regular[i - 1] - 1) f.Add($"[GROWTH_MONOTONE] {d} w={waves} t={total} g={g}");
```
  Cho phép lệch 1 vì làm tròn. Thêm `STARTING_GOLD_FLOOR`: mọi `row.startingGold ≥ CONFIG_PLAYER_STARTING_GOLD`.
- [ ] **Step 2: Chạy. Xác nhận fail.** Expected: lỗi compile `BuildWavePlans(d, …)` 4 tham số, `hordePct`, `startingGold`.
- [ ] **Step 3: Cài.**
  - **Trọng số wave `i` (0-based):** `1 + (g − 1) × i / (waveCount − 1)`. Nếu `waveCount = 1` thì trọng số là 1.
  - Wave boss nhân thêm `bossWaveMult`. Wave boss vẫn tính theo công thức cũ `ceil(waveCount × k / bossWaveCount) − 1`.
  - **Cỡ wave** `= Apportion(weights, total)`. Sau đó nâng mọi wave dưới mức tối thiểu (1, hoặc `bossPerWave + 1` với wave boss) bằng cách lấy từ wave lớn nhất. Lặp cho tới khi đủ.
  - **Thành phần mỗi wave:** `Distribute(d, size − bosses)`, xáo Fisher–Yates, rồi append boss. Chỉ dùng 3 phần tử đầu cho tới Task 12. Task 9 thêm tiến độ trận vào `Distribute`.
  - `TDEnemyPathMainView`: đọc `row = DifficultyRatioTable.Get(config.difficulty)`, gọi `TDGoldControl.api?.Initialize(row.startingGold)` **trước** `TDDeployCap.api?.Initialize(...)`.
  - `LimitFor` trả `Mathf.Max(1, level.deployLimit + Get(level.difficulty).deployLimitDelta)`.
  - Log `BuildWavePlans` in danh sách cỡ từng wave.
- [ ] **Step 4: Chạy validator.** Expected: hai dòng PASS.
- [ ] **Step 5: Kiểm trong Play Mode.**
  - DEMO-1 Hard: log cỡ wave tăng dần, HUD tổng địch bằng tổng log, vàng 30, trần `0/5`.
  - Bằng script, tạm đặt `TDLevelConfigSettings.api.GetLevel(0).difficulty = Difficulty.Normal`, `RetryGameplay()`: vàng 40, trần `0/6`.
  - Đặt lại `Hard`. Sau khi thoát Play Mode, `git diff "Assets/1.Assets/Resources/Configs/Level Config.asset"` chỉ được còn `deployLimit` và `waveGrowth`.
- [ ] **Step 6: Commit.** Message `balance: difficulty owns load, level owns rhythm and capacity; waves grow`.

### Task 8: Bảng ρ trong validator

**Files:**
- Create: `Assets/2.Scripts/Editor/TDLoadModel.cs`
- Test: `Assets/2.Scripts/Editor/TDBalanceValidator.cs`: section `LoadFactor`, menu `Tools/TD/Print Load Table`

**Interfaces:**
- Consumes (Task 7): `BuildWavePlans(Difficulty, int, int, float)`, `RatioRow`, `TDDeployCap.LimitFor`.
- Produces:

```csharp
public static class TDLoadModel
{
    public static readonly string[] ReferenceTeam = { "Knight", "Ginger", "Striker", "Defender", "Moon", "Ace" };
    public struct WaveLoad { public int wave; public bool isBoss; public int onField; public float hp; public int bodies; public float seconds; public float rho; }
    // NaN = not calibrated yet → target assertions are skipped. Task 11 fills these in.
    public static float EfficiencyOf(Difficulty d);
    public static List<WaveLoad> Compute(LevelConfig level, Difficulty d,
        IReadOnlyList<OperatorData> roster, IReadOnlyList<EnemyData> enemies);
}
```

**Luật tính:**
- `onField(w) = min(max(1, level.deployLimit + Get(d).deployLimitDelta), w + 1)`, với `w` đánh từ 1. Dùng `d` truyền vào, **không** dùng `level.difficulty`, để in được bảng của cả 3 độ khó cho cùng một level.
- Đội = `onField` tên đầu của `ReferenceTeam`. `DPS = Σ damage × attackSpeed`, `B = Σ blockCount`.
- `hp` là tổng `baseHP × hpMult` của wave.
- `seconds` là tổng thời gian bộ sinh wave sẽ chờ.
- `H = hp / bodies`.
- `rho = hp / (DPS × seconds + B × H)`. Nếu đã hiệu chỉnh thì chia thêm cho `EfficiencyOf(d)`.

- [ ] **Step 1: Viết assertion.** Assertion cấu trúc, luôn chạy:
  - `RHO_FINITE`: không có NaN hay Infinity khi `EfficiencyOf` chưa hiệu chỉnh.
  - `RHO_TEAM_RESOLVES`: mọi tên trong `ReferenceTeam` có trong config.

  Assertion mục tiêu, **chỉ chạy khi `EfficiencyOf(d)` không phải NaN**, dùng bảng mục tiêu §5.7:

```csharp
// targets: (early, atCap, lateRegular, peak)
static readonly Dictionary<Difficulty, (float, float, float, float)> k_RhoTargets = new()
{
    { Difficulty.Normal,    (0.50f, 0.60f, 0.75f, 1.00f) },
    { Difficulty.Hard,      (0.60f, 0.75f, 0.90f, 1.20f) },
    { Difficulty.Nightmare, (0.70f, 0.85f, 1.00f, 1.40f) },
};
```
  | Tag | Luật |
  |---|---|
  | `RHO_EARLY` | ρ lớn nhất trong các wave có `onField < Limit` ≤ mục tiêu + 0,1 |
  | `RHO_AT_CAP` | Wave thường đầu tiên có `onField == Limit` nằm trong ±0,1 |
  | `RHO_LATE` | Wave thường cuối nằm trong ±0,1 |
  | `RHO_PEAK` | ρ lớn nhất trong các wave boss nằm trong ±0,15 |
- [ ] **Step 2: Chạy. Xác nhận fail.** Expected: `CS0103 'TDLoadModel'`.
- [ ] **Step 3: Cài `TDLoadModel` và menu in bảng.** Menu in mọi level × độ khó, mỗi dòng một wave.
- [ ] **Step 4: Chạy validator rồi chạy menu in bảng.** Expected: hai dòng PASS, vì assertion mục tiêu đang bị bỏ qua.
  - DEMO-1 Hard phải cho ρ tăng ở các wave thường cuối trận. So với bảng "đường cong hiện tại" ở spec §5.7: đường cong không còn đi ngang 0,33 sau khi chạm trần.
    **Đã chạy: không đạt** (0,35 / 0,35 / 0,35). Cỡ tăng mà khoảng cách sinh giữ nguyên thì HP/giây không đổi. Spec thêm D14, Task 9 sửa.
  - Chép bảng in ra vào file kết quả của task.
- [ ] **Step 5: Commit.** Message `tools: load-factor model and table for every level and difficulty`.

### Task 9: Mật độ và thành phần theo tiến độ trận (D14)

**Files:**
- Modify: `Assets/2.Scripts/Model/Config/TDLevelConfigSettings.cs`: `RatioRow.mixRamp`, bảng, thêm `Shares`, `Distribute` nhận tiến độ
- Modify: `Assets/2.Scripts/Control/PathControl/TDEnemyPathMainControl.cs`: thêm `GrowthWeight`, `SpawnIntervalFor`. `BuildWavePlans` chia thành phần theo tiến độ. `StartWaveLoop` truyền khoảng cách sinh theo wave
- Modify: `Assets/2.Scripts/Model/Config/Constant/TDConstant.cs`: `CONFIG_SPAWN_INTERVAL_FLOOR = 0.8f`
- Modify: `Assets/2.Scripts/Editor/TDLoadModel.cs`: `seconds` dùng khoảng cách sinh của wave
- Modify: `Assets/2.Scripts/Control/Tower/TDOperatorRegistry.cs`: comment `// ponytail:` trên `LeakAmplifierAt` (dòng ~236) bỏ "Task 12", thay bằng "the Herald task", vì số task đã dời
- Test: `Assets/2.Scripts/Editor/TDBalanceValidator.cs`

**Interfaces:**
- Consumes (Task 7, 8): `Apportion`, `BuildWavePlans`, `TDLoadModel.Compute`, `DifficultyRatioTable.Get`.
- Produces:
  - `RatioRow.mixRamp` (`float`): Normal **1.0**, Hard **0.8**, Nightmare **0.4**.
  - `public static float[] DifficultyRatioTable.Shares(Difficulty d, float progress)`: trả `[Normal, Fast, Tank, Horde, Herald]`.
    - `progress` kẹp vào 0..1.
    - Loại nặng (Tank, Horde, Herald): `pct × (1 − r + 2r × progress)`, với `r = mixRamp`.
    - Fast: `fastPct`. Normal: `1 −` tổng bốn loại kia.
  - `public static int[] DifficultyRatioTable.Distribute(Difficulty d, int total, float progress)` = `Apportion(Shares(d, progress), total)`. **Thay** bản hai tham số.
  - `public static float TDEnemyPathMainControl.GrowthWeight(int waveIdx, int waveCount, float waveGrowth)`: `1 + (g − 1) × i / (n − 1)`; `n ≤ 1` trả 1. `BuildWavePlans` dùng hàm này thay công thức viết tại chỗ.
  - `public static float TDEnemyPathMainControl.SpawnIntervalFor(float spawnInterval, int waveIdx, int waveCount, float waveGrowth)`:
    - `w = GrowthWeight(...)`. `w ≤ 0` thì trả `spawnInterval`.
    - Ngược lại trả `Mathf.Min(spawnInterval, Mathf.Max(CONFIG_SPAWN_INTERVAL_FLOOR, spawnInterval / w))`.

- [ ] **Step 1: Viết `RHO_RISES` trước**, một mình. Nó chỉ dùng `TDLoadModel` nên compile được với code hiện tại. Luôn chạy, không cần hiệu chỉnh, vì là tỉ số:

```csharp
// spec §5.7 / D14: load keeps rising after the team reaches the cap
int limit = Mathf.Max(1, level.deployLimit + DifficultyRatioTable.Get(d).deployLimitDelta);
var atCap = rows.FirstOrDefault(r => !r.isBoss && r.onField == limit);
var last = rows.LastOrDefault(r => !r.isBoss);
if (atCap.wave > 0 && last.wave > atCap.wave && last.rho < 1.1f * atCap.rho)
    f.Add($"[RHO_RISES] {at}: last regular wave {last.rho:F2} < 1.1 × first at cap {atCap.rho:F2}");
```
  Đặt trong vòng level × độ khó của `LoadFactor`, ngay sau `RHO_FINITE`.
- [ ] **Step 2: Chạy. Xác nhận fail đúng hành vi.** Expected: có `[RHO_RISES] level 0 Hard` (0,35 so với 0,35), `level 0 Normal`, `level 1 Normal`. Không lỗi compile.
- [ ] **Step 3: Viết các assertion còn lại.**
  - Vòng `RATIO` / `SUM` / `NEGATIVE` / `DRIFT` chạy thêm với `progress ∈ {0, 0.5, 1}`. Ideal của `DRIFT` = `Shares(d, t)[i] × total`.
  - `MIX_FEASIBLE`: mọi độ khó, `t ∈ {0, 1}`, mọi share ≥ 0 và tổng = 1 ± 0,001.
  - `MIX_AVERAGE`: `Shares(d, 0.5)` bằng bảng tỉ lệ ± 0,001.
  - `MIX_RAMPS`: tổng share nặng ở `t = 1` ≥ ở `t = 0`, và lớn hơn hẳn khi `mixRamp > 0`.
  - Với `s ∈ {0.5, 1.5, 2}`, `n` 1..10, `g ∈ {0.5, 1, 2.7}`:
    - `DENSITY_FIRST`: `SpawnIntervalFor(s, 0, n, g) == s`.
    - `DENSITY_NEVER_SLOWER`: mọi wave ≤ `s`.
    - `DENSITY_FLOOR`: mọi wave ≥ `min(s, 0.8)`.
    - `DENSITY_MONOTONE`: `g ≥ 1` thì không tăng theo wave.
- [ ] **Step 4: Chạy. Xác nhận fail.** Expected: lỗi compile `Shares`, `SpawnIntervalFor`, `mixRamp`, `Distribute` 3 tham số.
- [ ] **Step 5: Cài.**
  - `mixRamp`, `Shares`, `Distribute(d, total, progress)`.
  - `BuildWavePlans`: wave `i` dùng `Distribute(d, size − bosses, n == 1 ? 0 : i / (n − 1))`. Trọng số lấy từ `GrowthWeight`.
  - `StartWaveLoop`: truyền `SpawnIntervalFor(config.spawnInterval, waveIdx, wavePlans.Count, config.waveGrowth)` vào `SpawnBatch` thay cho `config.spawnInterval`.
  - Log `BuildWavePlans(LevelConfig)` in thêm khoảng cách sinh từng wave.
  - `TDLoadModel`: `seconds = bodies × SpawnIntervalFor(level.spawnInterval, i, n, level.waveGrowth)`. Cập nhật comment về T.
  - Comment `TDOperatorRegistry` như mục Files.
- [ ] **Step 6: Chạy validator rồi `Print Load Table`.** Expected: hai dòng PASS. DEMO-1 Hard gần bảng dự kiến ở spec §5.7 (± 0,02): `0,54  0,36  0,41  B0,86  0,49  0,54  0,63  B1,30`. Chép bảng vào file kết quả.
- [ ] **Step 7: Kiểm trong Play Mode**, DEMO-1 Hard:
  - Log `BuildWavePlans` in khoảng cách giảm dần, từ `2.00` ở wave 1 tới `0.80` ở wave 8.
  - Ghi thời điểm `OnEnemySpawned` theo **vị trí cổng**. Khoảng giữa hai con liên tiếp ở cùng cổng trong wave 1 và wave 7 khớp khoảng cách đã log, ± 0,1s.
  - Wave 1 không có Tank.
- [ ] **Step 8: Commit.** Message `balance: load rises by density and mix, not by wave size (D14)`.

---

## Đo lượt 1

### Task 10: `TDPressureProbe` thành máy ghi

**Files:**
- Modify: `Assets/2.Scripts/Control/Tower/TDPressureProbe.cs`
- Modify: `Assets/2.Scripts/View/GamePlay/Tower/TDOperatorView.cs`: theo dõi lượt đứng, báo suy sụp
- Modify: `Assets/2.Scripts/Control/Tower/TDOperatorRegistry.cs`: `ReportLeak` báo cho máy ghi
- Modify: `Assets/2.Scripts/Control/Gameplay/TDGoldControl.cs`: thêm `TotalEarned`
- Test: `Assets/2.Scripts/Editor/TDMoraleValidator.cs`: section `Pearson`

**Interfaces:**
- Produces (static trên `TDPressureProbe`; `AuraRateAt` giữ nguyên):
  - `void RecordLeak()`: +1 lần lọt cho wave hiện tại.
  - `void RecordCollapse()`
  - `void RecordStint(string op, float seconds, float hpLost, float stressGained)`:
    - Gọi khi một lượt đứng kết thúc: rút, chết, hoặc hết trận.
    - `stressGained` là tổng các lần tăng **dương** giữa hai frame, không phải chênh lệch ròng.
  - `string LastReport { get; }`
  - `float Pearson(IReadOnlyList<float> xs, IReadOnlyList<float> ys)`: trả NaN khi n < 2 hoặc một trong hai dãy có phương sai 0.
  - Mỗi lần `TDGameEventBus.WaveStarted`, chốt một dòng của wave trước: `wave, onField, limit, TDGoldControl.TotalEarned, leaks, collapses`.
  - `LastReport` gồm bảng theo wave, số lượt đứng, `r` = Pearson(HP mất/giây, stress tăng/giây) trên các lượt đứng ≥ 5 giây, và tổng số lần suy sụp.
  - Report được dựng **lúc đọc** lần đầu sau Victory hoặc GameOver, không dựng trong handler. Lý do: thứ tự các handler Victory là không xác định, và view phải kịp ghi những lượt đứng còn dở.
  - Dữ liệu reset khi wave 1 bắt đầu.
- `public int TDGoldControl.TotalEarned { get; private set; }`: cộng trong `AddGold`.

- [ ] **Step 1: Viết assertion:**

```csharp
Near(f, "PEARSON_PERFECT", TDPressureProbe.Pearson(new[] { 1f, 2f, 3f }, new[] { 2f, 4f, 6f }), 1f, 0.001f);
Near(f, "PEARSON_INVERSE", TDPressureProbe.Pearson(new[] { 1f, 2f, 3f }, new[] { 3f, 2f, 1f }), -1f, 0.001f);
if (!float.IsNaN(TDPressureProbe.Pearson(new[] { 1f }, new[] { 1f }))) f.Add("[PEARSON_N1] n<2 gave a number");
if (!float.IsNaN(TDPressureProbe.Pearson(new[] { 1f, 2f }, new[] { 5f, 5f }))) f.Add("[PEARSON_FLAT] zero variance gave a number");
```
- [ ] **Step 2: Chạy. Xác nhận fail.** Expected: `CS0117 'Pearson'`.
- [ ] **Step 3: Cài máy ghi, rồi nối:**
  - View ghi lượt đứng: thời điểm bắt đầu, HP lúc đầu, stress tăng dương mỗi frame. Gọi `RecordStint` trong `OnRemove`, và cho mọi người còn đứng khi trận kết thúc.
  - `ConsumeBreak` gọi `RecordCollapse`.
  - `ReportLeak` gọi `RecordLeak`.
- [ ] **Step 4: Chạy validator.** Expected: hai dòng PASS.
- [ ] **Step 5: Kiểm trong Play Mode.** Chơi DEMO-1 tới Victory hoặc GameOver. Đọc `TDPressureProbe.LastReport` qua `script-execute`: có đủ bảng wave, có `r`, có số lần suy sụp.
- [ ] **Step 6: Commit.** Message `tools: the pressure probe now records leaks, stints and gold per wave`.

### Task 11: Bot đo và hiệu chỉnh lượt 1

**Files:**
- Create: `Assets/2.Scripts/Editor/TDCalibrationBot.cs`
- Modify: `Assets/2.Scripts/Control/PathControl/TDEnemyPathMainControl.cs`: property chỉ-đọc `Groups`
- Modify: `Assets/2.Scripts/View/GamePlay/Enemy/TDEnemyView.cs`, `Assets/2.Scripts/Control/Tower/TDPressureProbe.cs`: đếm con địch đã lọt (mỗi con một lần), cột `leaked` trong `LastReport`
- Modify: `Assets/2.Scripts/Editor/TDLoadModel.cs`: T chia theo số cổng ra cùng lúc (`GatesFor`, đọc `TDStageConfig`)
- Modify: `Assets/2.Scripts/Editor/TDLoadModel.cs`: giá trị `EfficiencyOf`
- Modify: `Assets/1.Assets/Resources/Configs/Level Config.asset`: `spawnInterval`, `waveGrowth` (`totalEnemies` nếu cần đổi độ dài trận). `Enemy Data Config.asset` chỉ sửa nếu rơi vào luật D11
- Modify (chỉ khi bước 3 cần): `Assets/2.Scripts/Model/Config/TDLevelConfigSettings.cs`: `mixRamp`, `bossWaveMult` trong bảng độ khó
- Create: `docs/superpowers/measurements/2026-10-06-load-model-round-1.md`

**Interfaces:**
- Consumes: `TDLoadModel`, `TDPressureProbe.LastReport`, `TDDeployCap`.
- Produces:

```csharp
public static class TDCalibrationBot
{
    [MenuItem("Tools/TD/Calibration Bot/Start Full Team")] public static void StartFull();   // Start(int.MaxValue)
    [MenuItem("Tools/TD/Calibration Bot/Start Three")]     public static void StartThree();  // Start(3)
    [MenuItem("Tools/TD/Calibration Bot/Stop")]            public static void Stop();
    public static void Start(int maxUnits);
}
```

**Hành vi bot:**
- Chạy trên `EditorApplication.update` khi đang Play Mode, mỗi 0,5 giây đồng hồ thật.
- Đặt người tiếp theo trong `TDLoadModel.ReferenceTeam` khi cả ba điều kiện đúng: người đó chưa có trên sân, `Gold ≥ cost`, và trần còn suất. Số người do bot đặt không vượt `maxUnits`.
- Tự dựng `TDTowerSlotInfo` từ config operator, nên không phụ thuộc deploy bar ngẫu nhiên.
- Trả tiền bằng `SpendGold`, rồi gọi `CreateOperatorBehavior(zone).Place(...)`.
- **Không bao giờ rút quân.**

**Chọn ô:**
- **Ô neo melee:** ô đường hợp lệ có nhiều hành lang đi qua nhất. Hoà thì chọn ô gần đích hơn. Lấy hành lang qua `TDEnemyPathMainControl`. Nếu cần, thêm một property chỉ-đọc `Groups`.
- **Các melee sau:** ô hợp lệ kế tiếp trên hành lang đó, đi về phía đích.
- **Xạ thủ:** ô tower zone hợp lệ, cùng một trong 4 hướng xoay, phủ được nhiều ô melee nhất.

- [ ] **Step 1: Viết bot. Compile. Chạy validator.** Expected: hai dòng PASS.
- [ ] **Step 2: Đo hiệu suất.** Với mỗi độ khó (tạm đặt `difficulty` của level 0 bằng script như Task 7, bước 5), chạy `StartFull` 3 trận DEMO-1. Mỗi trận:
  - Tìm **wave thường đầu tiên có ≥ 2 con địch lọt** (cột `leaked` của `LastReport`, mỗi con tính một lần).
    Không dùng cột `leaks`: bot xếp melee thành hàng, một con qua cả hàng sinh nhiều sự kiện lọt.
  - `e_trận` = ρ mô hình **chưa hiệu chỉnh** của wave đó, lấy từ `Print Load Table`.
  - Không wave nào đạt 2 lần lọt: ghi `e > ρ lớn nhất`, giảm `spawnInterval` của level 0 đi 20%, chạy lại.
  - `e` = trung bình 3 trận. Ghi từng trận vào log.
- [ ] **Step 3: Đặt `EfficiencyOf` = `e` đã đo.** Chạy validator. Các dòng `RHO_*` sẽ chỉ ra giai đoạn nào lệch.
  - Chỉnh `spawnInterval` của level khi cả đường cong cao hoặc thấp đều. `totalEnemies` gần như không đổi ρ (T tỉ lệ với số con), chỉ đổi độ dài trận.
  - Chỉnh `waveGrowth` khi đầu và cuối trận lệch ngược chiều nhau. Từ Task 9 núm này đổi cả mật độ, nên kéo được cuối trận.
  - Chỉnh `mixRamp` của độ khó khi riêng `RHO_EARLY` lệch.
  - Chỉnh `bossWaveMult` của độ khó khi riêng `RHO_PEAK` lệch. Mô phỏng ở spec §5.7 cho thấy đỉnh boss đang khoảng 2 lần wave thường cuối, mục tiêu khoảng 1,33.
  - Lặp cho tới khi `RHO_*` pass ở **cả 2 level**, cả 3 độ khó. `RHO_RISES` (Task 9) phải vẫn pass sau mọi lần chỉnh.
  - **Kết quả lượt 1 (user chốt):** `EfficiencyOf` = `e` đo được ÷ `BotStrength` 0,58. Bot giữ một hàng cố định, không rút quân, nên được coi là yếu hơn người chơi mà mục tiêu mô tả. `RHO_EARLY` rời validator, chuyển thành phép đo bằng bot ở Step 4 (spec §5.7, §7.2). Mật độ giữ nguyên. Data đổi: `bossWaveMult` 1,2 / 1,3 / 1,15, `mixRamp` 1,0 / 0,65 / 0,3, và DEMO-1 `totalEnemies` 56 → 50 để wave 3 Hard thôi để lọt (Step 4).
- [ ] **Step 4: Ba phép đo còn lại ở Hard**, với data mới:

  | Phép đo | Cách làm | Mục tiêu | Nếu trượt |
  |---|---|---|---|
  | Đầu trận | Các trận `StartFull` | Ở các wave mô hình coi là chưa đủ trần (`onField < Limit`), mỗi wave lọt < 2 con (cột `leaked`) | Giảm tải đầu trận: `mixRamp` hoặc `waveGrowth` |
  | Morale có xuất hiện không | 1 trận `StartFull` | `LastReport` có ≥ 1 lần suy sụp | Tăng tải hoặc giảm trần |
  | Tương quan HP ↔ stress | 3 trận `StartFull`, `ResetPool` trước trận đầu | `r melee pooled` < 0,5 (chỉ melee, gộp trận; spec §7.2) | Quay lại spec §5.5, báo user, dừng |
  | Thời điểm chạm trần | 1 trận `StartFull` và 1 trận `StartThree`. Từ `TotalEarned` theo wave, tìm wave đầu tiên mà `startingGold + TotalEarned ≥` tổng giá `Limit` người đầu của `ReferenceTeam` | Hai trận lệch ≤ 1 wave | Nhân mọi `goldReward` với 0,75 rồi đo lại (luật D11) |
- [ ] **Step 5: Ghi log đo**: số từng trận, `e` theo độ khó, các chỉnh sửa data kèm lý do. Đặt lại `difficulty` của level 0 về Hard. Chạy validator. Expected: hai dòng PASS, gồm cả `RHO_*` (trừ `RHO_EARLY`, đã chuyển sang Step 4).
- [ ] **Step 6: Commit** bot, `TDLoadModel.cs`, các asset đã chỉnh và log. Message `balance: calibrate the load model against bot runs (round 1)`.

---

## Mốc D — Hai loại địch mới

### Task 12: Bầy đàn

**Files:**
- Modify: `Assets/2.Scripts/Model/Config/Enum/Enums.cs`: `EnemyType.Horde = 4`
- Modify: `Assets/1.Assets/Resources/Configs/Enemy Data Config.asset`: dòng mới `type 4, baseHP 70, baseSpeed 4, dieDuration 0.25, goldReward 1, baseAttackDamage 2, baseAttackSpeed 1, prefab Horde`
- Create: `Horde.prefab`, là prefab variant của prefab Fast, đặt cạnh prefab Fast
  - Bỏ `Animator`, bỏ child HP bar, scale 0,6.
  - Material bật `enableInstancing`.
- Modify: `Assets/2.Scripts/Model/Config/TDLevelConfigSettings.cs`: `hordePct` và tỉ lệ (bảng dưới)
- Modify: `Assets/2.Scripts/Control/PathControl/TDEnemyPathMainControl.cs`: nở bầy, nhịp ra bầy, cắt wave khi nhiều cổng ra cùng lúc
- Modify: `Assets/2.Scripts/Model/Config/Constant/TDConstant.cs`: `HORDE_PACK_SIZE = 5`, `HORDE_PACK_SPAWN_INTERVAL = 0.2f`
- Modify: `Assets/2.Scripts/Editor/TDLoadModel.cs`: thời gian và số con của bầy
- Test: `Assets/2.Scripts/Editor/TDBalanceValidator.cs`

Tỉ lệ cho task này: phần của Kẻ gieo sợ tạm gộp vào Normal cho tới Task 13.

| | normal / fast / tank / horde / herald |
|---|---|
| Normal | 0.60 / 0.20 / 0.10 / 0.10 / 0 |
| Hard | 0.48 / 0.22 / 0.15 / 0.15 / 0 |
| Nightmare | 0.38 / 0.20 / 0.22 / 0.20 / 0 |

**Interfaces:**
- Produces: `public static List<List<EnemyType>> TDEnemyPathMainControl.SliceWave(List<EnemyType> wave, int groups)`: cắt wave cho các cổng ra cùng lúc. Ranh giới lát cắt được đẩy tới hết lượt Horde liền kề, nên không bao giờ cắt ngang một bầy.

- [ ] **Step 1: Viết assertion**, mọi độ khó, `total` 10..200:
  - `HORDE_PACKS_INTACT`: mọi đoạn Horde liên tiếp có độ dài là bội số của `HORDE_PACK_SIZE`.
  - `HORDE_BOSS_LAST`: boss vẫn đứng cuối wave boss.
  - `HORDE_TOTAL_EXPANDED`: tổng phần tử của kế hoạch = suất không phải Horde + `HORDE_PACK_SIZE` × suất Horde + số boss.
  - `HORDE_SLICE_INTACT`: với `groups` 1..3, không lát cắt nào bắt đầu giữa một bầy.
  - `RATIO` vẫn tổng bằng 1.

  Lưu ý: `GROWTH_SUM` ở Task 7 đếm **suất**, nên phải sửa để so tổng suất, không so tổng phần tử.
- [ ] **Step 2: Chạy. Xác nhận fail.** Expected: `CS0117 'Horde'`.
- [ ] **Step 3: Cài.**
  - Nở mỗi suất Horde thành 5 phần tử liền nhau **sau** Fisher–Yates, **trước** khi append boss.
  - `GetActualEnemyCount` đếm sau khi nở.
  - `SpawnBatch`: giữa hai Horde trong cùng một bầy (bộ đếm Horde liên tiếp `% HORDE_PACK_SIZE != HORDE_PACK_SIZE − 1`) chờ `HORDE_PACK_SPAWN_INTERVAL`. Các trường hợp khác chờ khoảng cách sinh của wave đó (`SpawnIntervalFor`, Task 9).
  - `StartWaveLoop` dùng `SliceWave` thay cho phép cắt theo `perGroup`.
  - `TDLoadModel`: một suất Horde tính 5 con, thời gian `4 × 0,2 + s_w`, với `s_w` là khoảng cách sinh của wave đó.
  - Horde là loại **nặng** trong `Shares` (Task 9): tỉ lệ tăng dần theo tiến độ trận. `MIX_FEASIBLE` phải vẫn pass với bảng dưới.
- [ ] **Step 4: Chạy validator.** Expected: hai dòng PASS. `RHO_*` có thể fail vì tải đã đổi. Nếu fail, ghi lại, và Task 14 sẽ hiệu chỉnh. Riêng bước này chấp nhận `RHO_*` fail.
- [ ] **Step 5: Kiểm trong Play Mode.** DEMO-1 Hard với `StartFull`, chơi tới Victory:
  - Mỗi bầy 5 con ra trong khoảng 1 giây.
  - HUD `killed/total` khớp nhau, và Victory có bắn.
  - Bầy không có HP bar, không lỗi Animator trong console.
- [ ] **Step 6: Commit.** Message `feat(enemy): Horde — packs of five that overflow a buffer`.

### Task 13: Kẻ gieo sợ

**Files:**
- Modify: `Enums.cs`: `EnemyType.Herald = 5`
- Modify: `Enemy Data Config.asset`: dòng mới `type 5, baseHP 500, baseSpeed 2, dieDuration 1, goldReward 12, baseAttackDamage 0, baseAttackSpeed 0, prefab Herald`
- Create: `Herald.prefab`, là variant của prefab Normal
  - Material tint `#7B3FA0`.
  - Child `FearRing`: đĩa phẳng bán kính `HERALD_RADIUS × cellSize`, unlit, alpha 0,25, nằm trên mặt đất.
- Modify: `TDLevelConfigSettings.cs`: tỉ lệ cuối của spec

  | | normal / fast / tank / horde / herald |
  |---|---|
  | Normal | 0.55 / 0.20 / 0.10 / 0.10 / 0.05 |
  | Hard | 0.40 / 0.22 / 0.15 / 0.15 / 0.08 |
  | Nightmare | 0.25 / 0.20 / 0.22 / 0.20 / 0.13 |

- Modify: `TDEnemyPathMainControl.cs`: thêm phần tử Herald vào wave
- Modify: `TDOperatorRegistry.cs`: `LeakAmplifierAt` thật
- Test: `TDMoraleValidator.cs`: section `Amplifier`

**Interfaces:**
- Produces: `public static float TDOperatorRegistry.LeakAmplifier(Vector2Int leakCell, IReadOnlyList<Vector2Int> heraldCells)`:
  - Trả `HERALD_LEAK_MULT` nếu **bất kỳ** ô Herald nào cách ô lọt ≤ `HERALD_RADIUS` (Euclid, tính theo ô).
  - Ngược lại trả 1. Không cộng dồn.
  - `LeakAmplifierAt(cell)` lấy ô của mọi Herald còn sống từ `TDEnemyRegistry` rồi gọi hàm này.

- [ ] **Step 1: Viết assertion:**

```csharp
var o = new Vector2Int(0, 0);
Near(f, "AMP_NONE",     TDOperatorRegistry.LeakAmplifier(o, new Vector2Int[0]), 1f);
Near(f, "AMP_INSIDE",   TDOperatorRegistry.LeakAmplifier(o, new[] { new Vector2Int(4, 0) }), TDConstant.HERALD_LEAK_MULT);
Near(f, "AMP_OUTSIDE",  TDOperatorRegistry.LeakAmplifier(o, new[] { new Vector2Int(4, 1) }), 1f); // √17 > 4
Near(f, "AMP_NO_STACK", TDOperatorRegistry.LeakAmplifier(o, new[] { new Vector2Int(1, 0), new Vector2Int(0, 1) }), TDConstant.HERALD_LEAK_MULT);
```
- [ ] **Step 2: Chạy. Xác nhận fail.** Expected: `CS0117 'LeakAmplifier'`.
- [ ] **Step 3: Cài.** Thêm Herald vào `Distribute` và kế hoạch wave (một phần tử mỗi suất). Herald là loại **nặng** trong `Shares` (Task 9). Đổi comment `// ponytail:` trên `LeakAmplifierAt` thành mô tả hàm thật. `TDPressureProbe.AuraRateAt` để nguyên: Herald **không** có aura.
- [ ] **Step 4: Chạy validator.** Expected: hai dòng PASS, trừ `RHO_*` như Task 12, bước 4.
- [ ] **Step 5: Kiểm trong Play Mode.**
  - Một Herald bị Knight chặn: HP Knight không giảm vì Herald không đánh.
  - Các lần lọt trong vòng 4 ô log ra gấp đôi điểm.
  - Vòng `FearRing` thấy được trong `screenshot-game-view`.
- [ ] **Step 6: Commit.** Message `feat(enemy): Herald — doubles every leak within four cells`.

### Task 14: Đo lượt 2, ca bắt buộc, hiệu năng

**Files:**
- Modify: `Assets/2.Scripts/Editor/TDCalibrationBot.cs`: thêm `SpawnForTest`
- Modify: `Assets/2.Scripts/Control/PathControl/TDEnemyPathMainControl.cs`: thêm `#if UNITY_EDITOR public void SpawnForTest(EnemyType type, int count, float interval)`. Dùng pool sẵn có và hành lang đầu tiên.
- Modify: `TDLoadModel.cs`, `Level Config.asset` (hiệu chỉnh)
- Create: `docs/superpowers/measurements/2026-10-06-load-model-round-2.md`

- [ ] **Step 1: Hiệu chỉnh lại.** Lặp lại Task 11, bước 2–5, với thành phần đầy đủ. Expected: hai dòng PASS gồm `RHO_*`, morale xuất hiện ở Hard, `r < 0,5`.
- [ ] **Step 2: Ca ác mộng.**
  - Một Knight Calm đứng một mình chặn đủ 2 Normal (sinh bằng `SpawnForTest`).
  - Sinh 1 Herald, rồi một bầy (`SpawnForTest(Horde, 5, 0.2f)`) trên cùng hành lang.
  - Đo tổng stress Knight nhận trong lúc bầy đi qua. Expected: **≤ 60**, và Knight không suy sụp.
  - Nếu vượt: giảm `STRESS_PER_LEAK` trước. Còn vượt thì giảm `HERALD_LEAK_MULT`. Ghi lý do vào log.
- [ ] **Step 3: Hiệu năng trong Editor.**
  - Nightmare với `StartFull`. Ở wave đông nhất, dùng `profiler-capture-frame` ba lần và đếm `TDEnemyRegistry.api.GetAll().Count`.
  - Ghi FPS và số địch vào log.
  - Đo trên máy thật là việc của user, ghi trong log là "chưa làm".
- [ ] **Step 4: Ghi log lượt 2.** Commit. Message `balance: round-2 calibration with Horde and Herald; nightmare case and perf notes`.

---

### Task 15: Đồng bộ tài liệu

**Files:**
- Modify: `MORALE_SYSTEM_DESIGN.md`: §03, §04, §10, mục lục
- Modify + **add** (đang untracked): `docs/superpowers/plans/2026-10-05-morale-system-update.md`
- Modify: `docs/superpowers/specs/2026-10-06-morale-load-model-design.md`: dòng trạng thái

- [ ] **Step 1:** Đầu §03, §04 và khối hằng số §10, thêm khung:

  > ⚠️ Đã thay thế bởi spec 2026-10-06 (mô hình tải), xem §5.2–5.4.

  Trong mục lục, đánh dấu ba mục đó.
- [ ] **Step 2:** Plan M1 cũ: đánh dấu Task 1–3 xong (kèm commit), Task 4–6 "thay bởi plan 2026-10-06". Task 7 trỏ sang task này.
- [ ] **Step 3:** Spec: thêm dòng trạng thái "Đã triển khai mốc A–D", kèm hash commit cuối.
- [ ] **Step 4: Commit** ba file. Message `docs: mark the pressure-zone model superseded; close plan M1`.

---

**Sau Task 15:** cổng người chơi ở spec §7.3 do user tự thực hiện, gồm "không vô hình, không bất khả kháng" và bài test A4. Plan này dừng ở đó.
