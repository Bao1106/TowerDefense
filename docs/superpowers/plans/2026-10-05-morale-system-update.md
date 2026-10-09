# Morale System — Update M1 Implementation Plan

> **Trạng thái (2026-10-08): đóng.** Task 1–3 xong. Task 4–6 thay bởi plan 2026-10-06 (mô hình tải). Task 7 chuyển sang Task 15 của plan đó.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Sửa những chỗ đang làm hệ morale không đánh giá được (bug cascade, Retreat im lặng), thêm hai thứ quyết định độ căng của hệ (giới hạn melee trên sân, slow-mo khi chọn lính), và gắn phép đo trả lời được câu hỏi sống còn: *"stress có phải thanh máu thứ hai không"*.

**Architecture:** Không thêm hệ thống mới. Sự kiện "gãy" chuyển vào model (`TDOperatorMorale`) để mọi nguồn đẩy qua 100 đều kích hoạt nó. Giới hạn deploy nằm ở `TDOperatorRoster.CanDeploy`, chỗ duy nhất mà cả card lẫn cổng đặt lính cùng đọc. Slow-mo gom về một hàm tính `timeScale`. Phép đo mở rộng `TDPressureProbe` đã có.

**Tech Stack:** Unity 2022.3.55f1, C# (Assembly-CSharp), Editor validator (`Tools ▸ TD ▸ …`), Unity MCP (`script-execute`, `assets-refresh`, `console-get-logs`) để compile/chạy/đọc log.

**Spec:** [`MORALE_SYSTEM_DESIGN.md`](../../../MORALE_SYSTEM_DESIGN.md) cộng review idea/design ngày 2026-10-05. Tóm tắt các phát hiện mà plan này trả lời:
- Gãy do N3 (`AddSpike`) bỏ qua bước chuyển trong `TDOperatorView.TickMorale`, nên dây chuyền dừng ở mắt xích thứ hai.
- Bấm Retreat trên lính suy sụp thì không có gì xảy ra và không có phản hồi.
- Không có giới hạn số người trên sân. Đủ vàng thì dồn ≥3 melee vào nút thắt, N1 về 0, hồi nhờ đồng đội Calm đạt trần, và morale biến mất giữa trận.
- Bài kiểm tra A4 (Chốt B) không phân biệt được stress với "một thanh máu cạn nhanh hơn".
- Thao tác thời gian thực trên mobile không có slow-mo.
- **Quyết tử (3.5) hoãn** theo quyết định của user. Ý chí giữ nguyên trong code nhưng không đầu tư thêm.

## Global Constraints

- Không thêm `Update()` mới, không thêm singleton `.api` mới, không thêm prefab, không thêm event vào `TDGameEventBus` (ràng buộc §15 2.4 và §06).
- Mọi lệnh ghi giá trị stress đi qua `TDOperatorMorale.SetValue`.
- Test = Editor validator. Không có NUnit (§15 0.3). Pass nghĩa là `[TDMoraleValidator] PASS — 0 failures.` và `[TDBalanceValidator] PASS`.
- Sau mỗi lần sửa code: MCP `assets-refresh` rồi `console-get-logs`. Console phải không có lỗi compile.
- Comment tiếng Anh, giải thích *vì sao*, cùng giọng với code xung quanh. Nhãn UI tiếng Anh viết hoa, giống `"RESCUE"`.
- Commit trên `feature/morale-system`, mỗi task một commit, chỉ `git add` đúng đường dẫn đã sửa. **Không bao giờ** stage `Packages/`, `ProjectSettings/PackageManagerSettings.asset`, `Assets/Plugins/NuGet*`, `.mcp.json`, `.claude/`, `*.bak`, `TDEffectManager.cs`, `DOTweenSettings.asset`.
- Mọi commit message kết thúc bằng `-m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"`.

## Review Focus

1. **Huỷ chọn mục tiêu Rescue để lại selection cũ.** Hôm nay `ResolveRescueTap` chỉ gọi `EndRescuePick()`, nên `m_SelectedOperator` vẫn còn trong khi diamond đã ẩn. Bấm lại đúng người đó thì `SelectOperator` return sớm. Có slow-mo rồi thì game sẽ kẹt ở 0,25×. Kiểm ở Task 3, bước 6.
2. **Victory/GameOver khi đang chọn lính** phải giữ `timeScale = 0`, dù `Deselect` chạy trước hay sau `Pause()`. Kiểm ở Task 3, bước 6.
3. **Cờ "gãy" chưa được xử lý mà lính bị Rescue hoặc chết** thì không được bắn ra khi deploy lại. Kiểm bằng `EDGE_CLEARED_BY_RESCUE` và `EDGE_CLEARED_BY_DEATH` ở Task 1.
4. **Lính chết trong lúc diễn clip Die** phải trả chỗ trong giới hạn ngay lập tức, không đợi xác biến mất. Kiểm bằng `CAP_FREES_ON_DEATH` ở Task 4.
5. **Tương quan bị thổi phồng vì lượt đứng dài ngắn khác nhau** (lượt càng dài thì cả HP mất lẫn stress đều tăng). Dùng tốc độ theo giây, có guard n<2 và phương sai 0. Kiểm bằng `PEARSON_*` ở Task 5.

---

### Task 1: Gãy là một sự kiện của model, bất kể nguồn nào đẩy qua 100

> ✅ **Xong**: commit `ce2ac65a`.

**Files:**
- Modify: `Assets/2.Scripts/Model/Info/Morale/TDOperatorMorale.cs` (fields ~46-48, `OnDeath` 209-232, `SetValue` 250-256)
- Modify: `Assets/2.Scripts/View/GamePlay/Tower/TDOperatorView.cs` (`TickMorale` 123-139)
- Test: `Assets/2.Scripts/Editor/TDMoraleValidator.cs` (`Run()` 38-52, khối `SETBACK_ONE_PER_INCIDENT` 231-239, section mới)

**Interfaces:**
- Produces: `public bool ConsumeBreak()` trên `TDOperatorMorale`. Trả `true` đúng một lần cho mỗi lần vào trạng thái gãy rồi tự xoá cờ. `Setbacks` tự tăng ngay lúc vào gãy, view không gọi `OnSetback()` nữa.

- [x] **Step 1: Viết assertion.** Thêm `BreakEdge(f);` vào `Run()` sau `BrokenState(f);`, rồi thêm section:

```csharp
// ── J — a break is an EVENT, whoever pushed the value over ──────────────
private static void BreakEdge(List<string> f)
{
    var spiked = At(80f);
    spiked.AddSpike(TDConstant.STRESS_ALLY_BREAK_SPIKE);
    if (!spiked.ConsumeBreak()) f.Add("[EDGE_FROM_SPIKE] a spike-induced break raised no edge");
    if (spiked.ConsumeBreak()) f.Add("[EDGE_ONCE] the same break was reported twice");
    if (spiked.Setbacks != 1) f.Add($"[EDGE_SETBACK] spike break counted {spiked.Setbacks}, want 1");

    spiked.AddSpike(TDConstant.STRESS_ALLY_BREAK_SPIKE); // already latched
    if (spiked.ConsumeBreak()) f.Add("[EDGE_WHILE_LATCHED] a spike on a broken operator raised a second edge");

    var ticked = new TDOperatorMorale();
    for (int i = 0; i < 2000 && !ticked.IsBroken; i++) ticked.Tick(0.05f, Ctx(1, 2));
    if (!ticked.ConsumeBreak()) f.Add("[EDGE_FROM_TICK] a tick-induced break raised no edge");

    var rescued = At(TDConstant.STRESS_MAX);
    rescued.OnRescue();
    if (rescued.ConsumeBreak()) f.Add("[EDGE_CLEARED_BY_RESCUE] an unconsumed edge survived the rescue");

    var died = At(TDConstant.STRESS_MAX);
    died.OnDeath();
    if (died.ConsumeBreak()) f.Add("[EDGE_CLEARED_BY_DEATH] an unconsumed edge would fire on redeploy");

    var again = At(TDConstant.STRESS_MAX);
    again.ConsumeBreak();
    again.OnRescue();
    again.AddSpike(TDConstant.STRESS_MAX);
    if (!again.ConsumeBreak() || again.Setbacks != 2)
        f.Add($"[EDGE_REBREAK] second break after rescue: setbacks {again.Setbacks}, want 2");
}
```

Trong khối `SETBACK_ONE_PER_INCIDENT`, xoá dòng `chain.OnSetback();` cùng comment của nó. Giờ chính cú gãy là setback thứ nhất, nên `chain.Setbacks` vẫn phải bằng 1.

- [x] **Step 2: Chạy, xác nhận fail.** `assets-refresh` → `console-get-logs`. Expected: lỗi `CS1061 … 'ConsumeBreak'`.

- [x] **Step 3: Cài đặt trong `TDOperatorMorale`.** Thêm field `private bool m_BreakPending;` và `public bool ConsumeBreak()` (trả giá trị cờ rồi xoá). Đây là phần tinh tế, nên `SetValue` viết như sau:

```csharp
m_Value = Mathf.Clamp(raw, 0f, TDConstant.STRESS_MAX);

if (!m_Broken && m_Value >= TDConstant.STRESS_MAX)
{
    m_Broken = true;
    m_BreakPending = true;
    Setbacks++;
}
else if (m_Value <= TDConstant.STRESS_STEADY_MAX)
{
    m_Broken = false;
    m_BreakPending = false; // rescued before anyone handled the break: nothing left to announce
}
```

Trong `OnDeath`, cạnh `m_Broken = false;` thêm `m_BreakPending = false;`. Comment: cờ không được sống sót qua cái chết rồi bắn ra ở lần deploy sau. Giữ nguyên `if (!m_Broken) OnSetback();`. Sửa doc-comment của `Setbacks` cho đúng: cú gãy được đếm ở `SetValue`.

- [x] **Step 4: Đổi `TickMorale`.** Xoá `bool wasBroken` và `Morale.OnSetback()`. Sau `Morale.Tick(...)` và `m_MoraleIcon?.Refresh(...)`:
  - `if (Morale.ConsumeBreak())` thì gọi `BroadcastSpike(m_MyCell, STRESS_ALLY_BREAK_SPIKE, wasBreak: true)` và `ReleaseBlockedEnemies(m_MyCell)`, kèm `Debug.Log($"[Morale] {m_Data?.operatorName} suy sụp tại {m_MyCell}")`.
  - Comment: N3 đưa hàng xóm vào trạng thái gãy *bên ngoài* `Tick`, nên đọc `IsBroken` trước `Tick` sẽ không bao giờ thấy được bước chuyển đó. Mỗi mắt xích dây chuyền giờ chậm một frame, và đó là chủ ý.

- [x] **Step 5: Chạy validator.** `Tools ▸ TD ▸ Validate Stress Model` và `Tools ▸ TD ▸ Validate Balance Tables`. Expected: cả hai PASS, 0 failures.

- [x] **Step 6: Kiểm trong game.** Play Mode, deploy 2 melee đứng cách nhau ≤2 ô. MCP `script-execute`:
  `var o = Object.FindObjectsOfType<TDOperatorView>(); o[1].Morale.AddSpike(75); o[0].Morale.AddSpike(100); return "ok";`
  Đợi 1 giây rồi `console-get-logs`. Expected: **2** dòng `[Morale] … suy sụp`, và cả hai `IsCollapsed == true`. Trước khi sửa chỉ có 1 dòng.

- [x] **Step 7: Commit**

```bash
git add Assets/2.Scripts/Model/Info/Morale/TDOperatorMorale.cs Assets/2.Scripts/View/GamePlay/Tower/TDOperatorView.cs Assets/2.Scripts/Editor/TDMoraleValidator.cs
git commit -m "fix(morale): breaks caused by an ally spike now fire the collapse transition" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Retreat trên lính suy sụp phải nói lý do, không được im lặng

> ✅ **Xong**: commit `2bf3d6e2`.

**Files:**
- Modify: `Assets/2.Scripts/View/GamePlay/Tower/TDDiamondPanelView.cs` (resolve block 152-166, `BuildRescueButton` line 201, method mới cạnh `UpdateRescue`)
- Modify: `Assets/2.Scripts/View/GamePlay/Tower/TDOperatorSelectionView.cs` (`Update` 72-84, `SelectOperator` 132-141, `SelectTower` 316-322)

**Interfaces:**
- Produces: `public void SetRetreatBlocked(string reason)` trên `TDDiamondPanelView`. `null` nghĩa là bình thường. Chuỗi khác `null` thì làm mờ nút (alpha `RESCUE_DIM_ALPHA`), tắt `interactable` và `blocksRaycasts`, và đổi label thành `reason`.

- [x] **Step 1: Sửa fake-null ở line 201.** Thay `go.GetComponent<CanvasGroup>() ?? go.AddComponent<CanvasGroup>()` bằng `go.TryGetComponent(out CanvasGroup g) ? g : go.AddComponent<CanvasGroup>()`.
  - Lý do cho comment: trong Editor, `GetComponent` không tìm thấy component sẽ trả về một object fake-null mà `??` không bắt được.
  - Hệ quả hiện tại: `RetreatButton` trong `DiamondPanel.prefab` không có CanvasGroup, nên `m_RescueGroup` là fake-null và nút Rescue không bao giờ bị làm mờ khi chạy trong Editor.

- [x] **Step 2: Cache trạng thái của nút Retreat.** Trong resolve block, **sau** `BuildRescueButton(retreatT)` (để bản clone không thừa hưởng group này), lấy `m_RetreatGroup` theo cùng pattern `TryGetComponent`, `m_RetreatLabel = GetComponentInChildren<TMP_Text>(true)`, và `m_RetreatLabelDefault = m_RetreatLabel?.text`.

- [x] **Step 3: Cài `SetRetreatBlocked(string reason)`.** Chỉ có tác dụng khi `m_Mode == Mode.Retreat`. Khi `reason == null` thì trả lại alpha 1, `interactable`, và label mặc định.

- [x] **Step 4: Gọi từ `TDOperatorSelectionView`.**
  - Thêm `private const string RETREAT_BLOCKED_LABEL = "BROKEN";`.
  - `SelectOperator`: sau `ShowRetreat`, gọi `SetRetreatBlocked(op.IsCollapsed ? RETREAT_BLOCKED_LABEL : null)`.
  - `SelectTower`: gọi `SetRetreatBlocked(null)`.
  - `Update`: trong khối `if (m_SelectedOperator != null)`, gọi lại mỗi frame, vì lính có thể gãy hoặc được cứu trong lúc panel đang mở.

- [x] **Step 5: Kiểm trong game.** Play Mode:
  - Cho một melee gãy (`AddSpike(100)` qua `script-execute`), rồi bấm vào nó. Expected: nút Retreat mờ, label `BROKEN`; bấm vào thì panel vẫn mở và lính vẫn đứng đó.
  - Cứu bằng `script-execute` (`o[0].Morale.OnRescue()`). Expected: trong 1 frame, label trở lại mặc định và nút sáng.
  - Chọn một lính khác có SP < 50 đứng cạnh người suy sụp. Expected: nút Rescue mờ (alpha 0,6), tức Step 1 đã có tác dụng.

- [x] **Step 6: Commit**

```bash
git add Assets/2.Scripts/View/GamePlay/Tower/TDDiamondPanelView.cs Assets/2.Scripts/View/GamePlay/Tower/TDOperatorSelectionView.cs
git commit -m "feat(morale): retreat on a collapsed operator shows why it is blocked" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Slow-mo khi đang chọn lính, và sửa selection cũ sau khi huỷ Rescue

> ✅ **Xong**: commit `4766ca47`.

**Files:**
- Modify: `Assets/2.Scripts/Model/Config/Constant/TDConstant.cs` (cạnh `SPEED_FAST` line 172)
- Modify: `Assets/2.Scripts/Control/Gameplay/TDSpeedControl.cs`
- Modify: `Assets/2.Scripts/Control/Gameplay/TDPauseControl.cs` (`Resume` 20-27)
- Modify: `Assets/2.Scripts/View/GamePlay/Tower/TDOperatorSelectionView.cs` (`Update`, `Deselect`, `BlinkRescueCandidates` 263-284, `ResolveRescueTap` 286-305)
- Test: `Assets/2.Scripts/Editor/TDMoraleValidator.cs`

**Interfaces:**
- Produces trên `TDSpeedControl`:
  - `public static float ScaleFor(bool paused, float speed, bool focused)`: trả `0` khi paused, nếu không thì `speed × (focused ? SPEED_FOCUS : 1)`.
  - `public bool IsFocused { get; private set; }`
  - `public void SetFocus(bool focused)`: không làm gì nếu giá trị không đổi.
  - `public void Apply()`: `Time.timeScale = ScaleFor(TDPauseControl.api?.IsPaused ?? false, SpeedMultiplier, IsFocused)`.
- Constant: `public const float SPEED_FOCUS = 0.25f;` (núm, chỉnh khi playtest).

- [x] **Step 1: Viết assertion.** Thêm `FocusScale(f);` vào `Run()`, kèm section:

```csharp
// ── H — focus slow-mo: one formula, pause always wins ───────────────────
private static void FocusScale(List<string> f)
{
    Near(f, "FOCUS_NORMAL", TDSpeedControl.ScaleFor(false, TDConstant.SPEED_NORMAL, true), TDConstant.SPEED_FOCUS);
    Near(f, "FOCUS_FAST", TDSpeedControl.ScaleFor(false, TDConstant.SPEED_FAST, true),
         TDConstant.SPEED_FAST * TDConstant.SPEED_FOCUS);
    Near(f, "PAUSE_WINS", TDSpeedControl.ScaleFor(true, TDConstant.SPEED_FAST, true), 0f);
    Near(f, "NO_FOCUS", TDSpeedControl.ScaleFor(false, TDConstant.SPEED_FAST, false), TDConstant.SPEED_FAST);
}
```

- [x] **Step 2: Chạy, xác nhận fail.** Expected: `CS0117 … 'ScaleFor'` và `'SPEED_FOCUS'`.

- [x] **Step 3: Cài đặt `TDSpeedControl`.**
  - `ToggleSpeed` gọi `Apply()` thay cho nhánh `if (!IsPaused)`.
  - `Initialize` đặt `IsFocused = false`.
  - `TDPauseControl.Resume` gọi `TDSpeedControl.api.Apply()`. Nếu `api` null thì đặt `Time.timeScale = 1f`.
  - Từ đây chỉ còn một công thức `timeScale` dùng chung cho pause, x2 và focus.

- [x] **Step 4: Sửa selection cũ.**
  - `ResolveRescueTap`, nhánh huỷ cuối hàm: `EndRescuePick()` → `Deselect()`.
  - `BlinkRescueCandidates`: cả hai chỗ `EndRescuePick()` → `Deselect()`.
  - `Deselect()` đã tự gọi `EndRescuePick()`.
  - Comment: một lần huỷ để lại `m_SelectedOperator` thì lần bấm lại cùng người đó bị `SelectOperator` nuốt mất, và với slow-mo thì game kẹt luôn ở 0,25×.

- [x] **Step 5: Gắn focus.** Dòng đầu `Update()`, **trước** check `IsGameEnded`: `TDSpeedControl.api?.SetFocus(HasSelected);`. Focus được suy ra mỗi frame chứ không latch, nên mọi đường thoát (pause, victory, deploy mới, huỷ) đều tự đúng.

- [x] **Step 6: Chạy validator rồi kiểm trong game.** Validator PASS. Play Mode, đọc `Time.timeScale` qua `script-execute` ở từng bước:

| Thao tác | Expected `timeScale` |
|---|---|
| Chọn một operator | 0,25 |
| Bấm x2 trong lúc đang chọn | 0,5 |
| Bấm ra chỗ trống | 2 |
| Chọn lại, rồi `TDPauseControl.api.Pause()` | 0 |
| `Resume()` | 2 (pause đã deselect) |
| Tạo 2 người suy sụp kề một lính tỉnh, bấm Rescue (vào chế độ chọn), bấm chỗ trống | 2, và bấm lại đúng người cứu thì diamond mở được |
| Đang chọn lính thì gọi `TDGameEventBus` Victory (hoặc thắng thật) | 0 |

- [x] **Step 7: Commit**

```bash
git add Assets/2.Scripts/Model/Config/Constant/TDConstant.cs Assets/2.Scripts/Control/Gameplay/TDSpeedControl.cs Assets/2.Scripts/Control/Gameplay/TDPauseControl.cs Assets/2.Scripts/View/GamePlay/Tower/TDOperatorSelectionView.cs Assets/2.Scripts/Editor/TDMoraleValidator.cs
git commit -m "feat(input): slow time while an operator is selected; fix stale selection after cancelling a rescue pick" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Giới hạn số melee trên sân

> ⏭ **Thay bởi plan 2026-10-06** ([`2026-10-06-morale-load-model.md`](2026-10-06-morale-load-model.md)): trần triển khai theo level, chung cho operator và turret (spec 2026-10-06 §5.1, commit `a234ac92`).

**Files:**
- Modify: `Assets/2.Scripts/Model/Config/Constant/TDConstant.cs` (sau `CONFIG_MAX_SLOTS` line 300)
- Modify: `Assets/2.Scripts/Control/Tower/TDOperatorRoster.cs` (`CanDeploy` 68-69, thêm 2 member)
- Modify: `Assets/2.Scripts/View/GamePlay/Tower/TDSlotHolderItemView.cs`
- Modify: `Assets/2.Scripts/View/GamePlay/Tower/TDSlotHolderMainView.cs` (`RefreshHolderInteractability` 77-81)
- Test: `Assets/2.Scripts/Editor/TDMoraleValidator.cs`

**Interfaces:**
- Constant: `public const int CONFIG_MAX_DEPLOYED_MELEE = 3;`. Comment: đây là núm thứ 5 của hệ morale, vì số người đứng ở nút thắt quyết định N1 và mức hồi nhờ đồng đội Calm. Phải nhỏ hơn `CONFIG_SLOTS_MELEE` (5) để luôn có người chờ.
- Produces trên `TDOperatorRoster`:
  - `public int DeployedMeleeCount` (số phần tử trong `m_Deployed` có `deployZone == DeployZone.PathCell`).
  - `public bool MeleeCapReached => DeployedMeleeCount >= TDConstant.CONFIG_MAX_DEPLOYED_MELEE`.
  - `CanDeploy` thêm điều kiện: là melee **và** đã chạm trần thì `false`. Ranged không bị ảnh hưởng.
- Produces trên `TDSlotHolderItemView`: `public void RefreshCostLabel()`. Khi card melee chưa ra sân mà đã chạm trần thì label hiện `"{DeployedMeleeCount}/{CONFIG_MAX_DEPLOYED_MELEE}"`, ngược lại hiện `"{Cost}$"`.

- [ ] **Step 1: Viết assertion.** Thêm `DeployCap(f);` vào `Run()`:

```csharp
// ── I — the melee cap: count is a design knob, not a function of gold ───
private static void DeployCap(List<string> f)
{
    int cap = TDConstant.CONFIG_MAX_DEPLOYED_MELEE;
    var roster = new TDOperatorRoster();
    var melee = new List<OperatorData>();
    for (int i = 0; i <= cap; i++)
        melee.Add(new OperatorData { operatorName = $"M{i}", operatorType = OperatorType.Knight });
    var ranged = new OperatorData { operatorName = "R", operatorType = OperatorType.Ranger };
    var extra = melee[cap];

    for (int i = 0; i < cap; i++) roster.OnDeployed(melee[i]);

    if (roster.CanDeploy(extra)) f.Add("[CAP_BLOCKS_MELEE] a melee past the cap was deployable");
    if (!roster.CanDeploy(ranged)) f.Add("[CAP_IGNORES_RANGED] the melee cap blocked a ranged operator");

    roster.OnLeftField(melee[0], voluntary: false);
    if (!roster.CanDeploy(extra)) f.Add("[CAP_FREES_ON_DEATH] a death did not free its place under the cap");

    roster.OnDeployed(extra);
    roster.OnLeftField(melee[1], voluntary: true);
    if (roster.DeployedMeleeCount != cap - 1)
        f.Add($"[CAP_COUNT] counted {roster.DeployedMeleeCount}, want {cap - 1}");
}
```

- [ ] **Step 2: Chạy, xác nhận fail.** Expected: `CS0117 … 'CONFIG_MAX_DEPLOYED_MELEE'`.

- [ ] **Step 3: Cài đặt** constant và các member của roster theo Interfaces.

- [ ] **Step 4: Label của card.**
  - Cài `RefreshCostLabel()`. `SetupSlotCost` vẫn là chỗ duy nhất gán `Cost`.
  - `RefreshHolderInteractability` gọi `holder.RefreshCostLabel()` ngay sau `SetInteractable`.
  - Grep `OnAvailabilityChanged` để xác nhận `RefreshAvailability` đã được subscribe. Nếu chưa thì subscribe trong `Start`/`OnDestroy` của main view theo cặp `+=`/`-=`.

- [ ] **Step 5: Chạy validator rồi kiểm trong game.** Validator PASS. Play Mode, đủ vàng:
  - Deploy 3 melee. Expected: các card melee còn lại xám, label `3/3`; card ranged vẫn sáng.
  - Rút 1 người. Expected: các card khác trở lại giá tiền ngay; người vừa rút vẫn khoá 8 giây.

- [ ] **Step 6: Commit**

```bash
git add Assets/2.Scripts/Model/Config/Constant/TDConstant.cs Assets/2.Scripts/Control/Tower/TDOperatorRoster.cs Assets/2.Scripts/View/GamePlay/Tower/TDSlotHolderItemView.cs Assets/2.Scripts/View/GamePlay/Tower/TDSlotHolderMainView.cs Assets/2.Scripts/Editor/TDMoraleValidator.cs
git commit -m "feat(morale): cap melee operators on the field so squad size stays a design knob" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Đo tương quan HP và stress, và đo áp lực theo mật độ

> ⏭ **Thay bởi plan 2026-10-06** ([`2026-10-06-morale-load-model.md`](2026-10-06-morale-load-model.md)): r HP ↔ stress và các phép đo lọt nằm trong `TDPressureProbe` của plan đó (spec §7.2).

**Files:**
- Modify: `Assets/2.Scripts/Control/Tower/TDPressureProbe.cs` (`Sample` 127-145, `Report` 147-168, thêm member)
- Modify: `Assets/2.Scripts/Control/Tower/TDOperatorRegistry.cs` (section Morale 166+)
- Modify: `Assets/2.Scripts/View/GamePlay/Tower/TDOperatorView.cs` (`Init`, `Update` 246, `DoRetreat`, `Die`)
- Test: `Assets/2.Scripts/Editor/TDMoraleValidator.cs`

**Interfaces:**
- `TDOperatorRegistry.CountAlliesAdjacent(Vector2Int cell) : int`: số operator ở 8 ô kề (Chebyshev 1), trạng thái nào cũng tính, không tính chính ô đó.
- `TDOperatorRegistry.Views : IEnumerable<TDOperatorView>`: các view đang sống (`m_OperatorViews.Values`). Chỉ có melee, vì ranged không đăng ký ở đây. Ghi điều này vào comment.
- `TDPressureProbe.Sample(Vector2Int cell, Quaternion rotation, OperatorData data, float netRate, ref float nextSampleTime)`: tự đọc `CountAlliesAdjacent` sau khi qua bộ giới hạn 1 Hz. Gom số liệu theo 4 nhóm `adj 0 | 1 | 2 | 3+`: số mẫu, số mẫu có địch trong vùng, số mẫu vượt ngưỡng, tổng `netRate`.
- `TDPressureProbe.RecordShift(string name, float hpLostFraction, float stressGained, float seconds, string exit)`: `exit` ∈ `"retreat" | "death" | "end"`.
- `TDPressureProbe.Pearson(IReadOnlyList<float> x, IReadOnlyList<float> y) : float`: trả 0 khi n<2 hoặc một trong hai dãy có phương sai 0.
- `TDOperatorView.ReportShift(string exit)`: gửi số liệu lượt đứng hiện tại sang `RecordShift`.
  - HP mất = `(m_MaxHp − max(0, m_CurrentHp)) / m_MaxHp`.
  - Stress tăng = cộng dồn mọi lần tăng của `Morale.Value` giữa các frame (bắt cả spike do người khác gây ra), không trừ phần hồi.
  - Số giây = `Time.time − thời điểm Init`.

- [ ] **Step 1: Viết assertion.** Thêm `Instrumentation(f);` vào `Run()`:

```csharp
// ── K — the measuring tool is a tool only once it has been checked ──────
private static void Instrumentation(List<string> f)
{
    Near(f, "PEARSON_POS", TDPressureProbe.Pearson(new[] { 1f, 2f, 3f }, new[] { 2f, 4f, 6f }), 1f, 0.001f);
    Near(f, "PEARSON_NEG", TDPressureProbe.Pearson(new[] { 1f, 2f, 3f }, new[] { 3f, 2f, 1f }), -1f, 0.001f);
    Near(f, "PEARSON_FLAT", TDPressureProbe.Pearson(new[] { 1f, 1f, 1f }, new[] { 1f, 2f, 3f }), 0f);
    Near(f, "PEARSON_TINY", TDPressureProbe.Pearson(new[] { 1f }, new[] { 1f }), 0f);
}
```

- [ ] **Step 2: Chạy, xác nhận fail.** Expected: `CS0117 … 'Pearson'`.

- [ ] **Step 3: Cài 2 member của registry, `Pearson`, `RecordShift`, và nhóm mật độ trong `Sample`.**

- [ ] **Step 4: Sửa view.**
  - `Init` ghi lại thời điểm và giá trị stress ban đầu.
  - `Update` cộng dồn stress tăng **sau** `TickMorale`, rồi gọi `Sample(..., Morale?.NetRate(MoraleContext) ?? 0f, ref m_NextPressureSample)`.
  - `DoRetreat` (sau check `CanRetreat`) gọi `ReportShift("retreat")`. `Die` (sau check `m_IsDying`) gọi `ReportShift("death")`.

- [ ] **Step 5: `Report` in thêm,** trước khi reset số liệu:
  1. Với mỗi view còn sống trong `TDOperatorRegistry.api?.Views`, gọi `ReportShift("end")`.
  2. Mỗi lượt đứng một dòng: `[Shift] {name} {exit} {seconds:F0}s hp-{hp%:F0}% stress+{gained:F0}`.
  3. Một dòng tương quan: `[Pressure] HP↔stress r={r:F2} over {n} shifts (per-second rates, shifts ≥5s)`. `r` tính trên `hpLostFraction/seconds` và `stressGained/seconds`, chỉ lấy lượt ≥ 5 giây.
  4. Mỗi nhóm mật độ một dòng: `[Density] adj {k}: engaged {e%}, over tolerance {o%}, mean net {rate:+0.00}/s`.
  5. Đếm theo `exit`: số lần retreat và số lần chết.

- [ ] **Step 6: Chạy validator, rồi chơi 1 ván.** Validator PASS. Chơi một ván bất kỳ đến Victory/GameOver. `console-get-logs` phải có đủ các dòng `[Shift]`, `[Pressure] HP↔stress`, và 4 dòng `[Density]`.

- [ ] **Step 7: Commit**

```bash
git add Assets/2.Scripts/Control/Tower/TDPressureProbe.cs Assets/2.Scripts/Control/Tower/TDOperatorRegistry.cs Assets/2.Scripts/View/GamePlay/Tower/TDOperatorView.cs Assets/2.Scripts/Editor/TDMoraleValidator.cs
git commit -m "feat(morale): log HP-vs-stress correlation per shift and pressure by squad density" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Chơi đo và chốt quyết định (gate trước M2)

> ⏭ **Thay bởi plan 2026-10-06** ([`2026-10-06-morale-load-model.md`](2026-10-06-morale-load-model.md)): gate thay bằng hai lượt hiệu chỉnh bằng bot (`docs/superpowers/measurements/2026-10-06-load-model-round-1.md`, `-round-2.md`).

**Files:**
- Modify: `Assets/1.Assets/Resources/Configs/Level Config.asset` (level 0, line 17: `difficulty: 1` → `difficulty: 0`)
- Modify: `MORALE_SYSTEM_DESIGN.md` (thêm `#### Đo sau M1` vào cuối GIAI ĐOẠN 3, §15)

- [ ] **Step 1:** Đặt level 0 về Normal. Không động vào file `.bak`.
- [ ] **Step 2:** Chơi **3 ván Normal** (level 0) và **2 ván Hard** (level 1). Ưu tiên user tự chơi. Nếu agent chơi qua MCP thì ghi rõ là bot trong bảng kết quả.
- [ ] **Step 3:** Mỗi ván ghi một dòng: kết quả và thời gian trận · `r` · 4 dòng `[Density]` · số lần gãy (đếm dòng `[Morale] … suy sụp`) · số retreat và số lần chết · số lần Rescue (đếm dòng `[Rescue] … cứu`).
- [ ] **Step 4: Đối chiếu bảng quyết định.** Các ngưỡng dưới đây là đề xuất; user có quyền sửa trước khi chơi:

| Kết quả | Quyết định |
|---|---|
| `r ≥ 0,7` (trung bình các ván) | Stress đang là thanh máu thứ hai. **M2 Horde-lite bắt buộc trước mọi việc ở GĐ4** |
| `r < 0,4` | Stress đã là trục riêng. M2 có thể làm sau GĐ4 |
| Nhóm `adj 2` và `adj 3+`: vượt ngưỡng ≈ 0% **và** net ≤ 0 | Hạ `CONFIG_MAX_DEPLOYED_MELEE` xuống 2 rồi đo lại. Nếu vẫn ≤ 0 thì **cắt hồi nhờ đồng đội Calm** (luật vô hình), cùng `RESOLVE_ADJ_CALM_ALLY` |
| 0 lần gãy mỗi ván ở Normal | Người mới không bao giờ thấy morale. Màn 1 phải được thiết kế để ép một lần gãy trong ~60 giây đầu |
| ≥ 4 lần gãy liên tiếp (dây chuyền) mỗi ván | Hạ `STRESS_ALLY_BREAK_SPIKE` (núm thứ 4, chỉnh **cuối cùng** theo §10) |

- [ ] **Step 5:** Ghi bảng số liệu và các quyết định đã chọn vào doc.
- [ ] **Step 6: Commit**

```bash
git add "Assets/1.Assets/Resources/Configs/Level Config.asset" MORALE_SYSTEM_DESIGN.md
git commit -m "chore(morale): level 0 back to Normal; record M1 measurements and decisions" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Đồng bộ doc với code và quyết định mới

> ➡️ **Chuyển sang Task 15 của plan 2026-10-06**: `MORALE_SYSTEM_DESIGN.md` §03, §04 và khối hằng số §10 được đánh dấu đã thay thế bởi spec 2026-10-06, thay vì sửa từng dòng.

**Files:**
- Modify: `MORALE_SYSTEM_DESIGN.md`

- [ ] **Step 1: Sửa các chỗ đã lệch.**

| Chỗ | Sửa thành |
|---|---|
| Bảng header | Đếm lại hằng số thật (`TDConstant.cs` 354–472, cộng `CONFIG_MAX_DEPLOYED_MELEE` và `SPEED_FOCUS`). Nguyên mẫu: "2 chạy được trên 21×9 (PHỄU, THÁC), 10 chưa kiểm" |
| §01 | Thêm đoạn **Nguồn gốc**: stress và resolve của Darkest Dungeon; trigger morale của Total War / Battle Brothers; vòng xoay morale ở base RIIC của Arknights. Phần mới là đưa vào TD thời gian thực, với rotation là động từ chống lại stress, và địa hình thiết kế quanh nó. Bỏ câu "thứ chưa TD nào có" |
| §05, dòng "Hết wave" | "Nghỉ giữa wave −15: chạy `waveInterval` sau đợt spawn cuối, không đợi wave sạch" |
| §06 Rescue | Hồi tại chỗ, không "kéo ra"; người cứu không rời vị trí |
| §06 Quyết tử và §15 3.5 | ⏸ **Hoãn (2026-10-05)**. Ý chí giữ trong code, không đầu tư thêm. Nếu làm lại thì thử phương án "gãy = lựa chọn" (hiện 2 nút ở mọi lần gãy, Ý chí quyết định độ mạnh thay vì quyết định có hiện nút hay không) |
| §07 | Cập nhật bảng theo validator: Tart 43 · Defender 40 · Knight 27 · Ace 26 · Striker 26 · Layla 20 · Moon 15 · Ginger 14. Thay câu "phân hoá mà không đụng sức mạnh" bằng sự thật: ba người khác nhau vì tầm đánh (tức chỉ số) đã đổi |
| §9.3 | Ghi chú ở SONG TUYẾN và CHẠC BA: mâu thuẫn với §9.1, chỉ dùng nếu chủ ý làm "vách đá" |
| §10 | `STRESS_PER_OVERLOAD = 1.0f`. Bảng núm thêm núm thứ 5: `CONFIG_MAX_DEPLOYED_MELEE` |
| §12 ① / Chốt B | Ghi chú: A4 không phân biệt được với một thanh máu cạn nhanh hơn; bổ sung chỉ số `r` của Task 5/6 |
| §15 | 3.4 ✅. Thêm các dòng M1 (Task 1–5) và lộ trình mới ở cuối plan này, kèm link tới file plan |

- [ ] **Step 2: Kiểm.** Grep doc: không còn `0.5f` ở §10, không còn `≈ 30 tổ hợp`, không còn `chưa TD nào có`.
- [ ] **Step 3: Commit**

```bash
git add MORALE_SYSTEM_DESIGN.md
git commit -m "docs(morale): sync design doc with code, defer Last Stand, credit design lineage" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

## Lộ trình sau M1 (mỗi mốc sẽ có plan riêng khi tới lượt)

| Mốc | Nội dung | Điều kiện bắt đầu |
|---|---|---|
| **M2 · Horde-lite** | GĐ5 thu gọn: chỉ Horde (enum, `RatioRow`, Hamilton 6 loại, nở pack **sau** Fisher–Yates với nhịp 0,2s, prefab không Animator/HP bar). Herald hoãn | Gate Task 6. Nếu `r ≥ 0,7` thì làm ngay |
| **M3 · Truyền đạt** | 4.2 morale và cooldown trên card · 4.3 âm thanh + nhịp phóng to khi vào Stressed · 4.4 vòng aura dưới đất · 4.5 kiểm silhouette · sprite `icon_rescue_white` · 3.7 nút rút khỏi chiến dịch | Sau M2, hoặc trước nếu `r < 0,4` |
| **Chốt C** | Bài kiểm tra 10 giây với **người chưa từng xem game** | Ngay sau M3, không được bỏ |
| **M4** | 3.6 chết vĩnh viễn + roster hữu hạn (tác động thẳng vào trục số người, nên phải có số đo của cap trước) | Sau Chốt C |
| **Sau đó** | GĐ6 hiệu năng · GĐ7 nội dung và cân bằng · GDD 2–3 trang tách khỏi devlog (trước khi gửi Kong) | |
| **Hoãn** | 3.5 Quyết tử · Herald | Xem lại sau Chốt D |
