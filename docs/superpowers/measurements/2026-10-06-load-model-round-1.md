# Hiệu chỉnh mô hình tải — lượt 1

Plan `docs/superpowers/plans/2026-10-06-morale-load-model.md`, Task 11. Spec §4.2, §5.7, §7.2.

## Cách đo

- Map DEMO-1 (level 0, 2 cổng ra cùng lúc). Bot `TDCalibrationBot`:
  - mua theo thứ tự đội chuẩn Knight, Ginger, Striker, Defender, Moon, Ace mỗi khi đủ vàng;
  - đặt melee thành một hàng ở ô nhiều hành lang đi qua nhất (gần đích), đặt xạ thủ ở ô phủ hàng đó;
  - không xoay ca, không rút quân. Tốc độ 2×.
- `e` của một trận = ρ **chưa hiệu chỉnh** của wave thường đầu tiên có ≥ 2 **con địch** lọt (cột `leaked`).
  `e` của độ khó = trung bình 3 trận.

## Lần đo 1 — bỏ

`e` 0,40 / 0,51 / 0,51 (Normal / Hard / Nightmare). Muốn khớp mục tiêu thì cần `spawnInterval` khoảng 5,75 s,
tức game thưa gấp 3–4 lần. Có hai lỗi đo làm `e` thấp giả:

- Đếm **sự kiện lọt**, mỗi lần một con đi qua một melee đầy. Bot xếp bốn melee một hàng, nên một con bị đếm
  tới bốn lần.
- T tính theo một luồng sinh, trong khi DEMO-1 sinh ở 2 cổng cùng lúc.

Đã sửa: đếm mỗi con lọt một lần (`TDEnemyView` → `TDPressureProbe.RecordLeakedEnemy`), và T chia theo số cổng
(`TDLoadModel.GatesFor`, đọc từ `TDStageRepository`). Số từng trận nằm trong `t11-runs-attempt1.log` của
workspace.

## Lần đo 2

Data lúc đo: DEMO-1 `spawnInterval` 2, `waveGrowth` 2,7. `bossWaveMult` 2,0 / 2,5 / 2,5. `mixRamp` 1,0 / 0,8 / 0,4.

| Trận | Độ khó | Kết quả | Wave đầu ≥ 2 con lọt | ρ thô | Suy sụp | r |
|--:|---|---|--:|--:|--:|--:|
| 1 | Normal | Victory | 6 | 0,52 | 6 | −0,37 |
| 2 | Normal | Victory | 6 | 0,52 | 6 | 0,21 |
| 3 | Normal | GameOver | 5 | 0,48 | 3 | 0,67 |
| 4 | Hard | GameOver | 5 | 0,58 | 5 | 0,50 |
| 5 | Hard | GameOver | 5 | 0,58 | 5 | 0,84 |
| 6 | Hard | GameOver | 5 | 0,58 | 7 | 0,74 |
| 7 | Nightmare | GameOver | 4 | 0,55 | 4 | 0,51 |
| 8 | Nightmare | GameOver | 5 | 0,67 | 2 | 0,77 |
| 9 | Nightmare | GameOver | 4 | 0,55 | 3 | 0,62 |

`e` đo được: **0,51 / 0,58 / 0,59**.

Hai điều thấy được trong số liệu:

- **Wave boss quá nặng.** Wave boss (×2–2,5) để lọt 13–19 con.
- **Mô hình đọc quá tay wave nhỏ.** Wave 1–2 có ρ đã hiệu chỉnh 1,1–1,6, vậy mà không trận nào lọt con nào ở đó.

## Tìm núm

Tìm trên chính code mô hình, mọi config nạp một lần ngoài vòng lặp.

| Tìm với | Kết quả |
|---|---|
| Mọi kiểm `RHO_*`; mỗi level `spawnInterval` 1–4 s, `waveGrowth` 1–4; mỗi độ khó `mixRamp`, `bossWaveMult` | 0 / 28 561 tổ hợp pass. Tốt nhất ở mép lưới (4 s) |
| Như trên, bỏ `RHO_EARLY`, `spawnInterval` tới 5 s | 2 tổ hợp pass: DEMO-1 4,5–5 s, `waveGrowth` 1,5–1,75; DEMO-2 2,25 s; boss ×1,0 |
| Giữ data level, `e` thả tự do | Pass khi `e` ≈ giá trị đo ÷ 0,56–0,63, boss ≈ ×1,0–1,4 |

## Quyết định

User chọn **giữ mật độ, coi bot ≈ 0,6 người chơi chuẩn**.

- `EfficiencyOf` = `e` đo được ÷ `BotStrength` **0,58**, ra 0,88 / 1,00 / 1,02. Đây là giá trị duy nhất trong
  khoảng 0,55–0,65 mà cả ba độ khó đều đạt mục tiêu. Nếu dùng 0,60 thì Nightmare cao hơn mục tiêu 3%.
- `bossWaveMult` 2,0 / 2,5 / 2,5 → 1,2 / 1,15 / 1,2. `mixRamp` 1,0 / 0,8 / 0,4 → **1,0 / 0,65 / 0,3**. Trong các
  tổ hợp pass, chọn boss lớn nhất mà vẫn giữ mỗi `mixRamp` gần giá trị cũ và đúng thứ tự giữa các độ khó.
- `RHO_EARLY` rời validator, chuyển thành phép đo bằng bot: ở các wave mô hình coi là chưa đủ trần, mỗi wave lọt
  < 2 con.
- Sau lượt đo Step 4 đầu tiên (bên dưới), user chọn thêm hai thay đổi:
  - r chỉ tính trên melee và gộp nhiều trận;
  - chỉnh lại để wave 3 Hard thôi để lọt.

  Thay đổi nhỏ nhất làm được việc này là DEMO-1 `totalEnemies` **56 → 50**, kèm `bossWaveMult` Hard 1,15 → **1,3**
  và Nightmare 1,2 → **1,15**. Kết quả là `bossWaveMult` **1,2 / 1,3 / 1,15**. Không giá trị `waveGrowth` 2,3–3,3
  hay `mixRamp` Hard nào làm nhẹ wave 1–3 mà validator vẫn pass.
- `spawnInterval` và `waveGrowth` **không đổi**.

## ρ sau hiệu chỉnh

DEMO-1 Hard có 50 con. Cỡ wave: 3, 4, 4, B7, 6, 7, 8, B11. Thành phần wave 1–3 (Normal/Fast/Tank) là 2/1/0, 3/1/0,
2/1/1.

| Level · độ khó | ρ theo wave (B = boss) |
|---|---|
| DEMO-1 Normal | 0,71 0,65 0,50 0,71 0,55 0,59 0,73 B1,04 |
| DEMO-1 Hard | 0,69 0,63 0,50 B0,86 0,68 0,80 0,93 B1,31 |
| DEMO-1 Nightmare | 0,97 0,84 B0,70 0,75 0,78 B1,12 1,04 B1,36 |
| DEMO-2 Normal | 0,83 0,55 0,50 0,64 0,56 0,64 0,67 B0,94 |
| DEMO-2 Hard | 0,75 0,69 0,58 B0,97 0,74 0,77 0,85 B1,25 |
| DEMO-2 Nightmare | 1,06 0,79 B0,92 0,84 0,90 B1,27 1,09 B1,38 |

## Ba phép đo ở Hard

Trần cần 94 vàng: 5 người đầu của đội chuẩn, trừ 30 vàng khởi đầu, nghĩa là phải kiếm thêm 64.

### Lượt đầu: `bossWaveMult` 1,2 / 1,15 / 1,2, DEMO-1 56 con, r trên mọi lần đứng chốt (`t11-step4.log`)

| Trận | Bot | Kết quả | Lọt ở wave 1–3 | Suy sụp | r | Wave đầu `goldEarned` ≥ 64 |
|--:|---|---|---|--:|--:|--:|
| 1 | StartFull | GameOver (wave 7) | 1 / 0 / **2** | 3 | 0,73 | 5 |
| 2 | StartFull | **Victory** | 0 / 0 / 1 | 6 | 0,60 | 4 |
| 3 | StartFull | GameOver | 0 / 0 / **3** | 5 | 0,77 | 5 |
| 4 | StartThree | GameOver | 0 / 1 / **2** | 3 | 0,20 | 4 |

Có hai phép đo trượt:

- **r = 0,60–0,77.** Xạ thủ (Ginger, Moon) không bao giờ mất HP, và stress họ nhận ít (0,2–2,0/s). Melee vừa mất
  HP vừa nhận phần lớn stress (0,9–4,1/s). Vì vậy r gộp mọi lần đứng chốt chủ yếu đo tuyến đầu so với tuyến sau.
  Nếu tính riêng melee và gộp 3 trận thì r = 0,48 (13 lần đứng chốt).
- **Wave 3 Hard lọt 2–3 con.** Giảm boss làm quân dồn sang các wave thường: cỡ wave đổi từ 3,3,4,11,5,6,6,18
  thành 4,5,5,7,7,8,9,11. Thêm vào đó, `mixRamp` 0,8 → 0,65 làm wave đầu có nhiều địch nặng hơn. ρ thô của wave 3
  chỉ tăng 0,50 → 0,55, vì T tăng theo số con, vậy mà số con lọt tăng từ 0 lên 2–3. Đây vẫn là điểm mù của mô
  hình với wave nhỏ, chỉ là lần này theo chiều ngược lại.

### Lượt cuối: data cuối, r melee gộp trận (`t11-step4b.log`)

| Trận | Bot | Kết quả | Lọt ở wave 1–3 | Suy sụp | r melee | Wave đầu `goldEarned` ≥ 64 |
|--:|---|---|---|--:|--:|--:|
| 1 | StartFull | **Victory** | 0 / 0 / 0 | 7 | 0,21 | 5 |
| 2 | StartFull | **Victory** | 0 / 0 / 1 | 6 | 0,29 | 5 |
| 3 | StartFull | GameOver | 0 / 0 / 0 | 4 | 0,70 | 5 |
| 4 | StartThree | GameOver | 0 / 0 / 0 | 2 | n/a (2 lần) | 5 |

| Phép đo | Mục tiêu | Kết quả |
|---|---|---|
| Đầu trận: mỗi wave lọt < 2 con trước trần (wave 1–3 ở Hard) | < 2 | **Đạt**: nhiều nhất 1 |
| Morale có xuất hiện không | ≥ 1 lần suy sụp | **Đạt**: 4–7 lần mỗi trận |
| Tương quan HP ↔ stress, melee, gộp 3 trận `StartFull` | r < 0,5 | **Đạt**: 0,38 (14 lần đứng chốt) |
| Chạm trần: StartFull so với StartThree | Lệch ≤ 1 wave | **Đạt**: cùng wave 5 |

Bot thắng Hard 2/3 trận. Điều này khớp với mục tiêu: người chơi chuẩn giữ được tới các wave boss.

## Còn mở

- `BotStrength` 0,58 là ước lượng. Cần người chơi thật thử Hard (spec §7.3). Nếu người chơi cũng thua như bot thì
  mọi độ khó đang khó gấp khoảng 1,7 lần.
- `e` chỉ đo trên DEMO-1. DEMO-2 dùng chung `e` qua mô hình cổng.
- r theo từng trận vẫn dao động mạnh (0,21–0,70) vì mỗi trận chỉ có 4–5 lần đứng chốt melee. Chỉ nên đọc r gộp.
