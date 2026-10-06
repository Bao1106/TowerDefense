# Morale — Mô hình tải (Tầng 0 + 1) · Thiết kế

> **Spec · 06/10/2026 · branch `feature/morale-system`**
>
> Thay thế các phần sau của [`MORALE_SYSTEM_DESIGN.md`](../../../MORALE_SYSTEM_DESIGN.md):
> §03 (vùng áp lực, ngưỡng chịu đựng), §04 (N1, N2, aura Herald), và các hằng số tương ứng ở §10.
> Các phần còn lại của doc gốc giữ nguyên hiệu lực cho tới khi spec Tầng 2–4 thay chúng.

---

## 0 · Tóm tắt

Morale cũ đo **"có địch ở gần không"**: +1,6/s khi có địch trong khối 3×3, cộng thêm khi số địch vượt
`1 + blockCount`. Nó không liên quan gì tới việc đội hình **có gánh nổi tải hay không**, và không biết
tới chỉ số địch hay số người trên sân.

Spec này thay nó bằng **mô hình tải dựa trên nền kinh tế chặn**:

1. Mỗi màn có **trần triển khai** cứng, nhỏ hơn số card mang vào. Trần đếm mọi thứ đứng trên sân,
   kể cả turret.
2. Stress chỉ tăng khi **quá tải**. Quá tải được đo bằng **sự kiện lọt**: một địch đi xuyên qua ô của
   melee đã đầy suất chặn.
3. Mỗi con lọt là **một nhịp** (10 điểm), chia **7/3** cho melee giữ ô và các xạ thủ phủ ô đó.
4. Chỉ số địch, operator, turret và cấu trúc level × độ khó được cân lại quanh một thước đo chung:
   **sức gánh mỗi wave** và **hệ số tải ρ**.

---

## 1 · Vấn đề

| # | Lỗi của mô hình cũ | Hệ quả |
|:--:|---|---|
| 1 | N2 (+1,6/s) bật bất cứ khi nào có địch trong 3×3 | Striker dọn sạch trong 3s và Defender đang ngộp bị tính cùng một mức |
| 2 | Ngưỡng `1 + blockCount` | Thưởng Defender **hai lần** cho cùng một ưu thế (chỗ chặn). Điểm yếu thật của nó (giết chậm) không bao giờ bị tính |
| 3 | N1 đếm địch đi ngang trong bán kính 1 ô | Đo bề rộng lối đi và hình dạng bản đồ, không đo quá tải |
| 4 | Địch nào cũng là "1" | Tank 1080 HP và Fast 180 HP có trọng lượng như nhau |
| 5 | Số người trên sân không có trong công thức | Hai người đứng cạnh nhau cùng thấy một đám địch và **cùng** bị +1,6/s |
| 6 | Xạ thủ không được đăng ký vào `TDOperatorRegistry` | Không nhận cú sốc, không được thưởng hết wave, không được tính là đồng đội, không Rescue được |
| 7 | Không có giới hạn triển khai | Đủ vàng thì đặt hết 8 card, tải mỗi người giảm, morale biến mất |

**Ẩn dụ dùng xuyên suốt spec: quầy thu ngân.** Mỗi melee là một quầy:
- Địch tới = khách tới.
- HP địch = giỏ hàng.
- DPS = tốc độ tính tiền.
- `blockCount` = số chỗ xếp hàng (túi đệm).
- Số operator = số quầy đang mở.
- Hàng đầy thì khách tràn qua, tức là **lọt**.

---

## 2 · Mục tiêu, phi mục tiêu, tiêu chí thành công

**Mục tiêu**
- Morale đo đúng thứ nó cần đo: đội hình có gánh nổi tải không.
- Chỉ số operator, chỉ số địch và số người trên sân đều **nằm trong** công thức.
- Người chơi **nhìn thấy** nguyên nhân gây stress.

**Phi mục tiêu** (thuộc spec khác)
- Tầng 2: đồng hồ stress (tốc độ hồi, giá trị cú sốc, band).
- Tầng 3: hệ quả (suy sụp, Rescue, chết vĩnh viễn, Quyết tử).
- Tầng 4: truyền đạt đầy đủ (icon, âm thanh, slow-mo).
- Skill của operator.

**Tiêu chí thành công**
1. Mọi con số của stress suy ra được từ nền kinh tế chặn. Không con số nào "đứng một mình".
2. Đổi chỉ số địch, số người trên sân hay mật độ wave thì độ căng đổi theo hướng đoán trước được.
3. Bài test A4: có lúc người chơi rút một lính **gần đầy máu** vì stress, và rút vì đúng lý do.
4. Ở Hard: đội không xoay ca thì có người gãy, người chơi biết xoay ca thì tránh được.
   Morale **không vô hình, không bất khả kháng**.

---

## 3 · Quyết định đã chốt

| # | Quyết định | Lý do chính |
|:--:|---|---|
| D1 | Stress chỉ tăng khi **quá tải**. Quá tải kéo dài thì buộc phải **xoay ca** | Chỉ đo xoay ca (B thuần) có chính sách tối ưu giải sẵn ("đỏ là rút"). Chỉ đo phân tải (A thuần) thì xếp đủ người là morale biến mất |
| D2 | **Trần triển khai cứng theo màn**, nhỏ hơn số card | Xoay ca cần người dự bị. Trần là đúng biến số "số quầy được mở" |
| D3 | **Một trần chung**, đếm melee, xạ thủ và turret | Quyết định "thêm chỗ chặn hay thêm tốc độ giết" thành quyết định thật. Bịt lỗ "xây turret không biết mệt" |
| D4 | Mọi địch chiếm **1 suất** chặn. Độ nặng chỉ nằm ở HP | Không thêm luật thứ hai cùng lúc với morale. Trọng lượng chặn để sau |
| D5 | Quá tải được đo bằng **sự kiện lọt** | Duy nhất vừa nhìn thấy được, vừa tách khỏi HP, vừa rẻ để làm |
| D6 | Skill làm **sau**. Nguyên tắc: **chỉ số nền gánh tải trung bình, đỉnh dành cho skill** | SP đã thiết kế sẵn cho skill (Rescue = nửa skill). Làm ngay thì nhân đôi phạm vi |
| D7 | **Mỗi con lọt = 1 nhịp**, không theo HP còn lại | Theo HP thì Bầy đàn (70 HP) gần như vô hại, mất lý do tồn tại |
| D8 | Kẻ gieo sợ **khuếch đại ×2** cú lọt, thay vì aura | Aura gây stress không cần quá tải, ngược D1 |
| D9 | Chia một nhịp **7/3**: melee 70%, các xạ thủ chia nhau 30% | Địch đi qua chân ai thì người đó căng nhất. Yểm trợ giúp đỡ một phần chứ không xoá được stress |
| D10 | 1 nhịp = **10 điểm**, tạm nhân hệ số band | Số khởi điểm. Hiệu chỉnh ở §7 |
| D11 | Giữ thu nhập **1 vàng / 3s** và vàng giết địch như hiện tại. Đo trước rồi mới sửa | Đúng khoảng 3–5s của design. Độ tăng wave đã tự giảm bớt độ lệch |
| D12 | **Không** thêm giáp (DEF) đợt này | Cùng lý do với D4: hạn mức nhận thức. Các vai đã tách bằng trục có sẵn |
| D13 | Người suy sụp **vẫn chiếm suất** (thử nghiệm). Turret **gỡ được** như code hiện tại | Một người gãy là một suất chết, nên Rescue đáng giá hơn. Gỡ turret mất nửa tiền, đó là cam kết thật |

---

## 4 · Thước đo thiết kế

Các công cụ dưới đây dùng để **thiết kế và kiểm tra cân bằng**. Chúng không chạy trong game. Trong game
chỉ có sự kiện lọt (§5.2).

### 4.1 Sức gánh mỗi wave

```
Sức gánh  =  block  +  DPS × T / H

  T : thời gian một wave đổ vào   = số địch mỗi wave × spawnInterval
  H : HP trung bình mỗi địch       (theo tỉ lệ loại địch và hpMult)
```

Cách đọc: **trong một wave, người này lo được bao nhiêu con.**

**Tỉ giá:** 1 ô chặn ≈ `H / T` DPS. Ở DEMO-1 Hard (T = 14s, H ≈ 441), 1 ô chặn ≈ 31,5 DPS.
- Wave dồn ngắn thì chặn đáng giá hơn.
- Wave kéo dài thì DPS đáng giá hơn.

Vì vậy `spawnInterval` của level quyết định vai trò nào mạnh. Level và chỉ số operator phải chỉnh
**cùng nhau**.

**Hai thước đo phụ** cho những thứ sức gánh không đo được:
- **Lọt mỗi bầy:** một bầy 5 con tới trong 0,8s thì lọt mấy con. Đo khả năng đỡ đợt dồn.
- **Sống khi giữ Tank:** giữ đầy suất bằng Tank (25 DPS) thì sống được bao lâu. Đo khả năng chịu áp lực máu.

### 4.2 Hệ số tải ρ

```
ρ  =  HP địch của wave  /  ( DPS đội × T  +  block đội × H )
```

- ρ < 1: đội gánh kịp trung bình.
- ρ > 1: có lọt.
- "Đội" là **đội chuẩn** ở số người hiện có trên sân. Đội chuẩn được đặt theo thứ tự cố định
  **Knight → Ginger → Striker → Defender → Moon → Ace**. Có n người trên sân thì đội chuẩn là n tên đầu
  danh sách. Validator (§7.1) và bot đo (§7.2) dùng cùng thứ tự này.

### 4.3 Bốn nguyên tắc cân bằng

1. **Không ai hơn ai ở mọi mặt.** Validator sẽ kiểm (§7.1).
2. **Trần làm một suất đắt hơn vàng.** Đánh giá một operator theo "có đáng một suất không".
3. **Nguyên tắc nền/đỉnh (D6):** đội đủ trần, xếp đúng thì gánh được tải trung bình (ρ < 1). Đỉnh tải thì lọt.
4. **Tách hai loại áp lực theo loại địch** để máu và stress đi riêng:
   - Địch gây **stress**: tới dồn, đánh nhẹ, làm tràn túi.
   - Địch gây áp lực **máu**: trâu, đánh đau, chiếm chỗ lâu.

---

## 5 · Thiết kế chi tiết

### 5.1 Trần triển khai

**Dữ liệu**
- `deployLimit` cơ sở nằm trong mỗi level, cạnh `totalEnemies`, `waveInterval`, `spawnInterval`.
- Độ khó cộng thêm `deployLimitDelta` (§5.7).
- DEMO-1: cơ sở 5, ra 6 / 5 / 4 cho Normal / Hard / Nightmare.

**Đếm những gì**
- Mọi unit đang đứng trên sân: melee, xạ thủ, turret.
- Operator đang suy sụp **vẫn chiếm suất**.
- Bóng xem trước lúc kéo thả **không** đếm.
- Chỉ có **một nguồn đếm duy nhất**:
  - Tăng khi đặt thành công (`TDTowerFactoryControl.onCreateUnitSuccess`).
  - Giảm khi unit rời sân (rút, chết, turret bị gỡ).

**Giải phóng suất**

| Sự kiện | Suất | Ràng buộc khác |
|---|---|---|
| Rút operator | Trống **ngay** | Cooldown 8s khoá **người** vừa rút, không khoá suất |
| Operator chết | Trống ngay | Cooldown 16s khoá người đó |
| Gỡ turret | Trống ngay | Hoàn 50% giá, không có cooldown (hành vi hiện tại) |

**Khi trần đầy**
- Card trên deploy bar mờ đi.
- Kéo card ra bị từ chối, và bộ đếm nháy để chỉ đúng lý do.

**HUD:** bộ đếm `n/cap` cạnh số vàng.

**Không đổi:** deploy bar vẫn 5 melee + 3 ranged. Mỗi operator chỉ có một bản trên sân.

### 5.2 Sự kiện lọt

**Định nghĩa**

> Một địch tới tâm một ô đường có melee đã đăng ký đứng đó, nhưng `CanBlock` trả về false
> (mọi suất đầy, hoặc melee đang suy sụp), nên nó đi tiếp. **Đó là một lần lọt tại ô đó.**

Điểm móc duy nhất: nhánh `else` trong `TDEnemyView.Update`, cùng chỗ hiện đang xử lý cú đánh lên người
suy sụp.

**Ca biên**

| Tình huống | Có tính lọt không |
|---|:--:|
| Tới ô melee đầy suất, đi qua | **Có** |
| Tới ô melee đang suy sụp, đi qua | **Có** |
| Đi qua ô đường không có ai đứng | Không |
| Được thả (`ForceUnblock`) khi người chặn rút, chết hoặc gãy | **Không**. Nó không thắng được túi đầy. Tính thì xoay ca cũng sinh stress |
| Lọt liên tiếp qua hai melee đầy | **2 lần**, mỗi ô một lần |
| Về tới căn cứ | Không. Để Tầng 2 |

**Người gánh và cách chia**

Người chịu trách nhiệm ô `c` gồm hai nhóm:
- **Melee giữ ô:** melee đứng ở `c`, nếu chưa suy sụp.
- **Xạ thủ phủ ô:** mọi xạ thủ chưa suy sụp mà tập ô đang bắn được (cùng hàm `TryAttack` dùng, theo
  hướng xoay tại thời điểm lọt) có chứa `c`.

Turret **không** gánh, vì turret không có morale.

| Đội hình tại ô | Melee nhận | Mỗi xạ thủ nhận |
|---|--:|--:|
| Chỉ có melee | 100% | |
| Melee + k xạ thủ | **70%** | 30% / k |
| Melee đã suy sụp, có k xạ thủ | | 100% / k |
| Không còn ai gánh | Nhịp đó mất | |

**Độ nặng**

```
stress cộng cho người nhận  =  STRESS_PER_LEAK × phần chia × hệ số Herald × hệ số band của người nhận

STRESS_PER_LEAK   = 10
LEAK_SHARE_MELEE  = 0.7
Hệ số Herald      = 2 nếu tâm ô lọt cách ít nhất một Kẻ gieo sợ còn sống ≤ 4 ô (khoảng cách Euclid);
                    ngược lại 1. Không cộng dồn khi có nhiều con
Hệ số band        = 1 / 1,5 / 2 (Calm / Steady / Stressed), tạm thời, chốt ở Tầng 2
```

Stress được cộng qua `TDOperatorMorale.SetValue`, nên gãy vì lọt phát đủ chuyển trạng thái suy sụp
(sửa ở Task 1 của plan M1).

**Số con lọt từ 0 tới gãy** (chưa tính hồi phục):

| | → Steady | → Stressed | → Gãy | Tổng |
|---|--:|--:|--:|--:|
| Melee đứng một mình | 4 | 2 | 2 | **8** |
| Melee có xạ thủ yểm trợ | 5 | 4 | 2 | **11** |
| Xạ thủ, 1 người phủ ô | 12 | 7 | 6 | **25** |

**Phản hồi tối thiểu** (bản đầy đủ thuộc Tầng 4):
- Mỗi lần lọt, icon morale của từng người nhận nảy lên một nhịp.
- Melee đang đầy suất có dấu hiệu dưới chân. Đây là cảnh báo trước "con sau sẽ lọt".

### 5.3 Đưa xạ thủ vào hệ

- `TowerZoneOperatorBehavior.OnInit` gọi `RegisterOperatorView`. **Không** gọi `RegisterOperator`,
  vì đó là danh sách chặn.
- `OnRemove` gỡ đăng ký tương ứng.
- Hệ quả mặc định: xạ thủ hưởng mọi luật morale đang duyệt danh sách view (cú sốc, thưởng hết wave,
  Rescue, đếm đồng đội), cộng thêm phần lọt ở §5.2.
- Ghi chú cho Tầng 3: xạ thủ đứng ngoài đường nên khi suy sụp không bao giờ bị đánh, và tự hồi sau
  khoảng 34 giây.

### 5.4 Gỡ, giữ, định nghĩa lại hệ cũ

| Thành phần | Số phận |
|---|---|
| N2: `STRESS_BASE_RATE` | **Gỡ** |
| N1: `STRESS_PER_OVERLOAD`, vùng 3×3, `TDPressureProbe.Tolerance`, `TDMoraleContext.enemiesInZone` / `tolerance` | **Gỡ** |
| Aura Kẻ gieo sợ | **Thay** bằng khuếch đại (§5.2) |
| Hồi khi rảnh −0,6/s | **Định nghĩa lại:** rảnh = không chặn ai **và** không có mục tiêu trong tầm. Thay `enemiesInZone > 0` bằng cờ `engaged` |
| Đồng đội Calm đứng kề −0,4/s | **Gỡ khỏi stress.** Theo cấu trúc nó chỉ trừ vào N1 + N2, nên tự bằng 0. Giữ `calmAlliesAdjacent` cho Ý chí |
| N3: cú sốc khi đồng đội gãy (+30) / chết (+20) | Giữ |
| N4: aura Boss | Giữ, xem lại ở Tầng 2 |
| Hết wave −15, Retreat −70 + cooldown 8s, chết, suy sụp, Rescue, SP | Giữ |
| Band 1 / 1,5 / 2 | Giữ, nhân vào nhịp lọt (tạm) |
| `SecondsToBreak` và `TDMoraleDebugOverlay` | Đổi thành **"còn mấy con lọt thì gãy"** |
| `TDPressureProbe` | Chuyển thành máy ghi lọt (§7.2) |
| Doc gốc §03, §04, §10 | Đánh dấu "đã thay thế", trỏ sang spec này |
| Plan M1 | Task 1–3 giữ. **Task 4–6 thay** bằng plan mới. Task 7 (đồng bộ doc) giữ |

Hệ quả: bỏ N2 thì operator **đang đánh mà giữ tốt** sẽ không tăng stress và cũng không hồi. Đây là câu
hỏi đầu tiên của Tầng 2.

### 5.5 Địch

| | Normal | Fast | Tank | Boss | 🆕 Bầy đàn | 🆕 Kẻ gieo sợ |
|---|--:|--:|--:|--:|--:|--:|
| `type` | 0 | 1 | 2 | 3 | 4 `Horde` | 5 `Herald` |
| `baseHP` | 300 | 150 | 900 | 3000 | 70 | 500 |
| `baseSpeed` | 3 | 6 | 1,5 | 1 | 4 | 2 |
| `baseAttackDamage` | 10 | 5 → **2** | 25 → **50** | 50 | 2 | 0 |
| `baseAttackSpeed` | 1 | 1,5 | 0,5 | 0,75 | 1 | 0 |
| DPS lên người chặn | 10 | **3** | **25** | 37,5 | 2 | 0 |
| `dieDuration` | 0 | 0,63 | 2 | 1,33 | 0,25 | 1 |
| `goldReward` | 2 | 4 | 8 | 10 | 1 | 12 |
| Vai | Chuẩn đo | Stress: tới dồn | Máu: đánh đau, chiếm chỗ | Cả hai + aura | Stress: tràn túi theo bầy | Khuếch đại stress |
| Khắc chế | | DPS cao | Xạ thủ xả túi, Tart | Cả đội | Túi lớn, Ace, nổ lan | Bắn chết từ xa (Ginger) |

**Bầy đàn** (giữ theo §11 của doc gốc):
- Tính theo **suất**, 1 suất nở thành 5 con.
- Nở **sau** Fisher–Yates và **trước** khi tính tổng địch.
- Các con trong một bầy ra cách nhau 0,2s (`HORDE_PACK_SPAWN_INTERVAL`).
- Hiệu năng: pool riêng, không Animator, không HP bar, bật GPU Instancing.

**Kẻ gieo sợ:**
- Chặn được. Không tấn công.
- Vòng tròn trên mặt đất bán kính 4 ô, hiển thị vùng khuếch đại.

### 5.6 Operator và turret

**Operator**

| | Vai | Giá | HP | Đòn × tốc | DPS | Block | `attackType` / đặc tính |
|---|---|--:|--:|---|--:|:--:|---|
| Defender | Tường | 20 | 3000 | 20 → **30** × 1,0 | 30 | 3 | Single |
| Tart | Chịu đòn | 22 → **20** | 4000 → **5000** | 15 → **25** × 0,8 | 20 | 3 → **2** | Single |
| Knight | Cân bằng | 15 → **18** | 1000 → **1200** | 50 → **40** × 2,0 | 80 | 2 | Single |
| Striker | Xả nhanh | 18 | 700 | 80 → **100** × 1,5 | 150 | 1 | Single |
| Ace | Quét bầy | 18 → **20** | 800 → **900** | 70 → **25** × 1,8 → **1,5** | 37,5 mỗi mục tiêu | 1 → **3** | **Multiple: đánh mọi địch đang chặn** |
| Layla | Mở màn | 18 → **10** | 750 → **900** | 75 → **45** × 1,5 | 67,5 | 1 | Single, tầm 4 ô (giữ) |
| Ginger | Bắn tỉa | 16 → **18** | 600 | 65 → **120** × 1,2 → **0,8** | 96 | 0 | Single, tầm 9 ô |
| Moon | Phép lan | 20 | 700 | 80 → **40** × 1,0 | 40 mỗi mục tiêu | 0 | **Nổ lan 1 ô quanh mục tiêu** |

Ghi chú: Defender, Striker và Layla đổi `attackType` từ Multiple về Single trong data, để khớp hành vi.

**Ba thước đo** (DEMO-1 Hard, 1 ô chặn ≈ 31,5 DPS):

| | Sức gánh | Lọt mỗi bầy 5 con | Sống khi giữ đầy Tank |
|---|--:|--:|--:|
| Defender | 3,95 | 2 | 40 s |
| Tart | 2,63 | 3 | 100 s |
| Knight | 4,54 | 3 | 24 s |
| Striker | 5,76 | 3 | 28 s |
| Ace | 4,19 (túi đầy 6,57) | 2, dọn trong ~2s | 12 s |
| Layla | 3,14 | 4 | 36 s |
| Ginger | 3,05 | | |
| Moon | 1,27 (cả bầy ~6,3) | Tỉa trước khi tới tuyến | |

**Thay đổi code đi kèm**
- **Melee đọc `attackType`:**
  - `Single` đánh 1 mục tiêu (hành vi hiện tại).
  - `Multiple` đánh **mọi địch đang chặn**. Không chặn ai thì đánh 1 địch trong tầm.
  - Comment trong `PathCellOperatorBehavior` về việc Striker từng tăng DPS ×4 phải được cập nhật:
    data giờ quyết định, và chỉ Ace là Multiple.
- **Xạ thủ nổ lan:**
  - `TowerZoneOperatorBehavior` gây sát thương đầy đủ cho mọi địch trong bán kính 1 ô quanh mục tiêu
    chính, khi data đánh dấu nổ lan.
  - Ưu tiên tách và dùng chung logic nổ lan đang có của turret (`TDTowerBehaviorMainControl.AttackTargets`).

**Turret.** Luật: mỗi suất turret mạnh bằng **60–75%** operator đánh xa tương ứng. Phần thiếu hụt là giá
của việc không bao giờ stress hay gãy.

| | Giá | Hiện tại | Đề xuất |
|---|--:|---|---|
| Cannon | 5 | 5 × 3, đơn mục tiêu (15) | **10** × 3 (30) |
| Catapult | 10 | 20, 2 mục tiêu | **30**, 2 mục tiêu |
| MissileG02 | 15 → **12** | 10, 3 mục tiêu | **20**, 3 mục tiêu |
| MissileG03 | 15 | 20, nổ lan | **30**, nổ lan (75% Moon) |
| Mortar | 20 | 15 × 2, đơn mục tiêu (30) | **30** × 2 (60, 62% Ginger) |

MissileG02 hạ giá 15 → 12. Nếu không hạ, MissileG03 (cùng giá 15, DPS 30 so với 20, tầm 11 ô so với 6)
sẽ hơn nó ở mọi mặt, vi phạm nguyên tắc 1. Kiểm "không ai hơn ai" (§7.1) phải bắt được đúng ca này.

Ý chí suy ra từ chỉ số nên các con số Ý chí sẽ đổi. Quyết tử đang hoãn nên chưa ảnh hưởng.

### 5.7 Level × độ khó

**Chia vai**
- **Level** quyết định **nhịp và sức chứa:** `waveCount`, `totalEnemies`, `waveInterval`,
  `spawnInterval`, `deployLimit`, `waveGrowth` (mới).
- **Độ khó** quyết định **tải**, và điều chỉnh vài thông số của level: thành phần 6 loại địch,
  `hpMult`, `speedMult`, tham số boss, `deployLimitDelta`, `startingGold`.

**Độ tăng wave (`waveGrowth`)**
- `waveGrowth` = tỉ lệ cỡ wave thường **cuối** so với wave thường **đầu**. Các wave ở giữa nội suy
  tuyến tính.
- Khởi điểm **2,7** (tương đương trọng số 0,6 → 1,6). `waveGrowth = 1` là hành vi hiện tại (mọi wave bằng nhau).
- Chuẩn hoá để **tổng số địch giữ nguyên**.
- Wave boss nhân hệ số boss lên trên kích thước đã tăng.
- Mục tiêu: **tải tiếp tục tăng sau khi đội chạm trần.**

**Bảng độ khó**

| | Normal | Hard | Nightmare |
|---|---|---|---|
| Tỉ lệ suất: Normal / Fast / Tank / Bầy / Kẻ gieo sợ | 55 / 20 / 10 / 10 / 5 | 40 / 22 / 15 / 15 / 8 | 25 / 20 / 22 / 20 / 13 |
| `hpMult` | 1,0 | 1,2 | 2,0 → **1,5** |
| `speedMult` | 1,0 | 1,1 | 1,5 → **1,25** |
| Số wave có boss × boss mỗi wave (hệ số cỡ wave) | 1 × 1 (×2,0) | 2 × 1 (×2,5) | 5 × 2 → **3 × 1** (×2,5) |
| `deployLimitDelta` | **+1** | 0 | **−1** |
| `startingGold` | **40** | 30 | 30 |

Thay đổi cấu trúc đi kèm:
- **Bỏ `bossPct`.** Số boss chỉ do một chỗ quyết định (tham số boss, hiện là `GetBossParams`).
  Validator đọc từ cùng nguồn đó.
- **Bỏ** luật Nightmare tự ép tối thiểu 15 wave / 75 địch. Độ dài trận do level quyết định.
- `RatioRow` và `DifficultyRatioTable.Distribute()` mở rộng lên 5 loại không phải boss (doc gốc §11.4).
  Hiện bộ sinh wave **tự chia** trong `BuildWavePlansInternal`, còn validator gọi `Distribute()`: lại hai
  nguồn. Bộ sinh wave phải gọi `Distribute()`.
- `CONFIG_PLAYER_STARTING_GOLD` chuyển thành giá trị theo độ khó.

**Mục tiêu ρ**

| Giai đoạn | Normal | Hard | Nightmare |
|---|--:|--:|--:|
| Đầu trận (chưa đủ trần) | 0,5 | 0,6 | 0,7 |
| Lúc vừa chạm trần | 0,6 | 0,75 | 0,85 |
| Wave thường cuối trận | 0,75 | 0,9 | 1,0 |
| Wave boss / bầy | 1,0 | 1,2 | 1,4 |

**Đường cong hiện tại để đối chiếu** (DEMO-1 Hard, mô hình lý tưởng, mua khoảng 1 người mỗi wave):

| Wave | 1 | 2 | 3 | 4 (boss) | 5 | 6 | 7 | 8 (boss) |
|---|--:|--:|--:|--:|--:|--:|--:|--:|
| Số người | 2 | 3 | 4 | 5 | 5 | 5 | 5 | 5 |
| ρ | 0,83 | 0,51 | 0,38 | 0,62 | 0,33 | 0,33 | 0,33 | 0,62 |

Đường cong đang **ngược**: đầu trận căng gấp 2,5 lần cuối trận.

---

## 6 · Danh sách thay đổi theo file

| File | Thay đổi |
|---|---|
| `Model/Config/TDLevelConfigSettings.cs` | `LevelConfig` thêm `deployLimit`, `waveGrowth`. `RatioRow` thêm tỉ lệ Bầy / Kẻ gieo sợ, `deployLimitDelta`, `startingGold`. Bỏ `bossPct` |
| `Level Config.asset` | Cả 2 level: `deployLimit` = 5, `waveGrowth` = 2,7 |
| `Control/PathControl/TDEnemyPathMainControl.cs` | Wave theo `waveGrowth`. Nở bầy. Tham số boss mới. Bỏ ép 15/75 |
| `Model/Config/Enum/Enums.cs` | `EnemyType` thêm `Horde`, `Herald` |
| `Enemy Data Config.asset` + prefab | Chỉ số §5.5, 2 loại địch mới |
| `View/GamePlay/Enemy/TDEnemyView.cs` | Phát sự kiện lọt ở nhánh `else` |
| `Control/Tower/TDOperatorRegistry.cs` | Tính người gánh và chia 7/3, hệ số Herald, phát nhịp lọt |
| `Model/Info/Morale/TDOperatorMorale.cs` | Thêm hàm nhận nhịp lọt. Gỡ N1, N2, đồng đội Calm khỏi `NetRate`. `engaged` thay `enemiesInZone`. "Còn mấy con lọt thì gãy" |
| `View/GamePlay/Tower/TDOperatorView.cs` | Dựng context mới (bỏ `Tolerance`) |
| `Control/Tower/TDPressureProbe.cs` | Bỏ vùng áp lực, chuyển thành máy ghi lọt |
| `Control/Tower/OperatorBehavior/PathCellOperatorBehavior.cs` | Đọc `attackType` |
| `Control/Tower/OperatorBehavior/TowerZoneOperatorBehavior.cs` | Đăng ký morale, nổ lan |
| `Melee Operator Config.asset`, `Tower Bullet Config.asset` | Chỉ số §5.6 |
| Đếm trần: placement (`TDPlaceTowerControl`, `TDDeployController`), card (`TDSlotHolderItemView`), HUD (`TDGameplayHUDView`) | Trần chung §5.1 |
| `Model/Config/Constant/TDConstant.cs` | Thêm `STRESS_PER_LEAK`, `LEAK_SHARE_MELEE`, `HERALD_LEAK_MULT`, `HERALD_RADIUS`, `HORDE_PACK_SPAWN_INTERVAL`. Gỡ `STRESS_BASE_RATE`, `STRESS_PER_OVERLOAD`, `STRESS_ALLY_CALM_RELIEF`, `STRESS_RELIEF_CAP` |
| `View/GamePlay/Tower/TDMoraleDebugOverlay.cs` | Hiện số con lọt tới khi gãy |
| `Editor/TDMoraleValidator.cs`, `Editor/TDBalanceValidator.cs` | §7.1 |

---

## 7 · Kiểm chứng

### 7.1 Validator trong Editor (tự động)

| Validator | Kiểm tra |
|---|---|
| `TDMoraleValidator` | Bảng "8 / 11 / 25 con lọt thì gãy". Chia 7/3 ở mọi ca biên: không xạ thủ, melee suy sụp, turret không gánh, không còn ai gánh. Herald ×2 không cộng dồn. Hệ số band. Gãy vì lọt phát cạnh gãy. "Rảnh" mới bật hồi đúng lúc |
| `TDBalanceValidator` | Bảng ρ theo từng wave cho mọi level × độ khó, so với mục tiêu §5.7. Boss chỉ có một nguồn. Bầy nở đúng: tổng HUD = tổng thật |
| Kiểm "không ai hơn ai mọi mặt" (mới) | So từng cặp **trong cùng nhóm**: melee với melee, xạ thủ với xạ thủ, turret với turret. Báo lỗi nếu A ≥ B ở **mọi** trục và hơn hẳn ở ít nhất một trục. Trục: block, DPS đơn mục tiêu, HP, giá (thấp hơn là hơn), số ô tầm, đánh nhiều mục tiêu (có hơn không). Turret bỏ trục block và HP |

### 7.2 Bot đo trong Play Mode (hiệu chỉnh)

**Bot đội chuẩn:**
- Lượt đo ép deploy bar chứa đủ đội chuẩn (§4.2).
- Đặt theo đúng thứ tự đội chuẩn mỗi khi đủ vàng và còn suất: melee lên ô đường gần nút hội tụ, xạ thủ
  lên ô phủ được ô đó.
- Không xoay ca.
- Điều khiển qua MCP.

**Máy ghi (`TDPressureProbe`):**
- Theo operator: số lần lọt nhận, stress theo nguồn (lọt / cú sốc / aura), HP mất.
- Theo wave: ρ mô hình so với lọt thật.

| Phép đo | Mục tiêu | Nếu trượt |
|---|---|---|
| Hiệu suất thật (lọt thật so với ρ mô hình) | Một hệ số hiệu chỉnh cho mỗi độ khó | Chỉnh `totalEnemies` và `waveGrowth` cho khớp mục tiêu ρ |
| Chạm trần ở wave mấy: bot đủ người so với bot chỉ đặt 3 người | Lệch ≤ 1 wave | Giảm vàng giết địch trước (D11) |
| Tương quan HP mất ↔ stress tăng cho mỗi lần đứng chốt | r < 0,5 | Vai Fast / Tank chưa tách đủ, quay lại §5.5 |
| Ca bắt buộc: Bầy đàn + Kẻ gieo sợ vào Knight đứng một mình | Một bầy không đưa được từ Calm thẳng tới gãy | Giảm `STRESS_PER_LEAK` hoặc `HERALD_LEAK_MULT` |
| Morale có xuất hiện không | Ở Hard, bot không xoay ca có ≥ 1 người gãy mỗi trận | Tăng tải hoặc giảm trần |

**Đo hai lượt:** sau mốc C, và sau mốc D (§8).

### 7.3 Người chơi (cổng cuối)

- **Không vô hình, không bất khả kháng:** ở Hard, người chơi biết xoay ca thì tránh được gãy, trong khi
  bot không xoay ca thì có người gãy.
- **A4:** người chơi có lúc rút một lính gần đầy máu vì stress đỏ.

---

## 8 · Mốc triển khai

| Mốc | Nội dung | Phụ thuộc |
|:--:|---|---|
| **A** | Trần (§5.1). Sự kiện lọt (§5.2). Đưa xạ thủ vào hệ (§5.3). Gỡ hệ cũ (§5.4). Validator morale | Task 1–3 của plan M1 |
| **B** | Chỉ số operator / turret / địch hiện có (§5.5–5.6). Melee đọc `attackType`. Moon nổ lan. Kiểm "không ai hơn ai" | A |
| **C** | Cấu trúc level × độ khó, `waveGrowth`, bảng độ khó (§5.7). Bảng ρ trong validator | B |
| — | **Đo lượt 1** + hiệu chỉnh | C |
| **D** | Bầy đàn + Kẻ gieo sợ + hiệu năng | C |
| — | **Đo lượt 2** + hiệu chỉnh | D |

---

## 9 · Ngoài phạm vi

| Hạng mục | Thuộc |
|---|---|
| Hồi khi đang giữ tốt. Stress khi địch về căn cứ. Giá trị cú sốc. Band có nhân vào nhịp lọt hay không. Aura Boss thành khuếch đại | Tầng 2 |
| Xạ thủ suy sụp có nên bị đe doạ. Chết vĩnh viễn. Quyết tử. 20 mạng so với 3–10 của Arknights | Tầng 3 |
| Icon, âm thanh, slow-mo, dấu đầy suất bản đầy đủ. Bài test 10 giây | Tầng 4 |
| Skill (2–3 loại theo vai, dùng chung SP với Rescue) | Sub-project riêng |
| Giáp DEF / kháng phép RES. Trọng lượng chặn của địch | Sau, có thể cùng skill |

---

## 10 · Phương án đã loại

| Phương án | Bị loại vì |
|---|---|
| Đo quá tải bằng **cân dòng tải** (HP/s so với DPS nhóm) | Người chơi không thấy nguyên nhân. Cần UI mới. Phải định nghĩa "nhóm" |
| Đo bằng **giữ quá lâu** | Giữ lâu = bị đánh lâu. HP và stress tụt cùng nhịp, trượt A4 từ cấu trúc |
| Giới hạn số người **bằng vàng** | Chơi giỏi thì giàu, giàu thì đông người, đông người thì ít căng |
| Giới hạn **bằng bản đồ** | Phụ thuộc generator. Người chơi đọc ra là "không có ô", không phải luật |
| **Hai trần** riêng melee / ranged | Mất quyết định "đệm hay tốc độ". HUD hai số |
| Trần **chỉ đếm người** | Dư vàng thì xây turret, morale nhạt dần qua cửa sau |
| **Trọng lượng chặn** ngay đợt này | Luật thứ hai cùng lúc với morale. Để sau |
| Lọt **theo HP còn lại** | Bầy đàn vô hại, Tank thành địch gây stress, ngược nguyên tắc 4 |
| Kẻ gieo sợ giữ **aura** | Gây stress không cần quá tải, ngược D1 |
| Chia lọt **đều** / **6/4** | Pha loãng trách nhiệm người chặn. Xạ thủ tầm rộng nhận quá nhiều |
| Đổi thu nhập sang **kiểu DP** ngay | Lập luận "dư vàng cuối trận" sai (Arknights cũng dư DP). Độ lệch thời điểm chạm trần cần đo trước |
| **Turret khoá suất vĩnh viễn** | Dựa trên thông tin sai (game đã có gỡ turret). Giữ hành vi hiện tại |
| Làm **skill ngay** | Nhân đôi phạm vi. Skill không chỉnh được trước khi có số đo lọt |

---

## Phụ lục · Tham chiếu Arknights

| Núm | Arknights | Spec này |
|---|---|---|
| Giới hạn trên sân | 8–9 trên đội 12 + 1 (~65%). CC hạ còn 4 / 2 | 6 / 5 / 4 trên 8 (75 / 62 / 50%) |
| Thu nhập | 1 DP/s cố định | 1 vàng / 3s + vàng giết (giữ, đo trước) |
| Đặt lại | Giá ×1,5 rồi ×2, chờ 70s. Executor 18s | Giá ×1, chờ 8s. Cố ý đi ngược: xoay ca là động từ chính, cái giá trả bằng stress |
| Challenge Mode | HP / ATK +10–20%, một ràng buộc về chất, 1 mạng, **cùng** thành phần địch | Hard: máu +20%, trần cơ sở. Nightmare: máu +50%, trần −1 |
| Tăng tốc độ địch | Một núm rủi ro riêng (+50%) | Nightmare +25% |
| Địch chiếm nhiều suất | 2–4 suất | Hoãn (D4) |
| Địch bầy yếu | Originium Slug (23 / 41 con ở 1-7) | Bầy đàn |

Nguồn:
- [Deployment Point](https://arknights.wiki.gg/wiki/Deployment_Point)
- [Cost](https://arknights.wiki.gg/wiki/Cost)
- [Redeployment time](https://arknights.wiki.gg/wiki/Redeployment_time)
- [Challenge Mode](https://arknights.wiki.gg/wiki/Challenge_Mode)
- [1-7](https://arknights.wiki.gg/wiki/1-7)
- [7-18](https://arknights.wiki.gg/wiki/7-18)
- [Transport Hub Contracts](https://arknights.wiki.gg/wiki/Transport_Hub/Contracts)
- [Block count](https://arknights.wiki.gg/wiki/Block_count)
- [Mizuki & Caerula Arbor](https://arknights.wiki.gg/wiki/Mizuki_%26_Caerula_Arbor)
