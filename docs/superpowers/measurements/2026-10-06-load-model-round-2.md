# Hiệu chỉnh mô hình tải — lượt 2

Plan `docs/superpowers/plans/2026-10-06-morale-load-model.md`, Task 14. Spec §5.2, §5.7, §7.2. Lượt 1:
`2026-10-06-load-model-round-1.md`.
Log thô và ledger (rulings, minor còn nợ): `raw/2026-10-06-load-model/`.

## Cách đo

Giống lượt 1: map DEMO-1, bot `TDCalibrationBot` (đội chuẩn, không xoay ca), tốc độ 2×. Khác lượt 1:

- Đủ năm loại địch thường. Bầy (Horde) và Kẻ gieo sợ (Herald) đã vào game (Task 12, 13).
- Máy ghi có thêm cột `horde`: số **thân bầy** trong số con lọt của mỗi wave.

## Lần đo đầu — đếm cả thân bầy

`e` = ρ chưa hiệu chỉnh của wave thường đầu tiên có ≥ 2 con lọt (`t14-runs.log`).

| Độ khó | Wave đầu ≥ 2 con lọt | ρ thô | `e` | Kết quả |
|---|---|---|--:|---|
| Normal | 6 / 5 / 5 | 0,70 / 0,65 / 0,65 | 0,67 | 1 Victory |
| Hard | 3 / 5 / 5 | 0,50 / 0,95 / 0,95 | 0,80 | 3 GameOver |
| Nightmare | 2 / 2 / 2 | 1,19 ×3 | 1,19 | 3 GameOver trước wave 5 |

Mọi trận Nightmare đều có wave 2 lọt đúng 5 con, tức bầy đầu tiên. `e` đang đo **lúc bầy đầu tiên tới**, chưa đo
sức gánh. Nếu chỉnh theo đó thì Nightmare phải **tăng** tải, trong khi bot đã thua trước wave 5.

**Quyết định:** con số hiệu chỉnh không tính thân bầy. Spec giao cho Bầy vai "tràn túi theo bầy". Đó là một cú dồn
mà ρ trung bình theo thời gian không thấy được, và ca bắt buộc (bên dưới) đo riêng nó.

## Lần đo lại — không tính thân bầy

`t14-runs2.log`.

| Độ khó | Wave đầu ≥ 2 con không phải bầy lọt | ρ thô | `e` | Kết quả |
|---|---|---|--:|---|
| Normal | 5 / 5 / 7 | 0,65 / 0,65 / 0,78 | 0,69 | 1 Victory |
| Hard | 5 / 5 / 5 | 0,95 ×3 | 0,95 | 3 GameOver ở wave 6–7 |
| Nightmare | 4 / 4 / 4 | 1,03 ×3 | 1,03 | 3 GameOver trước wave 5 |

Lượt 1 đo được 0,51 / 0,58 / 0,59.

## Xung đột và quyết định

Tìm trên các núm độ khó (`mixRamp`, `bossWaveMult`, `hpMult` tới ×1,6) với `e` mới:

- Hard cần `hpMult` 1,2 → 1,4–1,75 mới đạt mục tiêu ρ.
- Nightmare không đạt được ở bất kỳ tổ hợp nào.

Tức là chỉnh theo ρ sẽ cho ra một game nặng hơn nhiều, trong khi bot đã thua Hard 3/3 (lượt 1 thắng 2/3). Thứ làm
game khó hơn là bầy tràn túi và hệ số Herald, và ρ không thấy cả hai. Chỉnh theo ρ vì vậy kéo sai chiều.

User chọn **hiệu chỉnh theo kết quả bot**:

- `EfficiencyOf` giữ `e` của lượt 1.
- Tải thật giảm tới khi validator pass. Chỉ núm độ khó thì không làm cả hai level đạt cùng lúc, nên phải đổi cả núm
  level:

| Núm | Normal | Hard | Nightmare |
|---|---|---|---|
| `hpMult` | 1,0 → **0,85** | 1,2 → **0,9** | 1,5 → **1,05** |
| `mixRamp` | 1,0 | 0,65 → **0,3** | 0,3 → **0,0** |
| `bossWaveMult` | 1,2 → **1,15** | 1,3 → **1,0** | 1,15 |

Level: DEMO-1 `totalEnemies` 50 → **46**. DEMO-2 `waveGrowth` 2,7 → **3,5**.

## ρ sau hiệu chỉnh

| Level · độ khó | ρ theo wave (B = boss) |
|---|---|
| DEMO-1 Normal | 0,64 0,59 0,46 0,56 0,68 0,75 0,78 B1,11 |
| DEMO-1 Hard | 0,58 0,69 0,67 B0,65 0,83 0,84 0,96 B1,28 |
| DEMO-1 Nightmare | 0,85 0,95 B1,02 0,80 0,92 B1,25 1,09 B1,44 |
| DEMO-2 Normal | 0,68 0,52 0,50 0,69 0,56 0,67 0,69 B0,99 |
| DEMO-2 Hard | 0,83 0,68 0,60 B0,92 0,73 0,75 0,81 B1,07 |
| DEMO-2 Nightmare | 1,07 0,80 B1,01 0,83 0,88 B1,24 1,01 B1,30 |

`TDMoraleValidator` và `TDBalanceValidator` đều PASS, gồm mọi kiểm `RHO_*`.

## Ca bắt buộc: một bầy qua Knight đứng một mình cạnh Herald

Cách dựng:

- Ở Hard, hủy vòng sinh wave. Bot đặt một Knight ở ô hành lang 15 (cách cổng 30 đơn vị), stress 0.
- `TDEnemyPathMainControl.SpawnForTest` sinh 1 Herald, rồi một bầy `(Horde, 5, 0,2 s)` trên cùng hành lang.
- Thời điểm sinh được tính để Herald đứng trong 4 ô khi bầy tới.
- Khi bầy tới, Knight đang chặn 1 con, nên 4 thân lọt qua.

| Lần | `STRESS_PER_LEAK` | `HERALD_LEAK_MULT` | Stress Knight nhận | Kết quả |
|---|--:|--:|---|---|
| 1 (`t14-nightmare.log`) | 10 | 2 | +20, +20, +30, +30 → **100** | **Gãy**. Trượt mục tiêu ≤ 60 |
| 2 (`t14-nightmare2.log`) | **7** | **1,5** | +10,5 ×4 → **42** | Đứng vững (Steady). **Đạt** |

Brief bảo giảm `STRESS_PER_LEAK` trước. Làm riêng núm đó thì phải xuống 5, tức nửa cả nền kinh tế lọt: 15 con lọt
mới gãy một melee đứng một mình. User chọn **7 và Herald ×1,5**:

- Morale vẫn cắn. Số con lọt tới gãy đổi từ 8 / 11 / 25 thành **11 / 15 / 35** (melee một mình / melee có yểm trợ /
  xạ thủ).
- Herald vẫn có nghĩa.

Validator có thêm kiểm `NIGHTMARE_PACK`: cả 5 thân lọt, từ stress 0 cộng **57,75**, không gãy.

**Sửa sau review cuối.** Ca từ stress 0 là điểm *dễ* nhất của band Calm (0–33). Với 7 và ×1,5, một melee bắt đầu
từ ≥ 23 stress vẫn gãy vì một bầy được khuếch đại (từ 33 lên 117). User chọn: **Kẻ gieo sợ không khuếch đại thân
Bầy**, vì Bầy vốn đã là mối đe doạ tràn túi. `NIGHTMARE_PACK` giờ chạy cả từ 0 lẫn từ 33: từ 33 bầy cộng 52,5
(→ 85,5) và không gãy. Có thêm kiểm `AMP_HORDE_EXEMPT`. Số con lọt tới gãy vẫn là 11 / 15 / 35.

## Bot ở Hard trên data cuối

`t14-hard2.log`, `STRESS_PER_LEAK` 7, Herald ×1,5. Trần cần 94 vàng, tức kiếm thêm 64 sau 30 vàng khởi đầu.

| Trận | Bot | Kết quả | Con không phải bầy lọt ở wave 1–3 | Suy sụp | r melee | Wave đầu `goldEarned` ≥ 64 |
|--:|---|---|---|--:|--:|--:|
| 1 | StartFull | GameOver ở wave 8 (boss cuối) | 0 / 0 / 1 | 5 | 0,39 | 4 |
| 2 | StartFull | GameOver ở wave 8 | 0 / 0 / 0 | 5 | 0,22 | 5 |
| 3 | StartFull | GameOver ở wave 8 | 0 / 0 / 0 | 5 | 0,59 | 4 |
| 4 | StartThree | GameOver ở wave 7 | 0 / 0 / 1 | 2 | n/a (2 lần) | 4 |

Trước khi đổi stress (`t14-hard.log`, `STRESS_PER_LEAK` 10, Herald ×2), bot thua ở wave 7–8. Trận 1 khi đó để
lọt 3 con không phải bầy ở wave 3.

| Phép đo | Mục tiêu | Kết quả |
|---|---|---|
| Đầu trận: mỗi wave lọt < 2 con trước trần | < 2 | **Đạt**: nhiều nhất 1 |
| Morale có xuất hiện không | ≥ 1 lần suy sụp | **Đạt**: 5 mỗi trận `StartFull` |
| Tương quan HP ↔ stress, melee, gộp 3 trận `StartFull` | r < 0,5 | **Đạt**: 0,30 (11 lần đứng chốt) |
| Chạm trần: StartFull so với StartThree | Lệch ≤ 1 wave | **Đạt**: wave 4–5 so với 4 |
| Bot thắng Hard khoảng 2/3 | 2/3 | **Trượt**: 0/3. User chấp nhận, xem bên dưới |

**Vì sao thua:**

1. Mỗi người nhận khoảng 115–150 stress cả trận và bot không xoay ca, nên cả năm người cùng gãy ở wave 6–7.
2. Sau đó các bầy lọt nguyên đàn, chiếm khoảng một nửa số con lọt.
3. Mỗi thân bầy về căn cứ lấy 1 trong 20 mạng.

**Đã thử: giảm tỉ lệ bầy/Herald ở Hard trong bộ nhớ** (`t14-hard3.log`). Normal / Fast / Tank / Bầy / Herald đổi từ
40 / 22 / 15 / 15 / 8 thành 45 / 22 / 15 / 12 / 6.

- Bot vẫn thua 0/3 ở wave 8.
- r melee lên 0,65.
- DEMO-2 Hard trượt validator (`RHO_LATE` 0,74, `RHO_PEAK` 1,02).

Không dùng. Không cách giảm tải thật nào vừa giữ validator pass vừa đạt 2/3.

**Quyết định của user: chấp nhận, để playtest quyết.**

- Bot tới wave boss cuối ở cả 6/6 trận `StartFull` trên hai bộ data. Điều này khớp mục tiêu người chơi của spec:
  "giữ được tới các wave boss".
- Bot thua vì không xoay ca, đúng thứ morale sinh ra để phạt.
- Tỉ lệ thắng thật do người chơi Hard quyết (spec §7.3).

## Hiệu năng (Editor)

DEMO-1 Nightmare, `StartFull`, tốc độ 1×, Game view trong Editor (`t14-perf.log`, `t14-perf2.log`).

`profiler-capture-frame` không có trong MCP đang dùng. Thay vào đó:

- Đo thời gian từng frame ngay trong script.
- Ba lần chụp là ba cửa sổ 1 s có nhiều địch sống nhất.
- Lượt thứ hai tách thời gian bằng `UnityEngine.Profiling.Recorder`.

| Địch sống | Số frame | FPS | Frame | PlayerLoop | Script Update | Physics | GC |
|---|--:|--:|--:|--:|--:|--:|--:|
| 0–3 | 849 | 32 | 31,1 ms | 10,0 ms | 0,3 ms | 0,6 ms | 0 |
| 4–6 | 488 | 30 | 33,4 ms | 11,3 ms | 0,3 ms | 0,5 ms | 0 |
| 7–9 | 638 | 28 | 35,4 ms | 11,9 ms | 0,3 ms | 0,6 ms | 0 |
| 10+ | 751 | 33 | 30,7 ms | 10,9 ms | 0,3 ms | 0,5 ms | 0 |

- Đông nhất bot gặp là **12 địch sống**, ở wave 5–6. Ba lần chụp đều 12 con, 15–21 fps (lượt đầu).
- Bot thua ở wave 6, nên wave 7–8 (đông hơn) **chưa đo**.
- **Số địch không làm frame chậm đi.** Frame giữ khoảng 31 ms từ 0–3 tới 10+ con. Script Update giữ 0,3 ms, không có
  GC. Phần lớn thời gian frame là chi phí Editor, nằm ngoài PlayerLoop.
- Lượt đầu có 19 fps lúc 12 con, nhưng không lặp lại ở lượt sau.
- Mono heap của Editor 1,1 GB, 239 assembly. Đó là các script MCP biên dịch dồn lại, sẽ mất khi domain reload.
  Không phải bộ nhớ của game.
- **Đo trên máy thật: chưa làm** (việc của user).

## Còn mở

- Hard: bot 0/3. Người chơi Hard (spec §7.3) quyết tỉ lệ thắng thật. Nếu người chơi cũng thua, còn ba núm:
  - số mạng một bầy lấy khi lọt;
  - `STRESS_PER_LEAK`;
  - bot biết xoay ca, để đo lại cho đúng.
- `e` vẫn là của lượt 1. Mô hình ρ không thấy bầy tràn túi hay hệ số Herald. Phần đó được trả bằng tải mà mô hình
  thấy, và được kiểm bằng kết quả bot.
- Hiệu năng wave 7–8 Nightmare và trên máy thật chưa đo.
