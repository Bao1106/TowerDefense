# Morale — Mô hình tải (Tầng 0 + 1) · Thiết kế

> **Spec · 06/10/2026 · branch `feature/morale-system`**
>
> Thay thế các phần sau của [`MORALE_SYSTEM_DESIGN.md`](../../../MORALE_SYSTEM_DESIGN.md):
> §03 (vùng áp lực, ngưỡng chịu đựng), §04 (N1, N2, aura Herald), và các hằng số tương ứng ở §10.
> Các phần còn lại của doc gốc giữ nguyên hiệu lực cho tới khi spec Tầng 2–4 thay chúng.
>
> **Sửa 07/10/2026:** tải tăng theo trận bằng **mật độ** và **thành phần**, không bằng cỡ wave (D14, §5.7).
> Lý do: bảng ρ đầu tiên (plan Task 8) cho thấy wave to hơn mà ra lâu tương ứng thì ρ đi ngang.
>
> **Trạng thái 08/10/2026: đã triển khai mốc A–D**, gồm cả hai lượt đo. Commit cuối `eee355d4`.
> Còn lại: cổng người chơi §7.3, do user tự làm.

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
| D8 | Kẻ gieo sợ **khuếch đại** cú lọt (×2 → **×1,5** sau lượt hiệu chỉnh 2; không áp cho thân Bầy), thay vì aura | Aura gây stress không cần quá tải, ngược D1 |
| D9 | Chia một nhịp **7/3**: melee 70%, các xạ thủ chia nhau 30% | Địch đi qua chân ai thì người đó căng nhất. Yểm trợ giúp đỡ một phần chứ không xoá được stress |
| D10 | 1 nhịp = 10 → **7 điểm** (lượt hiệu chỉnh 2), tạm nhân hệ số band | Số khởi điểm 10. Ca bắt buộc §7.2 hạ xuống 7 |
| D11 | Giữ thu nhập **1 vàng / 3s** và vàng giết địch như hiện tại. Đo trước rồi mới sửa | Đúng khoảng 3–5s của design. Độ tăng wave đã tự giảm bớt độ lệch |
| D12 | **Không** thêm giáp (DEF) đợt này | Cùng lý do với D4: hạn mức nhận thức. Các vai đã tách bằng trục có sẵn |
| D13 | Người suy sụp **vẫn chiếm suất** (thử nghiệm). Turret **gỡ được** như code hiện tại | Một người gãy là một suất chết, nên Rescue đáng giá hơn. Gỡ turret mất nửa tiền, đó là cam kết thật |
| D14 | Tải tăng theo trận bằng **mật độ** (khoảng cách sinh thu dần) và **thành phần** (địch nặng tăng dần). Cỡ wave tăng chỉ là hệ quả | Wave to hơn mà ra lâu tương ứng thì HP/giây không đổi: ρ đi ngang 0,35 sau trần (bảng ρ đầu tiên). Arknights tăng tải đúng hai trục này (4-4: khoảng cách 3s → 0,8s, loại địch mới ở wave 2) |

---

## 4 · Thước đo thiết kế

Các công cụ dưới đây dùng để **thiết kế và kiểm tra cân bằng**. Chúng không chạy trong game. Trong game
chỉ có sự kiện lọt (§5.2).

### 4.1 Sức gánh mỗi wave

```
Sức gánh  =  block  +  DPS × T / H

  T : thời gian một wave đổ vào   = số địch ở cổng đông nhất × khoảng cách sinh của wave đó (§5.7).
                                    Stage Simultaneous chia wave cho mọi cổng cùng lúc, nên T chia theo số cổng
  H : HP trung bình mỗi địch       (theo tỉ lệ loại địch và hpMult)
```

Cách đọc: **trong một wave, người này lo được bao nhiêu con.**

**Tỉ giá:** 1 ô chặn ≈ `H / T` DPS. Ở DEMO-1 Hard (T = 14s, H ≈ 441), 1 ô chặn ≈ 31,5 DPS.
- Wave dồn ngắn thì chặn đáng giá hơn.
- Wave kéo dài thì DPS đáng giá hơn.

Vì vậy `spawnInterval` của level quyết định vai trò nào mạnh. Level và chỉ số operator phải chỉnh
**cùng nhau**. Khoảng cách sinh thu dần theo trận (D14), nên cuối trận ô chặn đáng giá hơn đầu trận.

**Hai thước đo phụ** cho những thứ sức gánh không đo được:
- **Lọt mỗi bầy:** một bầy 5 con tới trong 0,8s thì lọt mấy con. Đo khả năng đỡ đợt dồn.
- **Sống khi giữ Tank:** giữ đầy suất bằng Tank (25 DPS) thì sống được bao lâu. Đo khả năng chịu áp lực máu.

### 4.2 Hệ số tải ρ

```
ρ  =  HP địch của wave  /  ( DPS đội × T  +  block đội × H )
```

- ρ < 1: đội gánh kịp trung bình.
- ρ > 1: có lọt.
- T và H như §4.1. Quãng nghỉ giữa hai wave không tính vào T: nó như nhau ở mọi wave, nên chỉ làm phẳng hình dạng đường cong.
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

STRESS_PER_LEAK   = 7          (10 trước lượt hiệu chỉnh 2, §7.2)
LEAK_SHARE_MELEE  = 0.7
Hệ số Herald      = 1,5 (2 trước lượt 2) nếu tâm ô lọt cách ít nhất một Kẻ gieo sợ còn sống ≤ 4 ô (khoảng cách Euclid);
                    ngược lại 1. Không cộng dồn khi có nhiều con. **Không áp cho thân Bầy**
Hệ số band        = 1 / 1,5 / 2 (Calm / Steady / Stressed), tạm thời, chốt ở Tầng 2
```

Stress được cộng qua `TDOperatorMorale.SetValue`, nên gãy vì lọt phát đủ chuyển trạng thái suy sụp
(sửa ở Task 1 của plan M1).

**Số con lọt từ 0 tới gãy** (chưa tính hồi phục):

| | → Steady | → Stressed | → Gãy | Tổng |
|---|--:|--:|--:|--:|
| Melee đứng một mình | 5 | 3 | 3 | **11** |
| Melee có xạ thủ yểm trợ | 7 | 5 | 3 | **15** |
| Xạ thủ, 1 người phủ ô | 16 | 11 | 8 | **35** |

Trước lượt hiệu chỉnh 2 (`STRESS_PER_LEAK` 10) là 8 / 11 / 25. Ca bắt buộc §7.2 (một bầy đi qua Knight đứng một
mình cạnh Kẻ gieo sợ) đưa Knight từ 0 tới gãy, nên hạ còn 7 và hệ số Herald còn 1,5. Review cuối thấy "Calm" là
cả band 0–33: từ ≥ 23 một bầy được khuếch đại vẫn làm gãy. Vì vậy Kẻ gieo sợ **không khuếch đại thân Bầy**. Bầy
vốn đã là mối đe doạ tràn túi. Từ 33, một bầy giờ cộng 52,5 (→ 85,5) và không gãy.

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
- Khuếch đại mọi cú lọt trong 4 ô, trừ thân Bầy.
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
  độ dốc thành phần `mixRamp` (D14), `hpMult`, `speedMult`, tham số boss, `deployLimitDelta`, `startingGold`.

**Độ tăng wave (`waveGrowth`): một núm điều khiển cả cỡ lẫn mật độ (D14)**
- Trọng số wave `i` (đếm từ 0, `n` wave): `w_i = 1 + (waveGrowth − 1) × i / (n − 1)`. Có 1 wave thì `w = 1`.
  `waveGrowth` = trọng số wave **cuối** so với wave **đầu**.
- **Cỡ:** tổng địch chia theo `w_i`, wave boss nhân thêm hệ số boss. **Tổng số địch giữ nguyên.**
- **Mật độ:** khoảng cách sinh của wave `i` là `s_i = clamp(spawnInterval / w_i, SPAWN_INTERVAL_FLOOR, spawnInterval)`,
  với `SPAWN_INTERVAL_FLOOR` = **0,8s**. Hệ quả: các wave thường kéo dài gần như nhau, còn HP đổ vào mỗi giây
  tăng theo `w_i`.
  - Không bao giờ thưa hơn `spawnInterval` của level, kể cả khi `waveGrowth < 1`.
  - Level đã đặt `spawnInterval` dưới sàn thì giữ nguyên, không bị nâng lên sàn.
  - Sàn 0,8s lấy từ khoảng cách dày nhất của lính thường ở Arknights 4-4.
- Khởi điểm **2,7**. `waveGrowth = 1` là mọi wave bằng nhau cả về cỡ lẫn mật độ.
- **Vì sao không chỉ tăng cỡ:** T = số con × khoảng cách (§4.1). Cỡ tăng mà khoảng cách giữ nguyên thì T tăng
  cùng tỉ lệ, HP/giây không đổi. Bảng ρ đầu tiên cho DEMO-1 Hard đi ngang 0,35 / 0,35 / 0,35 sau trần.
- Mục tiêu: **tải tiếp tục tăng sau khi đội chạm trần.**

**Thành phần theo tiến độ trận (D14)**
- Tiến độ wave `i`: `t_i = i / (n − 1)`, có 1 wave thì `t = 0`.
- Ba loại **nặng** (Tank, Bầy, Kẻ gieo sợ): tỉ lệ ở tiến độ `t` = tỉ lệ trong bảng × `(1 − r + 2r × t)`.
  Fast giữ nguyên. Normal nhận phần còn lại.
- `r` là **độ dốc thành phần** (`mixRamp`) của độ khó. Tỉ lệ đi tuyến tính từ `1 − r` lần ở wave đầu tới `1 + r`
  lần ở wave cuối, nên **trung bình cả trận (theo wave) đúng bằng bảng tỉ lệ**. `r` chỉ đổi lúc nào địch nặng tới.
- Ràng buộc: Normal ở wave cuối không âm, tức `r ≤ tỉ lệ Normal / tổng tỉ lệ nặng`. Với bảng cuối, giới hạn
  là: Normal 2,2, Hard 1,05, Nightmare 0,45.
- Giá trị (sau lượt hiệu chỉnh 2):
  - Normal **1,0**: wave đầu không có địch nặng.
  - Hard 0,8 → 0,65 → **0,3**.
  - Nightmare 0,4 → 0,3 → **0,0**: địch nặng chia đều mọi wave, vì tỉ lệ nặng trung bình 55% không chừa đủ chỗ để
    bắt đầu từ 0.
  - Lượt 2 hạ `mixRamp` để dồn bớt địch nặng khỏi các wave cuối, nơi Bầy và Kẻ gieo sợ làm bot gãy hàng loạt.
- **Vì sao:** Arknights mở trận bằng địch yếu và đưa loại mới vào sau (1-7 mở bằng slime lẻ tẻ, 4-4 thêm hai
  loại ở wave 2). Đây cũng là cách kéo đầu trận xuống: trước sửa đổi, wave 1 Hard (3 con) đã có Tank.

**Bảng độ khó**

| | Normal | Hard | Nightmare |
|---|---|---|---|
| Tỉ lệ suất: Normal / Fast / Tank / Bầy / Kẻ gieo sợ | 55 / 20 / 10 / 10 / 5 | 40 / 22 / 15 / 15 / 8 | 25 / 20 / 22 / 20 / 13 |
| `hpMult` | 1,0 → **0,85** | 1,2 → **0,9** | 2,0 → 1,5 → **1,05** |
| `speedMult` | 1,0 | 1,1 | 1,5 → **1,25** |
| Số wave có boss × boss mỗi wave (hệ số cỡ wave) | 1 × 1 (×2,0 → 1,2 → **×1,15**) | 2 × 1 (×2,5 → 1,3 → **×1,0**) | 5 × 2 → **3 × 1** (×2,5 → **×1,15**) |
| `deployLimitDelta` | **+1** | 0 | **−1** |
| `startingGold` | **40** | 30 | 30 |
| Độ dốc thành phần `mixRamp` (D14) | **1,0** | 0,8 → 0,65 → **0,3** | 0,4 → 0,3 → **0,0** |

Hệ số cỡ wave boss và `mixRamp` lấy từ lượt hiệu chỉnh 1 (§7.2). Wave boss ×2–2,5 làm bot để lọt 13–19 con, và
không có giá trị nào trên khoảng 1,4 đạt được mục tiêu ρ.

Lượt hiệu chỉnh 2 (Bầy và Kẻ gieo sợ vào) hạ tiếp `hpMult`, `mixRamp` và hệ số boss. Hai level cũng đổi:
DEMO-1 `totalEnemies` 50 → **46**, DEMO-2 `waveGrowth` 2,7 → **3,5**. Chỉ núm độ khó thì không làm cả hai level đạt
mục tiêu ρ cùng lúc. Lý do giảm tải thật: mô hình ρ không thấy một bầy tràn túi hay hệ số Herald, nên phần áp lực
đó phải trả bằng tải mà mô hình thấy (§7.2).

Thay đổi cấu trúc đi kèm:
- **Bỏ `bossPct`.** Số boss chỉ do một chỗ quyết định (tham số boss, hiện là `GetBossParams`).
  Validator đọc từ cùng nguồn đó.
- **Bỏ** luật Nightmare tự ép tối thiểu 15 wave / 75 địch. Độ dài trận do level quyết định.
- `RatioRow` và `DifficultyRatioTable.Distribute()` mở rộng lên 5 loại không phải boss (doc gốc §11.4).
  Hiện bộ sinh wave **tự chia** trong `BuildWavePlansInternal`, còn validator gọi `Distribute()`: lại hai
  nguồn. Bộ sinh wave phải gọi `Distribute()`.
- `Distribute()` nhận thêm tiến độ trận (D14). Vòng sinh wave lấy khoảng cách sinh của từng wave từ cùng hàm
  mà mô hình ρ dùng.
- `CONFIG_PLAYER_STARTING_GOLD` chuyển thành giá trị theo độ khó.

**Mục tiêu ρ**

| Giai đoạn | Normal | Hard | Nightmare |
|---|--:|--:|--:|
| Đầu trận (chưa đủ trần) | 0,5 | 0,6 | 0,7 |
| Lúc vừa chạm trần | 0,6 | 0,75 | 0,85 |
| Wave thường cuối trận | 0,75 | 0,9 | 1,0 |
| Wave boss / bầy | 1,0 | 1,2 | 1,4 |

- **Đầu trận không do validator kiểm.** Mô hình đọc quá tay các wave nhỏ: ρ đã hiệu chỉnh ở wave 1–2 là 1,1–1,6,
  vậy mà 9/9 trận bot không để lọt con nào ở đó. Bot kiểm giai đoạn này thay mô hình: ở các wave mà mô hình
  coi là chưa đủ trần, mỗi wave lọt dưới 2 con (ρ < 1 tương ứng với dưới 2 con lọt, theo đúng định nghĩa
  hiệu suất ở §7.2).
- Mục tiêu tả **người chơi**, không phải bot. Bot chỉ giữ một hàng cố định và không rút quân, nên được coi
  bằng 0,58 người chơi chuẩn (lượt hiệu chỉnh 1). Đây là ước lượng, cần được xác nhận bằng người chơi thật
  ở Hard (§7.3).

**Đường cong hiện tại để đối chiếu** (DEMO-1 Hard, mô hình lý tưởng, mua khoảng 1 người mỗi wave):

| Wave | 1 | 2 | 3 | 4 (boss) | 5 | 6 | 7 | 8 (boss) |
|---|--:|--:|--:|--:|--:|--:|--:|--:|
| Số người | 2 | 3 | 4 | 5 | 5 | 5 | 5 | 5 |
| ρ | 0,83 | 0,51 | 0,38 | 0,62 | 0,33 | 0,33 | 0,33 | 0,62 |

Đường cong đang **ngược**: đầu trận căng gấp 2,5 lần cuối trận.

**Sau bước đầu và dự kiến với D14** (DEMO-1 Hard, ρ **chưa hiệu chỉnh**, ba loại địch hiện có, chỉ số §5.6):

| Wave | 1 | 2 | 3 | 4 (boss) | 5 | 6 | 7 | 8 (boss) |
|---|--:|--:|--:|--:|--:|--:|--:|--:|
| Số người | 2 | 3 | 4 | 5 | 5 | 5 | 5 | 5 |
| Chỉ tăng cỡ (bảng ρ đầu tiên) | 0,76 | 0,45 | 0,34 | 0,62 | 0,35 | 0,35 | 0,35 | 0,61 |
| D14: khoảng cách sinh (s) | 2,00 | 1,61 | 1,35 | 1,16 | 1,01 | 0,90 | 0,81 | 0,80 |
| D14: Normal / Fast / Tank (+ boss) | 2/1/0 | 2/1/0 | 2/1/1 | 5/3/2 +1 | 3/1/1 | 3/2/1 | 2/2/2 | 6/5/6 +1 |
| D14: ρ | 0,54 | 0,36 | 0,41 | 0,86 | **0,49** | **0,54** | **0,63** | 1,30 |

- Sau trần ρ tăng dần 0,49 → 0,63. Wave 1 không còn là wave căng nhất. Mô phỏng cả 2 level × 3 độ khó đều cho
  ρ wave thường cuối ≥ 1,13 × ρ wave thường đầu tiên ở trần.
- **Đỉnh boss vọt quá:** khoảng 2 lần wave thường cuối, trong khi bảng mục tiêu muốn khoảng 1,33 lần
  (Hard 1,2 / 0,9). Lượt hiệu chỉnh vì thế được chỉnh thêm `bossWaveMult` (§7.2).
- Bảng trên tính T theo một luồng. Từ lượt hiệu chỉnh 1, T chia theo số cổng ra cùng lúc (§4.1). DEMO-1 có
  2 cổng, nên ρ thô cao hơn bảng trên (wave thường ×1,2, wave boss ×1,4), còn hình dạng giữ nguyên.

---

## 6 · Danh sách thay đổi theo file

| File | Thay đổi |
|---|---|
| `Model/Config/TDLevelConfigSettings.cs` | `LevelConfig` thêm `deployLimit`, `waveGrowth`. `RatioRow` thêm tỉ lệ Bầy / Kẻ gieo sợ, `deployLimitDelta`, `startingGold`, `mixRamp`. Bỏ `bossPct`. `Distribute` theo tiến độ trận |
| `Level Config.asset` | Cả 2 level: `deployLimit` = 5, `waveGrowth` = 2,7 |
| `Control/PathControl/TDEnemyPathMainControl.cs` | Wave theo `waveGrowth`: cỡ và khoảng cách sinh. Thành phần theo tiến độ. Nở bầy. Tham số boss mới. Bỏ ép 15/75 |
| `Editor/TDLoadModel.cs` | ρ theo §4.2, T theo khoảng cách sinh của từng wave |
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
| `Model/Config/Constant/TDConstant.cs` | Thêm `STRESS_PER_LEAK`, `LEAK_SHARE_MELEE`, `HERALD_LEAK_MULT`, `HERALD_RADIUS`, `HORDE_PACK_SPAWN_INTERVAL`, `SPAWN_INTERVAL_FLOOR`. Gỡ `STRESS_BASE_RATE`, `STRESS_PER_OVERLOAD`, `STRESS_ALLY_CALM_RELIEF`, `STRESS_RELIEF_CAP` |
| `View/GamePlay/Tower/TDMoraleDebugOverlay.cs` | Hiện số con lọt tới khi gãy |
| `Editor/TDMoraleValidator.cs`, `Editor/TDBalanceValidator.cs` | §7.1 |

---

## 7 · Kiểm chứng

### 7.1 Validator trong Editor (tự động)

| Validator | Kiểm tra |
|---|---|
| `TDMoraleValidator` | Bảng "11 / 15 / 35 con lọt thì gãy" (8 / 11 / 25 trước lượt 2). Một bầy qua melee đứng một mình cạnh Kẻ gieo sợ, từ stress 0 và từ đỉnh Calm (33), cộng ≤ 60 và không gãy. Thân Bầy không được khuếch đại. Chia 7/3 ở mọi ca biên: không xạ thủ, melee suy sụp, turret không gánh, không còn ai gánh. Herald ×1,5 không cộng dồn. Hệ số band. Gãy vì lọt phát cạnh gãy. "Rảnh" mới bật hồi đúng lúc |
| `TDBalanceValidator` | Bảng ρ theo từng wave cho mọi level × độ khó, so với mục tiêu §5.7. ρ tăng sau khi chạm trần. Thành phần hợp lệ ở đầu và cuối trận, trung bình đúng bảng. Khoảng cách sinh không thưa dần và không dưới sàn. Boss chỉ có một nguồn. Bầy nở đúng: tổng HUD = tổng thật |
| Kiểm "không ai hơn ai mọi mặt" (mới) | So từng cặp **trong cùng nhóm**: melee với melee, xạ thủ với xạ thủ, turret với turret. Báo lỗi nếu A ≥ B ở **mọi** trục và hơn hẳn ở ít nhất một trục. Trục: block, DPS đơn mục tiêu, HP, giá (thấp hơn là hơn), số ô tầm, số mục tiêu tối đa mỗi đòn. Turret bỏ trục block và HP |

### 7.2 Bot đo trong Play Mode (hiệu chỉnh)

**Bot đội chuẩn:**
- Lượt đo ép deploy bar chứa đủ đội chuẩn (§4.2).
- Đặt theo đúng thứ tự đội chuẩn mỗi khi đủ vàng và còn suất: melee lên ô đường gần nút hội tụ, xạ thủ
  lên ô phủ được ô đó.
- Không xoay ca.
- Điều khiển qua MCP.

**Máy ghi (`TDPressureProbe`):**
- Theo operator: số lần lọt nhận, stress theo nguồn (lọt / cú sốc / aura), HP mất.
- Theo wave: ρ mô hình so với lọt thật. Lọt được ghi hai cách: **sự kiện lọt** (mỗi lần qua một melee đầy, là thứ morale cảm nhận) và **con địch đã lọt** (mỗi con tính một lần, là thứ hiệu chỉnh dùng: một con qua cả hàng bốn melee là một lần đội không giữ được, không phải bốn).

| Phép đo | Mục tiêu | Nếu trượt |
|---|---|---|
| Hiệu suất thật (lọt thật so với ρ mô hình) | Một hệ số hiệu chỉnh cho mỗi độ khó: ρ chưa hiệu chỉnh của wave thường đầu tiên có ≥ 2 con lọt (**không tính thân Bầy**: bầy tràn túi là một cú dồn mà ρ trung bình theo thời gian không thấy, ca bắt buộc bên dưới đo nó), chia cho sức bot (0,58 người chơi chuẩn, §5.7). Từ lượt 2, hệ số giữ của lượt 1; Bầy và Kẻ gieo sợ được hiệu chỉnh **theo kết quả bot** (giảm tải thật tới khi validator pass) | Chỉnh cho khớp mục tiêu ρ: `spawnInterval` khi cả đường cong lệch đều, `totalEnemies` khi wave đầu trận quá đông (ρ đổi ít hơn số con, vì T tỉ lệ với số con), `waveGrowth` khi đầu và cuối lệch ngược chiều, `mixRamp` khi riêng đầu trận lệch, `bossWaveMult` khi riêng đỉnh lệch |
| Đầu trận (thay mục tiêu ρ đầu trận, §5.7) | Ở các wave mô hình coi là chưa đủ trần, mỗi wave lọt < 2 con | Giảm tải đầu trận: `mixRamp` hoặc `waveGrowth` |
| Chạm trần ở wave mấy: bot đủ người so với bot chỉ đặt 3 người | Lệch ≤ 1 wave | Giảm vàng giết địch trước (D11) |
| Tương quan HP mất ↔ stress tăng cho mỗi lần đứng chốt **của melee**, gộp ≥ 3 trận (xạ thủ không mất HP, nên tính cả họ thì r đo tuyến đầu và tuyến sau; một trận chỉ có 3–5 lần đứng chốt melee) | r < 0,5 | Vai Fast / Tank chưa tách đủ, quay lại §5.5 |
| Ca bắt buộc: Bầy đàn + Kẻ gieo sợ vào Knight đứng một mình | Một bầy không đưa được từ Calm thẳng tới gãy (≤ 60) | Giảm `STRESS_PER_LEAK` hoặc `HERALD_LEAK_MULT`. Lượt 2: 10 → 7 và ×2 → ×1,5. Review cuối: Herald không khuếch đại thân Bầy, từ đỉnh Calm bầy cộng 52,5 |
| Bot thắng Hard | Lượt 1: 2/3 trận. Lượt 2: bot thua 0/3 nhưng cả 6/6 trận tới wave boss cuối. Thua vì không xoay ca: cả đội gãy ở wave 6–7, rồi bầy lọt nguyên đàn. User chấp nhận; tỉ lệ thắng thật do người chơi ở Hard quyết (§7.3) | Số mạng bầy lấy khi lọt, `STRESS_PER_LEAK`, bot biết xoay ca |
| Morale có xuất hiện không | Ở Hard, bot không xoay ca có ≥ 1 người gãy mỗi trận | Tăng tải hoặc giảm trần |

**Đo hai lượt:** sau mốc C, và sau mốc D (§8).

### 7.3 Người chơi (cổng cuối)

- **Không vô hình, không bất khả kháng:** ở Hard, người chơi biết xoay ca thì tránh được gãy, trong khi
  bot không xoay ca thì có người gãy.
- **A4:** người chơi có lúc rút một lính gần đầy máu vì stress đỏ.
- **Sức bot:** người chơi ở Hard giữ được tới các wave boss, đúng như mục tiêu ρ. Đây là điều kiện để giữ hệ số
  sức bot 0,58 (§5.7). Nếu người chơi cũng thua như bot, hệ số này sai và mọi độ khó đang khó gấp khoảng 1,7 lần.

---

## 8 · Mốc triển khai

| Mốc | Nội dung | Phụ thuộc |
|:--:|---|---|
| **A** | Trần (§5.1). Sự kiện lọt (§5.2). Đưa xạ thủ vào hệ (§5.3). Gỡ hệ cũ (§5.4). Validator morale | Task 1–3 của plan M1 |
| **B** | Chỉ số operator / turret / địch hiện có (§5.5–5.6). Melee đọc `attackType`. Moon nổ lan. Kiểm "không ai hơn ai" | A |
| **C** | Cấu trúc level × độ khó, `waveGrowth`, bảng độ khó (§5.7). Bảng ρ trong validator. Mật độ và thành phần theo tiến độ trận (D14) | B |
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
| Tăng tải chỉ bằng **cỡ wave** | Wave to ra lâu tương ứng, HP/giây không đổi. Bảng ρ đầu tiên đi ngang sau trần (D14) |
| Đo T **tính cả quãng nghỉ** giữa hai wave | Quãng nghỉ như nhau ở mọi wave, chỉ làm phẳng đường cong. Mô hình phải nói đúng điều game làm |
| Hai bảng tỉ lệ **đầu / cuối trận viết tay** | 30 con số phải giữ khớp với trung bình. Một hệ số `mixRamp` giữ trung bình đúng bằng cấu trúc |
| Tăng `hpMult` **theo wave** | Arknights không làm vậy trong một màn. Người chơi đọc được "loại địch mới", không đọc được "máu +8%" |

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
| Nhịp sinh địch | Dòng thời gian nhiều luồng chồng nhau, mỗi luồng một khoảng cách riêng. 4-4: lính thường 3s ở wave 1, 0,8–1s ở wave 2 | Wave rời nhau, khoảng cách thu theo trọng số wave, sàn 0,8s (D14) |
| Thành phần theo thời gian | Mở bằng địch yếu, loại mới và elite vào giữa và cuối | Địch nặng tăng dần theo `mixRamp` (D14). Boss ở wave boss |

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
- Dữ liệu stage (repo Kengxxiao/ArknightsGameData_YoStar): [1-7](https://raw.githubusercontent.com/Kengxxiao/ArknightsGameData_YoStar/main/en_US/gamedata/levels/obt/main/level_main_01-07.json), [4-4](https://raw.githubusercontent.com/Kengxxiao/ArknightsGameData_YoStar/main/en_US/gamedata/levels/obt/main/level_main_04-04.json)
