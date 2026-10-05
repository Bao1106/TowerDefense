# Morale System — Tài liệu thiết kế

> **Hệ tinh thần cho operator · phiên bản 1.0 · 05/09/2026**
>
> Cơ chế lõi mới cho Tower Defense. Mọi con số trong tài liệu này đều đã đối chiếu với
> dữ liệu thật trong project (`Level Config.asset`, `Enemy Data Config.asset`,
> `Melee Operator Config.asset`) — không có giá trị nào là giả định.
>
> Đọc kèm [`TOWER_DEFENSE_CODEX.md`](TOWER_DEFENSE_CODEX.md) cho kiến trúc hiện có.

| Chỉ số | Giá trị |
|---|---|
| Hằng số mới | 8 |
| Núm vặn khi cân bằng | 4 |
| Loại địch mới | 2 |
| Nguyên mẫu địa hình | 12 hình thái × 2 × 2 ≈ 30 tổ hợp |
| Sửa đổi bắt buộc trong code cũ | 4 |
| Chốt kiểm tra bắt buộc | 4 |
| Ước lượng công | **5 – 7 tuần** · bản cắt gọn ~4 tuần — [mục 15](#15--kế-hoạch-triển-khai) |

## Mục lục

1. [Vì sao hệ này tồn tại](#01--vì-sao-hệ-này-tồn-tại)
2. [Thang đo, trạng thái, icon](#02--thang-đo-trạng-thái-icon)
3. [Vùng áp lực và ngưỡng chịu đựng](#03--vùng-áp-lực-và-ngưỡng-chịu-đựng)
4. [Bốn nguồn tăng](#04--bốn-nguồn-tăng)
5. [Nguồn giảm và hồi phục](#05--nguồn-giảm-và-hồi-phục)
6. [Suy sụp · Rescue · Quyết tử](#06--suy-sụp--rescue--quyết-tử)
7. [Ý CHÍ — công thức suy ra](#07--ý-chí--công-thức-suy-ra)
8. [Hai loại địch mới](#08--hai-loại-địch-mới)
9. [Địa hình — 12 nguyên mẫu xương sống](#09--địa-hình--12-nguyên-mẫu-xương-sống)
10. [Bảng hằng số đầy đủ](#10--bảng-hằng-số-đầy-đủ)
11. [Bốn sửa đổi bắt buộc trong code hiện có](#11--bốn-sửa-đổi-bắt-buộc-trong-code-hiện-có)
12. [Điều kiện nghiệm thu](#12--điều-kiện-nghiệm-thu)
13. [Lộ trình](#13--lộ-trình)
14. [Nhật ký quyết định](#14--nhật-ký-quyết-định)
15. [Kế hoạch triển khai](#15--kế-hoạch-triển-khai)

---

## 01 · Vì sao hệ này tồn tại

> Vấn đề được chẩn đoán từ bản demo 105 giây: **trong toàn bộ ván chơi, không có một
> khoảnh khắc nào người chơi phải đối mặt với lựa chọn mà cả hai đường đều mất mát.**
> Vàng tăng đều, đặt lính, địch chết, thắng. Không có gì hỏng theo cách thú vị.

Tower defense, ở mọi phiên bản, là một **thể loại đặt để**: tích luỹ, đặt xuống, thứ
đặt xuống tồn tại vĩnh viễn. Hệ tinh thần bẻ gãy điều đó bằng một câu:

> ### Người lính có hạn sử dụng.

| Trước | Sau |
|---|---|
| Đặt xong là xong | Đặt xong là bắt đầu đếm ngược |
| Đường cong tích luỹ đi lên | Quản lý nguồn lực hao mòn |
| Retreat = nút undo không ai bấm | Retreat = **động từ chính, bấm liên tục** |
| Vàng là tài nguyên duy nhất | Thần kinh của lính là tài nguyên thứ hai — **không mua được bằng tiền** |

### Ba hệ thống có sẵn được cứu

| Đang có | Hiện đọc ra là | Dưới hệ tinh thần |
|---|---|---|
| Roster 8 slot random (Fisher–Yates lấy 8) | "Danh sách bừa" | Nhất quán: mọi thứ trong game này đều biến động |
| `TDOperatorRetreatControl` | Nút undo miễn phí | Kỹ năng cốt lõi, dùng mỗi wave |
| 20 tim / rating theo lives remaining | Con số giảm dần | Máu chiến dịch, mất sớm đau muộn |

### Bài kiểm tra sống–chết

> **"Nó có phải thanh máu thứ hai không?"**
>
> Nếu stress chỉ tăng khi bị đánh và đầy thì chết — nó là HP viết bằng chữ khác, và
> phải bỏ. Ba trục dưới đây là thứ giữ cho nó khác về chất:

| | HP | Stress |
|---|---|---|
| Tăng vì | Bị đánh | **Làm việc** — chặn, chứng kiến, bị vây, đối mặt boss |
| Hồi vì | Gần như không | **Được rút về hậu tuyến** |
| Cạn thì | Chết, biến mất | **Bỏ vị trí** — vẫn sống, nhưng thành gánh nặng |

Bằng chứng thực nghiệm cho tính độc lập: xem [tỉ lệ 10:1](#82--kiểm-chứng-bằng-số-thật)
ở mục Bầy đàn.

---

## 02 · Thang đo, trạng thái, icon

### Thang đo

| Trạng thái | Khoảng | Hệ số tăng tốc | Màu |
|---|:--:|:--:|---|
| **Calm** | 0 – 33 | **1,0×** | `#A1CD3A` |
| **Steady** | 34 – 66 | **1,5×** | `#FC7B01` |
| **Stressed** | 67 – 99 | **2,0×** | `#FB2425` |
| **Suy sụp** | 100 | — | — |

> **Hệ số CHỈ nhân vào nguồn dạng tốc độ (N1, N2, N4).**
> Không nhân vào cú giật tức thì (N3). Không nhân vào hồi phục — nếu không, người
> stress cao lại hồi chậm hơn, tức là bị phạt hai lần.

### Công thức thời gian

```
Thời gian gãy  =  72 / R          (R = tốc độ ròng, điểm/giây)

Phân đoạn:   Calm 33/R   ·   Steady 22/R   ·   Stressed 17/R
```

Đoạn **Stressed chỉ chiếm 24% tổng thời gian** dù nó là một phần ba thanh đo. Đó là
cảm giác "điểm không quay đầu", và nó trùng đúng khoảnh khắc icon đổi trạng thái.

> **VÌ SAO PHẢI TĂNG TỐC, KHÔNG TUYẾN TÍNH**
>
> Với đường thẳng, quyết định rút quân lúc nào cũng như nhau — người chơi rút theo
> thói quen, không theo tình huống. Với đường tăng tốc, 1/3 cuối trôi qua rất nhanh
> nên **do dự bị trừng phạt**.
>
> Và vì hệ số đổi đúng tại ranh giới trạng thái, **icon đổi hình = tín hiệu cho biết
> đồng hồ vừa chạy nhanh hơn**. Phản hồi thị giác và công thức toán là cùng một sự
> kiện — người chơi học được luật mà không cần đọc tutorial.

### Icon

Không dùng thanh bar ngang. Ba trạng thái rời rạc, hiển thị **cả trên model đã deploy
lẫn trên card ở deploy bar**.

> Deploy bar trở thành **bảng theo dõi tình trạng toàn đội** — người chơi liếc một chỗ
> duy nhất ở đáy màn hình thay vì quét 8 widget rải rác giữa lúc đang đánh nhau. Đây
> là lời giải cho rủi ro nặng nhất của hệ này: quá tải nhận thức.

**Cấu trúc:** một background tròn tô trắng + một icon đè lên. Ba ảnh nằm ở
`Assets/1.Assets/Resources/Sprites/Morale/` (đã chuyển vào `Resources` để load lúc chạy).

**Vị trí:** offset **world** `(+0.465, +1.125, −0.855)` so với gốc operator — đặt tay trong
Scene view rồi đọc ngược ra khỏi transform. Trên màn hình (camera pitch 30°) rơi vào **cạnh
đầu, lệch phải**, cách HP bar ~1,07 đơn vị. Không đặt chính giữa trên đầu vì panel rút quân mở
ra đúng chỗ đó — thứ báo *có nên rút* sẽ bị che bởi thứ dùng để *rút*. Thành phần `z` âm kéo
icon ra trước model, tránh chìm vào nhân vật. Offset là **world chứ không phải local**: operator
xoay theo mục tiêu, local sẽ làm icon quay vòng quanh nó.

> **Hai cái bẫy cùng một loại đã dính ở đây.** `Canvas` dựng bằng code mặc định
> `sortingOrder = 0` trong khi `HPBar_Operator.prefab` để **5** — icon được vẽ ra nhưng không
> bao giờ nhìn thấy. Và cả 8 prefab operator có root scale **1.5**, khiến `WORLD_SIZE` render
> lớn hơn 50% so với con số nó khai báo.
>
> **UI dựng bằng code không thừa hưởng gì từ ngữ cảnh prefab** — và cả hai lỗi đều im lặng,
> không có exception nào để mà đọc.

**Trọng lượng thị giác theo band.** Đo tại 1080p (camera ortho, pitch 40°, size 10 → **54 px /
đơn vị world**): icon full size rộng **69 px** so với operator **~76 px** — gần bằng chính nhân
vật nó mô tả. Hai icon ở hai ô kề nhau (cách 108 px tâm) chỉ hở **39 px**, nên chúng đọc thành
*một cụm hai mắt* thay vì hai chỉ báo riêng.

Vì Calm là trạng thái phổ biến nhất, để full size đồng nghĩa phần lớn trận đấu icon đang hét
lên "không có gì" — to hơn cả đám địch mà nó sinh ra để cảnh báo. Nên tỉ lệ theo band:

| Band | scale | alpha | px @1080p | hở giữa 2 ô kề |
|---|---|---|---|---|
| Calm | 0,55 | 0,45 | 38 | **70 px** |
| Steady | 0,80 | 1,00 | 55 | 53 px |
| Stressed · Suy sụp | 1,00 | 1,00 | 69 | 39 px |

Không ẩn hẳn Calm: hoạt ảnh rút dần **trong** Calm chính là cảnh báo sớm cho người chơi hành
động *trước khi* vào Stressed — cửa sổ phản ứng rộng rãi duy nhất còn lại. Ẩn đi là vứt nó.

> Bậc thang này trả luôn phần **"một nhịp phóng to"** mà mục này yêu cầu khi vào Stressed:
> icon *lớn dần vào* báo động, không cần tween one-shot riêng.

**Hành vi:**

| Giai đoạn | Biểu hiện |
|---|---|
| Vào trận | Icon **Calm** xanh, đầy |
| Tích stress trong Calm | Lớp phủ trắng **rút dần từ trên xuống** |
| Chạm 0% của đoạn | Hiệu ứng **poof** kèm âm thanh → chuyển sang icon **Steady** |
| Tích stress trong Steady | Lặp lại cơ chế rút — poof — chuyển sang **Stressed** |
| Trong **Stressed** | **Khác hẳn**: tô màu vào chính background tròn trắng, sắc **đậm hơn** màu icon Stressed. Kèm **nhấp nháy** |
| Suy sụp | Xem [mục 06](#06--suy-sụp--rescue--quyết-tử) |

> ⚠️ **RÀNG BUỘC BẮT BUỘC — ĐỌC ĐƯỢC BẰNG HÌNH DÁNG, KHÔNG CHỈ BẰNG MÀU**
>
> Màn hình 6 inch, ngoài nắng, giữa 15 con địch và VFX nổ khắp nơi — màu là thứ mất
> đầu tiên. Ba trạng thái phải phân biệt được qua **silhouette**: chuyển ảnh sang đen
> trắng mà vẫn nhận ra được thì mới đạt. Đây cũng là điều kiện tối thiểu cho người mù
> màu (~8% nam giới).

> ⚠️ **ÂM THANH LÀ BẮT BUỘC, KHÔNG PHẢI TRANG TRÍ**
>
> Ở các tình huống bị vây nặng, đoạn Stressed chỉ dài **2,4 – 5,5 giây** (xem
> [bảng tham chiếu](#bảng-thời-gian-tham-chiếu)). Nếu icon là tín hiệu duy nhất thì
> không thể phản ứng kịp. Bắt buộc: **âm thanh riêng + một nhịp phóng to** ngay
> khoảnh khắc vào Stressed.

---

## 03 · Vùng áp lực và ngưỡng chịu đựng

Định nghĩa này phải chốt trước mọi con số, và nó **khác nhau theo `deployZone`**.

| | Vùng áp lực | Ngưỡng chịu đựng |
|---|---|---|
| **Melee** (`PathCell`) | Số địch trong **khối 3×3** quanh chỗ đứng, hợp với phần **tầm đánh** vươn ra ngoài khối đó | **1 + blockCount** |
| **Ranged** (`TowerZone`) | Số địch trong **1 ô kề** vị trí đứng | **1** |

> **VÌ SAO MELEE LÀ 3×3, KHÔNG PHẢI TẦM ĐÁNH** *(sửa sau khi đo — [1.6](#16--ba-số--dụng-cụ-đo))*
>
> Bản đầu ghi "số địch trong tầm đánh". Đo thật: **N1 kích hoạt 0% trong 110 giây giao
> chiến**, và không phải vì xui:
>
> ```
> rangeOffsets của melee mặc định = {(0,0)} = đúng ô đang đứng
>     ↓  chỉ địch BỊ CHẶN mới đứng lại ô đó
> n ≤ blockCount        mà ngưỡng = 1 + blockCount
>     ↓
> n − ngưỡng ≤ −1 < 0   ⇒ N1 = 0, VĨNH VIỄN
> ```
>
> N1 được ghi là **nguồn chính** ở [mục 04](#04--bốn-nguồn-tăng) nhưng theo định nghĩa cũ
> nó là dòng code chết — chỉnh `STRESS_PER_OVERLOAD` kiểu gì cũng không thấy khác.
>
> ⚠️ **Không có "hàng địch dồn ứ" — game này không xếp hàng.** `TDEnemyView` kiểm `CanBlock`
> khi tới ô; block đầy thì nó **đi tiếp**, không đứng chờ. Nên số địch *bị chặn* không bao
> giờ vượt `blockCount`, và ngưỡng `1 + blockCount` sẽ không bao giờ chạm tới nếu chỉ đếm
> địch bị chặn.
>
> Cái làm ngưỡng chạm được là **địch đang đi qua**: vùng 3×3 có 9 ô, ai băng qua đó đều
> được đếm ở thời điểm lấy mẫu. Bằng chứng: đỉnh **6** đo trên một **Striker `blockCount 1`**
> — nếu chỉ đếm địch bị chặn thì trần là 1.
>
> Vì vậy N1 **không** đo "quá tải khả năng chặn" mà đo **mật độ dòng địch chảy qua chỗ
> đứng**. Và vì địch không bị giữ thì đi tiếp, mọi con trong vùng mà mình không ghim được
> chính là con **đang lọt qua**:
>
> | | Nghĩa |
> |---|---|
> | `blockCount` con | giữ lại được — việc thường ngày |
> | `+1` | một con lọt qua, chấp nhận được |
> | vượt ngưỡng | **địch chảy qua nhanh hơn mình chặn nổi — tuyến đang rò** |
>
> Ý nghĩa này mạnh hơn "bị vây" theo nghĩa đen: nó buộc stress vào **một thất bại đang thật
> sự diễn ra**, và người chơi **nhìn thấy được** — địch trôi qua chân người lính của mình.
>
> Phần hợp với `rangeOffsets` giữ lại cho tương lai: hôm nay không ai vươn quá 3×3, nhưng
> thêm một melee tầm 2 ô mà vùng áp lực không theo thì lỗi sẽ im lặng y như lần này.

> **VÌ SAO RANGED TÍNH THEO Ô KỀ, KHÔNG THEO TẦM ĐÁNH**
>
> Ginger có 9 ô tầm, Moon có 8. Nếu tính theo tầm thì chúng **luôn** "bị vây" và luôn
> stress, dù đang đứng an toàn trên tường bắn xuống. Xạ thủ chỉ căng khi **có thứ gì
> đó đến gần**.
>
> Hệ quả: ranged gần như không stress khi tuyến phòng thủ còn đứng, và stress rất
> nhanh khi tuyến vỡ. Khớp hoàn hảo với Ý chí thấp mà [công thức mục 07](#07--ý-chí--công-thức-suy-ra)
> tính ra cho chúng.

**Ngưỡng theo roster hiện tại:**

| Operator | blockCount | Ngưỡng |
|---|:--:|:--:|
| Tart, Defender | 3 | **4** địch trong tầm |
| Knight | 2 | **3** |
| Striker, Ace, Layla | 1 | **2** |
| Ginger, Moon | 0 | **1** địch ở ô kề |

---

## 04 · Bốn nguồn tăng

| # | Nguồn | Giá trị | Điều kiện | ×hệ số |
|:--:|---|:--:|---|:--:|
| **N2** | Thời gian giao chiến | **+1,60 /giây** | ≥1 địch trong vùng áp lực | ✅ |
| **N1** | Tuyến rò | **+1,00 /giây × (số địch − ngưỡng)** | Chỉ tính phần **vượt** ngưỡng | ✅ |
| **N3** | Đồng đội **chết** trong bán kính 2 ô | **+20** tức thì | Một lần mỗi sự kiện | ❌ |
| **N3** | Đồng đội **suy sụp** trong bán kính 2 ô | **+30** tức thì | Một lần mỗi sự kiện | ❌ |
| **N4** | Aura **Boss** (bán kính 3 ô) | **+2,00 /giây** | Trong vùng | ✅ |
| **N4** | Aura **Herald** (bán kính 4 ô) | **+1,50 /giây** | Trong vùng | ✅ |

### Ghi chú thiết kế

**N2 là đồng hồ nền, và theo số đo cũng là nguồn lớn nhất** (~96% tổng điểm một ván).
Bằng 0 khi không có địch trong vùng áp lực — nên đứng gác chỗ vắng là miễn phí.

**N1 là đỉnh, không phải khối lượng.** Bản đầu ghi *"N1 là nguồn chính"*; đo thật thì nó
đóng 3,7%. Giá trị của nó nằm ở **thời điểm** chứ không ở tổng: nó dồn đúng vào lúc tuyến
sắp vỡ, do người chơi chọn vị trí gây ra, nhìn thấy được bằng mắt (đám đông), và **hoàn
toàn độc lập với HP** — một Defender không mất giọt máu nào vẫn gãy vì bị vây. Nhưng để
cái đỉnh đó *cảm nhận được*, hệ số phải lớn hơn 0,5 — xem [mục 10](#10--bảng-hằng-số-đầy-đủ).

**N3 tạo sụp đổ dây chuyền.** Tuyến phòng thủ *sập*, không *mòn dần*. Và nó làm
khoảng cách giữa lính có ý nghĩa lần đầu tiên.

> **VÌ SAO GIẬT DO SUY SỤP (+30) NẶNG HƠN DO CHẾT (+20)**
>
> Cái chết là dứt điểm, chấp nhận được. Người đồng đội gãy vẫn còn đó — sống, vô dụng,
> không rút ra được. Chứng kiến điều đó nặng hơn.

> **VÌ SAO N3 KHÔNG NHÂN HỆ SỐ**
>
> Cú giật 30 điểm ở trạng thái Stressed mà nhân 2 thành 60 thì gần như luôn giết ngay
> lập tức → dây chuyền trở thành xoá sổ tự động, không còn chỗ cứu vãn.

**N4 biến boss từ cục HP thành vũ khí chiếm không gian.** Boss đi tới đâu, tuyến rạn
tới đó. Aura phải có **decal vòng tròn trên mặt đất** — không nhìn thấy thì nó là hộp
đen.

---

## 05 · Nguồn giảm và hồi phục

| Nguồn | Giá trị | Ghi chú |
|---|:--:|---|
| Đồng đội **Calm** ô kề | **−0,40 /giây mỗi người, trần −0,80** | Chỉ trừ vào N1+N2. **Không** trừ được N3, N4 |
| Không có địch trong vùng áp lực | **−0,60 /giây** | Lý do khoảng lặng giữa wave có giá trị |
| **Hết wave** | **−15** tức thì | Mọi operator **đang deploy và còn đứng vững**. Người đang suy sụp **không** được |
| **Retreat chủ động** | **−70**, countdown **8 giây** | Xem dưới |
| **Rescue** | **−50** + gỡ trạng thái suy sụp | Tốn 50 SP |
| Suy sụp, không bị đánh ≥5 giây | **−1,00 /giây** | 100 → 66 mất **34 giây** yên ổn |
| **Chết** | **0** | Bất di bất dịch |

> **Trần hồi phục tổng: −0,80 /giây.** Nếu không, một cụm 3 operator Calm đứng chỗ
> vắng sẽ hồi quá nhanh và vòng lặp lõi biến mất.

> 🔴 **"Wave sạch — không ai gãy" là bản cũ, và nó mâu thuẫn với chính [mục 06](#chết-vĩnh-viễn).**
>
> Mục này gọi −15 là **phần thưởng cho wave sạch**; mục 06 gọi đúng con số đó là **"van chống
> chết chùm"**. Code làm theo mục này, nên van ấy **chưa từng tồn tại**: một người gãy là cả đội
> mất thưởng, tức nó **đóng chặt đúng lúc cascade xảy ra** — cascade chính là thứ nó được đặt tên
> để chặn. Một cái van đóng lại khi có áp lực thì không phải van.
>
> Bộ lọc chuyển từ *"có ai gãy không"* sang *"BẠN có đang gãy không"*. Phần thưởng rơi vào đúng
> người đã trụ được, và nó chặn dây chuyền ở **mắt xích thứ ba** thay vì để chạy hết đội. Người
> đang suy sụp vẫn trả đủ giá — hết wave **không** gỡ được suy sụp, chỉ Rescue hoặc 39 giây yên
> tĩnh mới gỡ.
>
> Phát hiện khi người chơi báo: *"2 operator kề nhau đều không giữ được quái → cả 2 suy sụp →
> đặt operator thứ 3 ra cứu là không kịp."* Đo lại thì **cả ba** cơ chế cứu trợ đều tắt đúng lúc
> cần: đồng đội Calm kề bị `NetRate` bỏ qua ở nhánh `IsBroken`, van wave đóng vì cascade, và
> Rescue đòi 50 SP mà người mới cắm có 0. Sửa cái thứ hai vì nó là thứ **đã hứa mà chưa giao**.

### Vì sao Retreat là −70, không phải về 0

| Rút lúc | Về lại ở mức | Ý nghĩa |
|---|:--:|---|
| Calm (30) | **0** | Sạch hoàn toàn |
| Steady (60) | **0** | Sạch hoàn toàn |
| Stressed (70) | **0** | Vẫn vừa đủ sạch |
| Stressed muộn (90) | **20** | **Không bao giờ sạch lại được nữa** |

> ### Rút sớm là RESET. Rút muộn là HOÃN.
>
> Một luật, một con số, dạy đúng kỹ năng cần dạy mà không tốn một dòng tutorial. Đồng
> thời [thang bậc sự nghiệp](#thang-bậc-sự-nghiệp) vẫn leo: operator bị ép tới sát
> ngưỡng không bao giờ trở lại nguyên vẹn.

### Thang bậc sự nghiệp

Hai luật ghép lại tạo ra thứ chưa TD nào có: **mỗi operator có một sự nghiệp trong màn
chơi.**

```
Deploy → căng → rút về hồi → deploy lại (stress cao hơn lần trước)
       → chết ở stress thấp → hồi sinh, MANG THEO stress cũ
       → stress leo dần qua mỗi vòng
       → chạm trần → lần chết tiếp theo là VĨNH VIỄN (hết màn)
```

| Hệ quả | |
|---|---|
| **Không cheese được** | Không thể "để nó chết cho sạch stress rồi gọi lại" |
| **Tội của người chơi là ép quá tay, không phải để lính chết** | Chết ở stress thấp = tai nạn. Chết ở stress đầy = đã được cảnh báo và phớt lờ |
| **Đường suy giảm mượt, không có vách đá** | Con lính bị dùng nhiều nhất tự nhiên là con chết vĩnh viễn đầu tiên — nó kể đúng một câu chuyện |

---

## 06 · Suy sụp · Rescue · Quyết tử

### Khi stress chạm 100

Một trong hai, quyết định bởi [Ý chí](#07--ý-chí--công-thức-suy-ra):

| | **SUY SỤP** (mặc định) | **QUYẾT TỬ** (nút hiện theo xác suất = Ý chí) |
|---|---|---|
| Kích hoạt | Tự động | **Người chơi tự bấm** |
| Hiệu ứng | Không chặn, không đánh, **không tự rút được**. Nhận **sát thương ×3** | 1 wave sức mạnh áp đảo |
| Kết cục | Cần [Rescue](#rescue), hoặc hồi rất chậm tại chỗ | **Chết vĩnh viễn** sau wave đó |

> **VÌ SAO QUYẾT TỬ PHẢI DO NGƯỜI CHƠI BẤM**
>
> Nếu nó tự kích hoạt ngẫu nhiên thì đó chỉ là "một cách thua khác, có nhạc hay hơn".
> Khi người chơi tự bấm, họ **tự tay ký giấy khai tử** — và họ sẽ nhớ mãi lần bấm ấy.
> Đó là narrative sinh ra từ lựa chọn, không phải từ kịch bản.

> **VÌ SAO SÁT THƯƠNG ×3 THAY VÌ "XÁC SUẤT CHẾT NGAY"**
>
> Toàn bộ độ tin cậy của hệ này dựa trên hợp đồng ngầm: *nếu bạn thua, đó là lỗi của
> bạn.* Một cú tung xúc xắc gây chết tức thì xé hợp đồng đó — người chơi mất người
> vĩnh viễn vì random sẽ nói "game này xui", và một khi họ nói câu đó một lần, họ sẽ
> nói mãi.
>
> Sát thương ×3 cho **kết quả gần như y hệt** (chết rất nhanh khi bị đánh) nhưng
> **tính được**: nhìn máu là biết còn mấy giây. Không bao giờ cảm thấy tuỳ tiện.

### Rescue

> Operator suy sụp không tự rút được. Nhưng một operator khác **còn tỉnh táo, đứng ô
> liền kề**, có thể kéo nó ra.

| Thuộc tính | Giá trị |
|---|---|
| **Chi phí** | **50 SP** (kỹ năng thường = 100 SP) |
| **Tốc độ hồi SP** | **+1 SP/giây**, **chỉ khi đang deploy** — quy tắc Arknights |
| **Cái giá của người cứu** | Rời vị trí vài giây + **nhận một cục stress** |
| **Trần an toàn** | Stress từ hành động cứu **không bao giờ đẩy người cứu vượt 90%** |
| **Hiệu quả** | Người được cứu **−50 stress** + gỡ suy sụp → về Steady |
| **Cooldown** | Theo chu kỳ hồi SP (~50 giây/người) |

**Luồng thao tác:**

```
Click vào operator tỉnh táo đứng cạnh người suy sụp
   → panel diamond hiện ra, có 2 nút:  Retreat (góc trên)  ·  Rescue (góc dưới)
   → bấm Rescue
       ├─ đúng 1 mục tiêu hợp lệ  → cứu luôn, không có bước chọn
       └─ ≥2 mục tiêu             → các mục tiêu nhấp nháy sáng tối, click chọn
```

> **KHÔNG PHẢI MODE THỨ BA CỦA `TDDiamondPanelView`**
>
> Diamond có 4 góc; mode retreat hiện chỉ dùng 1. Rescue là **nút thứ hai trên cùng
> panel**, không thêm state nào vào state machine, không tăng bề mặt bug "diamond ma",
> không thêm prefab.

**Ba luật hiển thị nút Rescue:**

| Tình huống | Xử lý | Vì sao |
|---|---|---|
| Không có ai suy sụp đứng cạnh | **Ẩn hẳn** | Tình huống không tồn tại. Nút mờ vĩnh viễn chỉ dạy người chơi bỏ qua nó |
| Có mục tiêu, nhưng **thiếu SP** hoặc **đang cooldown** | **Hiện, làm mờ, ghi rõ lý do** (`45/50 SP` hoặc đồng hồ) | Người chơi cần biết lựa chọn tồn tại và tại sao chưa dùng được |
| Đủ điều kiện | Hiện, sáng | |

> **Vị trí cố định tuyệt đối: Retreat góc trên, Rescue góc dưới.** Đối lập theo trục
> dọc = khoảng cách chạm xa nhất → chống bấm nhầm. Trong game thời gian thực người
> chơi bấm bằng trí nhớ cơ bắp; một lần nút đổi chỗ là một lần rút nhầm người đáng lẽ
> phải cứu.

### Vì sao Rescue tồn tại

Không có nó, chuỗi suy sụp là: không rút được → không đánh → có thể bị giết → hồi rất
chậm. **Trong toàn bộ chuỗi đó người chơi không có hành động nào để thực hiện** — đó
không phải quyết định, đó là một đoạn phim phạt.

| Không có Rescue | Có Rescue |
|---|---|
| "Rook gãy rồi. Ngồi xem nó chết." | **"Rook gãy rồi. Kéo Knight khỏi cổng bắc để lôi nó ra — nhưng cổng bắc hở 6 giây, và Knight sẽ dính stress. Hay bỏ Rook luôn?"** |
| Bị phạt | **Lưỡng nan hay nhất trong cả game** |

Và về narrative: người chơi **bỏ rơi một người lính, hoặc hy sinh vị trí để cứu**.
Không cần viết một dòng thoại nào.

### Chết vĩnh viễn

> **Mọi operator chết trong lúc stress đầy → mất hẳn cho tới hết màn.**

Roster hữu hạn mỗi màn. Van chống chết chùm: [thưởng wave sạch](#05--nguồn-giảm-và-hồi-phục)
(−15 cho toàn đội đang deploy) — thưởng cho lối chơi sạch và cho người chơi giỏi một
đường quay lại.

**Lối thoát cuối:** nút **RÚT KHỎI CHIẾN DỊCH** → hộp thoại 2 lựa chọn: **CHƠI LẠI**
(nạp lại màn) hoặc **VỀ MENU**. Cả hai đi qua `TDSceneController`, và **phải gọi
`EnsureUnpaused()` trước** — Codex đã ghi rõ vì sao (popup pause `timeScale = 0` treo
cả chuỗi fade).

---

## 07 · Ý CHÍ — công thức suy ra

Ý chí = xác suất nút **QUYẾT TỬ** hiện ra thay vì suy sụp thẳng.

### Nguyên tắc: suy ra, không gán tay

> Ý chí gán tay = một ngày nào đó có người đặt Defender ý chí 10%.
> Ý chí suy ra = **trạng thái sai không tồn tại được.**

Cùng nguyên tắc `deployZone` đang dùng trong `OperatorData`.

### Mâu thuẫn biểu kiến, và cách nó tự tan

Ý chí phải **vuông góc với sức mạnh** — nếu không nó chỉ là "đắt thì tốt hơn" viết
bằng chữ khác. Nhưng nếu tính từ chỉ số thì làm sao vuông góc với chỉ số?

> **Tính từ TỈ LỆ (hình dạng vai trò), không từ giá trị tuyệt đối (cấp độ sức mạnh).**
>
> Tính vuông góc **tự rơi ra**: một Defender rẻ vẫn ra ý chí cao (tỉ lệ nghiêng về
> chịu đựng), một Striker đắt vẫn ra ý chí thấp (tỉ lệ nghiêng về sát thương).

### Công thức

```
GÁNH VÁC    =  hp × (1 + blockCount)
SÁT THƯƠNG  =  damage × attackSpeed × rangeOffsets.Length

TỈ LỆ CHỊU ĐỰNG   E  =  GÁNH VÁC / SÁT THƯƠNG

Ý CHÍ GỐC = clamp( 10 + 35 × (log10(E) − E_LO) / (E_HI − E_LO), 10, 45 )

     E_LO = -0.5     E_HI = 3.0
```

- **`log10`** vì E trải từ 0,86 tới 667 — chênh gần 800 lần. Ánh xạ tuyến tính sẽ dồn
  4/5 roster vào cùng một giá trị.
- **`(1 + blockCount)`** để Ranger/Mage (block 0) không triệt tiêu tử số.
- **Chuẩn hoá tuyệt đối, không tương đối.** Kiến thức người chơi phải ổn định — nếu
  thêm một operator ở bản sau mà Ý chí của Tart âm thầm tụt, họ sẽ cảm thấy bị lừa mà
  không chỉ ra được chỗ nào.
- **Guard:** `damage` hoặc `attackSpeed` = 0 → kẹp mẫu số ở epsilon. `rangeOffsets`
  null → dùng lại fallback sẵn có trong `IDeployableDTO.RangeOffsets` (độ dài 1).

### Kết quả với roster thật

| Operator | cost | hp | block | dmg | aspd | ô tầm | E | **Ý CHÍ** |
|---|--:|--:|--:|--:|--:|--:|--:|--:|
| **Tart** | 22 | 4000 | 3 | 15 | 0,8 | 2 | 666,7 | **43%** |
| **Defender** | 20 | 3000 | 3 | 20 | 1,0 | 2 | 300,0 | **40%** |
| **Knight** | 15 | 1000 | 2 | 50 | 2,0 | 2 | 15,0 | **27%** |
| **Layla** | 18 | 750 | 1 | 75 | 1,5 | 3 | 4,44 | **22%** |
| **Ace** | 18 | 800 | 1 | 70 | 1,8 | 3 | 4,23 | **21%** |
| **Striker** | 18 | 700 | 1 | 80 | 1,5 | 3 | 3,89 | **21%** |
| **Moon** | 20 | 700 | 0 | 80 | 1,0 | 8 | 1,09 | **15%** |
| **Ginger** | 16 | 600 | 0 | 65 | 1,2 | 9 | 0,86 | **14%** |

Trải đều 14–43%, không con nào chạm trần, thứ tự đúng trực giác.

### Ba điều chỉnh trong trận

```
Ý chí thực tế = clamp( Ý_chí_gốc + Σ(điều chỉnh), 5%, 60% )
```

| Điều kiện | Ý chí | Dùng lại quy tắc nào |
|---|:--:|---|
| Có đồng đội **Calm** ô liền kề | **+10%** | Cùng quy tắc với hồi stress từ đồng đội bình tĩnh |
| Có đồng đội **gãy** gần đó trong wave này | **−15%** | Cùng quy tắc với N3 (lây lan) |
| **Mỗi lần** operator này đã gãy/chết trong màn | **−10%** | Cùng quy tắc với thang bậc sự nghiệp |

Hai tầng tách bạch: **bản chất con người (suy ra) + hoàn cảnh (cộng vào)**. Và hai
điều kiện đầu cho người chơi quyền tác động — **may mắn trở thành thứ mua được bằng
lối chơi tốt.**

### Hiển thị

| Nơi | Hiện? |
|---|:--:|
| Bảng thông tin operator | ✅ Con số đầy đủ |
| Card trên deploy bar | ✅ Dạng gọn (▲ / ■ / ▼) |
| **Ngay khoảnh khắc stress đầy** | ❌ **KHÔNG** |

> Hiện *"Ý CHÍ: 28%"* ngay trước khi tung xúc xắc là biến khoảnh khắc kịch tính thành
> một ván cược — người chơi sẽ đếm số lần trượt và cảm thấy bị lừa. Ý chí là **thông
> tin để lên kế hoạch**, không phải thông tin lúc lâm trận. Đến lúc đó thì hoặc nút
> hiện ra (bất ngờ), hoặc không (đúng như dự đoán).

### Công cụ kiểm tra

Một cửa sổ Editor liệt kê toàn roster: `cost` · `E` · `Ý chí`, **cảnh báo nếu hệ số
tương quan giữa cost và Ý chí vượt ~0,5**. Nếu roster tình cờ toàn "đắt = trâu", công
thức sẽ ra "đắt = ý chí cao" → tính vuông góc mất → Ý chí thành thừa. Tool bắt được
điều đó trong 2 giây thay vì sau 3 tuần playtest.

> **PHÁT HIỆN TỪ ROSTER HIỆN TẠI**
>
> Striker (21%) · Ace (21%) · Layla (22%) — ba nhân vật, cùng chỉ số, cùng Ý chí.
> Người chơi không có lý do gì để chọn con này thay con kia. Không phải lỗi công thức
> — nó chỉ **soi ra** một vấn đề roster đã có sẵn. Nhưng giờ đã có một trục mới để
> tách chúng: **Ý chí cho phép phân hoá nhân vật mà không đụng tới sức mạnh.**
>
> *(Ngoài lề: Knight — 15 vàng, 1000 HP, 100 DPS, block 2 — mạnh hơn hẳn Striker 18
> vàng, 700 HP, 120 DPS, block 1. Có vẻ lệch cân bằng, đáng xem lại khi tune.)*

---

## 08 · Hai loại địch mới

### 8.1 · Chỉ số

| | Normal | Fast | Tank | Boss | 🆕 **BẦY ĐÀN** | 🆕 **KẺ GIEO SỢ** |
|---|--:|--:|--:|--:|--:|--:|
| `type` | 0 | 1 | 2 | 3 | **4** `Horde` | **5** `Herald` |
| `baseHP` | 300 | 150 | 900 | 3000 | **70** | **500** |
| `baseSpeed` | 3,0 | 6,0 | 1,5 | 1,0 | **4,0** | **2,0** |
| `dieDuration` | 0 | 0,63 | 2,0 | 1,33 | **0,25** | **1,0** |
| `goldReward` | 2 | 4 | 8 | 10 | **1** | **12** |
| `baseAttackDamage` | 10 | 5 | 25 | 50 | **2** | **0** |
| `baseAttackSpeed` | 1,0 | 1,5 | 0,5 | 0,75 | **1,0** | **0** |
| **DPS** | 10 | 7,5 | 12,5 | 37,5 | **2** | **0** |
| 🆕 `fearAuraRadius` | 0 | 0 | 0 | **3** | 0 | **4** |
| 🆕 `fearAuraRate` | 0 | 0 | 0 | **2,0** | 0 | **1,5** |

**Hai field mới** (`fearAuraRadius`, `fearAuraRate`) phục vụ **cả Boss lẫn Herald** →
aura boss ở N4 không còn hardcode, designer chỉnh được.

**Herald dùng `baseAttackSpeed = 0`** — comment trong code đã ghi sẵn
`0 = does not attack`. Không cần cơ chế mới, chỉ cần điền 0.

**Bầy đàn là 2 sát thương, không phải 0** — nếu đúng 0 thì người chơi học cách bỏ qua
nó hoàn toàn và thanh máu đứng im khiến họ tưởng game lỗi. 2 điểm giữ cho thanh máu
nhúc nhích. Herald thì ngược lại: nó **phải** là 0, vì bài học của nó phụ thuộc vào
sự tuyệt đối đó.

### 8.2 · Kiểm chứng bằng số thật

**Defender** (3000 HP · 20 dmg · 1,0 aspd · block 3 · `AttackType.Multiple`) — ngưỡng = 4

| 15 Bầy đàn vây một Defender | |
|---|---|
| **Gãy tinh thần** | vượt +11 → R = 1,6 + 5,5 = 7,1 → `72/7,1` = **10 giây** |
| **Chết vì máu** | 15 × 2 DPS = 30 → `3000/30` = **100 giây** |

> # Tỉ lệ 10 : 1
>
> Con **trâu nhất bản đồ** gục vì tinh thần **nhanh gấp 10 lần** so với vì máu.
> Không cần tutorial — người chơi thấy một bầy sinh vật vô hại xoá sổ tuyến phòng thủ
> của mình, và họ hiểu ngay stress là một trục hoàn toàn khác.

**Ngưỡng nguy hiểm của Bầy đàn:**

| Số con cùng lúc | R | Gãy sau | Defender dọn kịp? |
|--:|--:|--:|---|
| 5 (1 pack) | 2,1 | 34 giây | ✅ ~6 giây |
| 8 | 3,6 | 20 giây | ✅ ~11 giây |
| **12** | 5,6 | **13 giây** | ⚠️ sát nút |
| **15** (3 pack) | 7,1 | **10 giây** | ❌ **gãy** |

**Knight vs Herald** (1000 HP · 100 DPS · block 2 · `AttackType.Single`) — ngưỡng = 3

| | |
|---|---|
| Giết Herald | `500/100` = **5 giây** nếu tập trung hoả lực — hoàn toàn giết được |
| Nếu chặn nó + 2 con khác | ở ngưỡng → R = 1,6 + 1,5 = 3,1 → gãy sau **23 giây** |
| Hai Herald chồng aura | R = 4,6 → **16 giây** |

> **CÁI BẪY NGƯỜI CHƠI PHẢI HỌC:** Herald **chặn được**. Và chặn nó chính là thứ giết
> người chặn. Bài học đúng là **giết nó từ xa, đừng ôm nó.**
>
> `goldReward = 12` — cao hơn cả Tank (8) lẫn Boss (10). Đó là tín hiệu kinh tế nói
> thẳng: **ưu tiên diệt con này.**

### 8.3 · Bảng tỉ lệ độ khó — bản 6 cột

| Độ khó | Normal | Fast | Tank | **Horde** | **Herald** | Boss | hpMult | spdMult |
|---|--:|--:|--:|--:|--:|--:|--:|--:|
| Easy | 68% | 13% | 4% | **12%** | **2%** | 1% | 0,8 | 0,9 |
| Normal | 44% | 19% | 10% | **18%** | **6%** | 3% | 1,0 | 1,0 |
| Hard | 30% | 19% | 14% | **21%** | **9%** | 7% | 1,2 | 1,1 |
| Extreme | 17% | 17% | 17% | **23%** | **11%** | 15% | 1,5 | 1,2 |
| Nightmare | 8% | 12% | 22% | **20%** | **13%** | 25% | 2,0 | 1,5 |

Đường cong đọc ra được: **Normal sụp 68→8, Herald leo 2→13.** *"Đầu game là bài toán
về máu, cuối game là bài toán về áp lực."*

### 8.4 · Kinh tế

`goldReward = 1` cho Horde. Ở Hard: ~8 suất × 5 = 40 con = **+40 vàng mỗi trận**.

Nghe nhiều, nhưng đó là chủ đích: hệ tinh thần tạo ra một khoản chi mới — **xoay vòng
quân tốn tiền deploy lại**. Bầy đàn là nguồn thu tài trợ cho khoản chi đó. Vòng kinh
tế khép kín: *loại địch tạo ra áp lực cũng là loại địch trả tiền để chống lại áp lực.*

---

## 09 · Địa hình — 12 nguyên mẫu xương sống

### 9.1 · Vì sao địa hình phải đổi

> **Địa hình song song KHÔNG tương thích với hao mòn.**

Generator hiện tại tạo N corridor **độc lập, song song**. Với hệ tinh thần đó là cấu
trúc tệ nhất có thể:

| 3 corridor song song | Khi mất 1 lính |
|---|---|
| Mỗi corridor cần ≥1 người giữ | **1/3 bản đồ bỏ trống ngay lập tức** |
| Không thể dồn quân | Không có cách nào bù đắp |
| | **Mất 1 lính = thua ngay.** Vách đá, không phải dốc |

Trò chơi hao mòn cần **đường suy giảm mượt** — cách để **đổi không gian lấy sinh
mạng**:

> **Rút lui có tổ chức.** Mất người → bỏ tuyến ngoài → lùi về nút thắt trong. Địch vào
> sâu hơn, nhưng bạn vẫn giữ được bằng ít quân hơn.
>
> Không có nó, hệ tinh thần sẽ tạo ra những ván thua kiểu *"chết ở phút 2, ngồi xem
> tới phút 5"* — trải nghiệm tệ nhất có thể trên mobile.

### 9.2 · Kiến trúc: một thuật toán, N file dữ liệu

Nguyên mẫu **không phải bản đồ** — nó là **đồ thị**: nút (cổng, nút thắt, đích) + cạnh
+ tầng. Mỗi cái ≈ 10–20 dòng dữ liệu.

```
1. Chọn nguyên mẫu (theo seed / theo stage)
2. Đặt các nút lên grid, co giãn theo kích thước map
3. Đục hành lang dọc các cạnh                      → XƯƠNG SỐNG
4. Từ ô trên hành lang, chạy Recursive Backtracker mọc nhánh ra hai bên
5. Nghiệm thu ràng buộc (mục 9.5) → retry, rồi hạ cấp nguyên mẫu
```

> **Thêm nguyên mẫu thứ 13 = thêm một file dữ liệu, không sửa một dòng thuật toán.**
>
> Đây là kỹ thuật PCG chuẩn công nghiệp (Spelunky dùng template phòng, Diablo dùng
> tileset). Và nó gần như miễn phí ở đây: `TDStageConfig` **đã có** `MapLayout` với 6
> bố cục — nguyên mẫu xương sống chỉ là bước tiến hoá của chính enum đó.

### 9.3 · Mười hai hình thái

| # | Tên | Sơ đồ | Áp lực đặc trưng |
|:--:|---|---|---|
| ① | **PHỄU** | `nhánh → ▣A,▣B → ▣▣ cuối → ◎` | Tập trung. Một trận đánh lớn. **Dễ đọc nhất → stage đầu** |
| ② | **THÁC** | `→ ▣1 → ▣2 → ▣3 → ◎` | Đường lùi rõ ràng nhất. **Thân thiện nhất với hệ stress** |
| ③ | **SONG TUYẾN** | `▣Bắc ─┐ ▣Nam ─┴→ ◎` (gặp ở ô cuối) | Chia rẽ chú ý. **Buộc bỏ rơi một bên. Tàn nhẫn nhất** |
| ④ | **XƯƠNG SỐNG** | `→─▣───▣───▣─→ ◎` (nhánh đâm ngang) | Áp lực dàn trải. **Thời gian di chuyển thành tài nguyên** |
| ⑤ | **VÀNH ĐAI** | đích ở giữa, cổng bao quanh | Mọi hướng đều là tiền tuyến. **Rescue cực giá trị** |
| ⑥ | **LỆCH** | `── ▣ ngắn ──→ ◎` + `/\/\ ▣ dài /\/\` | Fast đi ngắn, Tank đi dài. **Hai bài toán song song** |
| ⑦ | **CHẠC BA** | 3 tuyến không bao giờ nhập | Không có đường lùi. **Chỉ dùng cho độ khó cao nhất** |
| ⑧ | **ĐỒNG HỒ CÁT** | `nhánh → ▣ (1 ô) → phình → ◎` | Giữ được điểm thắt là thắng. **Mất nó là vỡ trận không cứu** |
| ⑨ | **XOẮN ỐC** | một đường rất dài cuộn vào trong | Nhiều thời gian, ít không gian. **Đảo ngược mọi cái khác** |
| ⑩ | **NGÃ TƯ** | hai đường cắt nhau tại `▣` | Một vị trí phủ cả hai luồng — **nhưng nằm sâu** |
| ⑪ | **CÂY** | `▣ ┬→◎ ├→◎ └→◎` (địch **phân kỳ**) | Chặn gốc sớm, hoặc chặn nhiều ngọn muộn |
| ⑫ | **GỌNG KÌM** | `→ ▣ → ◎ ← ▣ ←` | Hai mặt trận quay lưng. **Khoảng cách chi viện lớn nhất** |

### 9.4 · Hai trục biến thiên

| Trục | Giá trị | |
|---|---|:--:|
| **Hình thái** | 12 cái ở trên | ×12 |
| **Vị trí nút thắt** | Sớm (gần cổng) / Muộn (gần đích) | ×2 |
| **Số tầng** | 2 tầng / 3 tầng | ×2 |

48 tổ hợp danh nghĩa → **~28–32 hợp lệ và khác biệt rõ** sau khi loại các tổ hợp vô
nghĩa (Vành đai không có "nút thắt sớm", Xoắn ốc không có 3 tầng…).

Hai trục này không phải trang trí — **nút thắt sớm** = cam kết quân sớm, nhiều đường
lùi phía sau; **nút thắt muộn** = được quan sát lâu, nhưng sai một lần là hết đất lùi.

### 9.5 · Ràng buộc nghiệm thu cho generator

Dù chọn nguyên mẫu nào, phải đạt cả năm:

| # | Ràng buộc | Nếu vi phạm |
|:--:|---|---|
| 1 | **Giữ được bằng ≤60% roster ban đầu** | Mất 2 người vĩnh viễn = thua chắc, và người chơi biết từ phút 2 |
| 2 | **Thời gian nút thắt → đích ≥ 12 giây** (1,5 × chu kỳ xoay vòng) | Rút người ra là địch tới đích trước khi người thay thế đặt xong |
| 3 | Mỗi nút thắt rộng **2–4 ô** | Quá hẹp = nhàm chán, quá rộng = không phải nút thắt |
| 4 | Mọi cổng còn đường tới đích sau khi đặt quân | Kẹt địch |
| 5 | Không ô nào có lính bị cô lập hoàn toàn | Kẹt quân |

**Dây chuyền dự phòng:** mỗi nguyên mẫu khai báo điều kiện tối thiểu (tỉ lệ khung, số
ô). Không đạt → hạ cấp xuống nguyên mẫu dự phòng → cuối chuỗi luôn là **① Phễu**. Đây
đúng tinh thần *"không bao giờ trả về thất bại"* đã có trong generator.

### 9.6 · Rải theo stage — đường cong dạy học miễn phí

| Stage | Nguyên mẫu | Dạy điều gì |
|:--:|---|---|
| 1 | Phễu | Luật cơ bản |
| 2 | Thác | Lùi có tổ chức |
| 3 | Xương sống | Luân chuyển quân |
| 4 | Lệch | Phân loại mục tiêu |
| 5 | Vành đai | Quản lý chu vi |
| 6+ | Song tuyến / Chạc ba | Khó nhất |

**Hiện tên sơ đồ ở đầu màn:** `SƠ ĐỒ: THÁC`

Ba tác dụng gần như miễn phí: biến đa dạng thành **ngôn ngữ học được**; làm procedural
generation **nhìn thấy được** (hiện tại người xem demo không hề biết map là sinh tự
động); và tạo đường cong dạy học mà không cần thêm tutorial.

---

## 10 · Bảng hằng số đầy đủ

### Vào `TDConstant`

```csharp
STRESS_BASE_RATE            = 1.6f    // N2, điểm/giây
STRESS_PER_OVERLOAD         = 0.5f    // N1 — ⚠️ đo thật cho thấy quá nhỏ, xem dưới
STRESS_ALLY_SPIKE           = 20f     // N3 chết  (suy sụp = x1.5 = 30)
STRESS_RETREAT_RELIEF       = 70f
STRESS_RETREAT_COUNTDOWN    = 8.0f    // ép bởi waveInterval = 10
RESOLVE_E_LO                = -0.5f
RESOLVE_E_HI                = 3.0f
HORDE_PACK_SPAWN_INTERVAL   = 0.2f
```

> ⚠️ **`STRESS_PER_OVERLOAD = 0,5` không sống nổi qua phép đo** *(một ván, [1.6](#16--ba-số--dụng-cụ-đo))*
>
> Sau khi sửa vùng áp lực, N1 **có** kích hoạt: **8%** thời gian giao chiến, vượt trung bình
> **1,54** địch. Nhưng cân lại cả trận:
>
> | | Điểm cả ván |
> |---|--:|
> | N2 (nền) | 155s × 1,60 = **248** |
> | N1 (bị vây) | 0,08 × 155s × (0,5 × 1,54) = **9,5** |
>
> **N1 đóng 3,7%.** [Mục 04](#04--bốn-nguồn-tăng) gọi nó là *"nguồn chính"* — theo số đo thì
> không. Và lúc bị vây tốc độ chỉ nhích từ 1,60 lên 2,37 điểm/giây: **+48%, dưới ngưỡng
> người chơi nhận ra là có gì đó vừa đổi.**
>
> Hướng sửa cho Giai đoạn 2 (chốt khi stress chạy thật, không chốt bây giờ): `1,0` đưa
> tốc độ lúc bị vây lên **~3,1 điểm/giây — gấp đôi nền**. Gấp đôi là mức chênh cảm nhận
> được mà không cần nhìn số. Vẫn phải xác nhận bằng playtest, một ván bốn người lính chưa
> đủ để chốt một hằng số.
>
> ✅ **Ván ở Normal ủng hộ con số 1,0.** Vượt ngưỡng **14%** thời gian, vượt trung bình **1,80**:
>
> | | Lúc bị vây | So với nền 1,60 |
> |---|--:|--:|
> | `0,5` | 2,50 /giây | ×1,6 — *chưa đủ để nhận ra* |
> | **`1,0`** | **3,40 /giây** | **×2,1** |
>
> Và đỉnh **6 địch** trên một Striker ngưỡng 2 — vượt 4 — là đúng khoảnh khắc tuyến sắp vỡ,
> chỗ N1 sinh ra để đánh dấu. Cân cả trận: N1 chiếm 7,9% tổng ở hệ số 0,5 · **15,8% ở 1,0**.
> Vẫn không phải "nguồn chính" theo khối lượng, và [mục 04](#04--bốn-nguồn-tăng) đã sửa lại
> cho đúng: nó là **đỉnh**, không phải khối lượng.

### Bảng thời gian tham chiếu

> **Đừng tune bằng "điểm/giây". Tune bằng "bao nhiêu giây thì gãy"** — con số duy nhất
> người chơi cảm nhận được.

| Tình huống | R | **Gãy sau** | Đoạn Stressed |
|---|--:|--:|--:|
| Ở ngưỡng, một mình | 1,60 | **45 giây** | 10,6 giây |
| Ở ngưỡng, 1 đồng đội Calm | 1,20 | **60 giây** | 14,2 giây |
| Ở ngưỡng, 2 đồng đội Calm | 0,80 | **90 giây** | 21,3 giây |
| Vượt ngưỡng +1 | 2,10 | **34 giây** | 8,1 giây |
| Vượt ngưỡng +2 | 2,60 | **28 giây** | 6,5 giây |
| Vượt ngưỡng +3 | 3,10 | **23 giây** | 5,5 giây |
| Ở ngưỡng + aura Herald | 3,10 | **23 giây** | 5,5 giây |
| Ở ngưỡng + aura Boss | 3,60 | **20 giây** | 4,7 giây |
| **15 Bầy đàn vây Defender** | 7,10 | **10 giây** | 2,4 giây |
| Rảnh, không địch trong vùng | −0,60 | *hồi phục* | — |

### Chỉ có bốn núm được vặn

| Núm | Giá trị | Vặn khi |
|---|:--:|---|
| `STRESS_BASE_RATE` | 1,6 | Toàn cục quá nhanh / quá chậm |
| `STRESS_PER_OVERLOAD` | 0,5 | Bị vây chưa đủ đáng sợ, hoặc quá tàn nhẫn |
| `STRESS_RETREAT_RELIEF` | 70 | Xoay quân quá dễ / quá vô ích |
| `STRESS_ALLY_SPIKE` | 20 | Dây chuyền quá hiền / quá tuyệt vọng |

Mọi thứ còn lại **suy ra hoặc cố định**: ngưỡng chịu đựng suy ra từ `blockCount`, Ý chí
suy ra từ công thức, hệ số trạng thái là hằng số thiết kế.

**Thứ tự vặn khi playtest — mỗi lần một núm:**

```
1. STRESS_BASE_RATE       → chỉnh nhịp tổng thể TRƯỚC
2. STRESS_PER_OVERLOAD    → độ trừng phạt của vị trí sai
3. STRESS_RETREAT_RELIEF  → giá trị của kỹ năng xoay vòng
4. STRESS_ALLY_SPIKE      → kịch tính, chỉnh CUỐI CÙNG
```

Vặn núm 1 làm mọi thứ khác lệch theo — vặn núm 4 trước thì công vặn 1 sẽ đổ sông.

---

## 11 · Bốn sửa đổi bắt buộc trong code hiện có

### 🔴 11.1 · Countdown retreat = 8 giây

**Đo được:** `waveInterval = 10` ở cả hai level.

```
waveInterval = 10 giây   <   countdown dự kiến ban đầu = 12 giây
```

Khoảng lặng ngắn hơn thời gian gọi lại quân → **mỗi lần rút một người là chắc chắn
thủng lỗ khi wave sau tới** → động từ chính của hệ thống bất khả thi.

**Sửa: countdown 8 giây** (dư 2 giây biên). Không nâng `waveInterval` lên 14 vì nó kéo
dài trận đấu 20 giây mỗi màn mà chẳng đổi lấy gì.

### 🔴 11.2 · Nở pack Horde — trong `BuildWavePlans`, sau Fisher–Yates

Bầy đàn spawn theo **pack 5 con**, nên `Distribute()` trả về **số suất**, không phải
số con.

**Bẫy A — nở pack phải xảy ra TRƯỚC khi tính tổng.** Codex ghi rõ: *"Toàn bộ wave được
tính trước khi spawn con enemy đầu tiên… HUD cần biết tổng số enemy để hiện
`killed/total` và **để phán định thắng**."*

> Nếu nở pack lúc spawn: HUD hiện `0/40` trong khi thực tế có 120 con, và **điều kiện
> thắng không bao giờ kích hoạt**. Người chơi giết sạch mọi thứ rồi ngồi nhìn màn hình
> đứng yên. Đây không phải bug hiệu năng — đây là **bug game không kết thúc được**.

**Bẫy B — nở pack phải xảy ra SAU khi xáo bài.** Codex: *"chia theo tỉ lệ, xáo
Fisher–Yates, rồi mới append boss."*

> Nếu nở trước khi xáo, 5 con bị trộn tung ra khắp wave → nhỏ giọt từng con → **không
> bao giờ tụ thành bầy** → loại địch này mất trắng lý do tồn tại.

```
Distribute → [suất] → Fisher-Yates trên suất → nở Horde x5 liền kề → append Boss
                                                ↑ tại đây, không sớm hơn
```

**Bẫy C — `spawnInterval` giết Bầy đàn.** `spawnInterval = 1,5` → pack 5 con mất 7,5
giây ra hết cổng. Ở `baseSpeed = 4,0` với `cellSize = 2` (2 ô/giây), con đầu đi trước
con cuối **15 ô** — gần hết chiều rộng grid. Cả pack trải dài khắp bản đồ.

> **Sửa:** dùng `HORDE_PACK_SPAWN_INTERVAL = 0.2f` giữa các thành viên trong cùng pack
> (pack ra hết trong 0,8 giây → cách nhau 1,6 ô → một khối đặc), rồi quay lại
> `spawnInterval` bình thường sau pack.

### 🔴 11.3 · Boss không được là phần dư nữa

Code hiện tại: `boss = total − normal − fast − tank`.

Với 6 loại và làm tròn, thử **Easy, 30 địch**:
```
Normal 20 · Fast 4 · Tank 1 · Horde 4 · Herald 1
Boss = 30 − 20 − 4 − 1 − 4 − 1 = 0        ← KHÔNG CÓ BOSS
```

Càng nhiều loại thì sai số làm tròn càng dồn vào phần dư — mà phần dư đang là loại
quan trọng nhất.

> **Sửa:** tính Boss tường minh (`RoundToInt`, tối thiểu 1 nếu `bossPct > 0`), cho
> **Normal** — loại đông nhất, ít quan trọng nhất — nhận phần dư. Sai số ±2 con Normal
> thì không ai nhận ra; thiếu boss thì hỏng cả màn.

### 🟠 11.4 · Mở rộng `RatioRow` và `Distribute()` lên 6 loại

`RatioRow` thêm `hordePct`, `heraldPct`. `Distribute()` trả `int[6]`.

### Nợ kỹ thuật phải trả TRƯỚC khi bắt đầu

| # | Vấn đề | Vì sao phải trả trước |
|:--:|---|---|
| 1 | **Bug #3 — `Die()` gọi `Destroy()` ngay, animation không play xong** | `TDOperatorView` sắp có **đường thoát thứ ba** (bỏ chốt), phức tạp nhất. Thêm nhánh mới vào chỗ mà nhánh cũ đã sai là cách chắc chắn nhất tạo ra bug không lần ra được |
| 2 | **`ForceUnblock()` + `m_CurrentPathIndex++`** | Đang là hack chạy ở đường hiếm (lính chết). Với stress, lính rời chốt **liên tục** → hack thành đường nóng, lộ mọi giả định ngầm |
| 3 | **Hai grid song song** | Codex tự gọi là *"nợ thật, nên hợp nhất ngay khi có dịp"*. Lính rời chốt = ô đổi trạng thái giữa trận, thường xuyên. **Đây chính là dịp đó** |
| 4 | **Rò rỉ event** | Vòng đời operator giờ có nhiều lối ra hơn (chết / retreat / bỏ chốt / rescue) → nhiều chỗ quên `-=` hơn |

### Yêu cầu hiệu năng do Bầy đàn đặt ra

Trường hợp xấu nhất: Nightmare, `totalEnemies` ≥75, Horde 20% = 15 suất × 5 = **75 con
Bầy đàn** + 60 con khác ≈ **135 địch**.

| Hạng mục | Hiện tại | Cần cho Horde |
|---|---|---|
| Pool | 4 pool theo `EnemyType` | **6 pool**, pool Horde lớn hơn nhiều |
| Animator | Đủ Walk/Attack/GetHit/Die | 🔴 **75 Animator là điểm nghẽn lớn nhất.** Prefab Horde **không có Animator** — một tween scale/bob là đủ |
| HP bar | `HPBar_Enemy.prefab` world-space | 🔴 **Horde không có HP bar.** Chúng chết trong 1–3 đòn nên bar cũng vô nghĩa |
| Mesh | Model đầy đủ | Model rất nhẹ, **bật GPU Instancing** |

> **Điểm tốt:** loại địch này vừa là cơ chế hay, vừa **ép tạo ra đúng câu chuyện hiệu
> năng** mà một studio muốn nghe: *"tôi thêm một loại địch số lượng lớn, nó đẩy số
> enemy đồng thời lên 135, tôi profile trên máy thật, bỏ Animator và HP bar cho loại
> đó, bật GPU instancing, giữ được 60fps."* Hai mục tiêu, một công việc.

---

## 12 · Điều kiện nghiệm thu

Định nghĩa "thất bại" **trước** khi bắt đầu, theo thứ tự:

### ① Bài kiểm tra A4 — tuần 1

> Tự hỏi khi prototype chạy: **"Tôi có bao giờ rút một con lính còn gần đầy máu ra không?"**
>
> - **Có** → stress và HP đã độc lập. Cơ chế có lý do tồn tại.
> - **Không** → **nó là thanh máu thứ hai. Dừng lại, đừng làm tiếp.**

### ② Bài kiểm tra 10 giây — tuần 2

> Đưa build cho một người **chưa từng xem game**, không giải thích gì. Để họ chơi tới
> lúc một operator gãy. Hỏi: *"Vừa xảy ra chuyện gì với anh chàng đó?"*
>
> - **Trả lời được** → hệ thống sống.
> - **Không** → **vấn đề ở UI, không phải ở toán. Đừng chỉnh số, sửa cách hiển thị.**

### ③ Bài kiểm tra tiếc nuối — tuần 3

> Người chơi thua rồi có tự nói *"đáng lẽ tôi phải rút nó ra sớm hơn"* không?
>
> - **Có** → đã có narrative sinh ra từ cơ chế.
> - **Nói "game này xui"** → **đang có hộp đen.** Quay lại kiểm tra tính công bằng của
>   mọi nguồn stress.

---

## 13 · Lộ trình

> Mục này giải thích **vì sao thứ tự là như vậy**. Phân rã theo ngày, theo file, kèm
> tiêu chí nghiệm thu từng bước nằm ở [mục 15](#15--kế-hoạch-triển-khai).

| Ưu tiên | Việc | Vì sao thứ tự này |
|:--:|---|---|
| **0** | Trả [nợ kỹ thuật #1](#nợ-kỹ-thuật-phải-trả-trước-khi-bắt-đầu) (bug death animation) | Sắp thêm nhánh thứ ba vào chỗ nhánh thứ nhất đang sai |
| **1** | **Đổi generator sang tô-pô hội tụ** (mục 09) | Không có nó thì mọi thứ khác **không kiểm chứng được** — sẽ tune stress trên bản đồ về bản chất không chơi được, rồi kết luận nhầm rằng hệ thống hỏng |
| **2** | 4 nguồn stress + đường cong tăng tốc + Ý chí | Lõi |
| **3** | **Động từ Rescue** | Biến hình phạt thành lưỡng nan hay nhất trong game |
| **4** | Icon 3 trạng thái, trên cả model lẫn deploy bar | **70% công sức nằm ở đây**, không phải ở toán |
| **5** | Quyết tử do người chơi bấm · sát thương ×3 | Giữ hợp đồng công bằng |
| **6** | Bầy đàn + Kẻ gieo sợ + van tiếp viện | Chứng minh stress ≠ HP, chặn chết chùm |
| **7** | Tối ưu hiệu năng cho 135 địch + profiling trên máy thật | Câu chuyện cho portfolio |

> ⚠️ **ƯỚC LƯỢNG ĐÃ SỬA — 05/09/2026**
>
> Con số **"2,5 – 3,5 tuần"** ở bản đầu chỉ tính riêng hệ tinh thần. Nó **không** bao
> gồm ba khối lớn cũng bắt buộc phải có: đổi tô-pô địa hình ([mục 09](#09--địa-hình--12-nguyên-mẫu-xương-sống)),
> hai loại địch mới ([mục 08](#08--hai-loại-địch-mới)), và tối ưu hiệu năng cho 135 địch.
>
> **Phạm vi đầy đủ: 5 – 7 tuần** (25–34 ngày công).
> **Bản cắt gọn để có demo quay video: ~4 tuần.**
>
> Trong đó vẫn giữ nguyên nhận định cũ: **~1 tuần là cân bằng và UI**, và đó là tuần
> quyết định hệ thống hay hay dở.

> **Mục 1 phải đứng trước mục 2.** Đây là sai lầm dễ mắc nhất: tune một hệ thống trên
> một bản đồ mà cấu trúc của nó chống lại chính hệ thống đó.

---

## 14 · Nhật ký quyết định

Những phương án đã cân nhắc và **bị loại**, kèm lý do — để sau này không ai đề xuất lại.

| Phương án | Bị loại vì |
|---|---|
| **"Máu thấp → stress tăng nhanh"** làm nguồn thứ 5 | Nối stress vào HP → người chơi nhìn máu là đoán được stress → **stress thành thanh máu thứ hai bằng đường vòng**. Có thể thêm lại ở v2 với trọng số ~20% nếu playtest thấy hai chỉ số rời rạc quá |
| **Xác suất chết ngay khi suy sụp** | Xé hợp đồng *"thua là lỗi của bạn"*. Thay bằng sát thương ×3: kết quả gần như y hệt, nhưng tính được |
| **Quyết tử tự kích hoạt ngẫu nhiên** | Chỉ là "một cách thua khác có nhạc hay hơn". Phải do người chơi bấm mới thành quyết định |
| **Suy sụp không có lối thoát nào** | Người chơi không có hành động nào để thực hiện = đoạn phim phạt, không phải quyết định. Rescue giải quyết |
| **Class "Anchor" — 4 unit đặc biệt định hình mê cung** | Chỉ chạm ~20% số lần đặt lính → là feature, không phải lõi |
| **Roguelite campaign bọc ngoài** | Là **mode**. Gỡ ra thì trận đấu vẫn nguyên vẹn → không phải lõi. Và nó được dùng làm cái cớ để nhét narrative vào — lý luận vòng vo |
| **Lính chặn đường + địch phá khi bị bịt kín** | **Orcs Must Die (2011)** đã làm y hệt: barricade *"cho phép re-route tuỳ ý"* + *"địch phá barricade nếu không còn đường khác"* |
| **Mazing bằng đơn vị** | Cả một subgenre có từ Warcraft 3: Block Siege, Blocks & Mobs, ReShape TD |
| **Fog of war che map** | Rogue Station Defense đã có |
| **Đào địa hình lấy tài nguyên** | Mindustry, Miner TD, Gem Miner TD |
| **Retreat hồi stress về 0** | Xoay vòng vô hạn = stress không bao giờ quan trọng. −70 tạo ra bài học *"rút sớm là reset, rút muộn là hoãn"* |
| **`STRESS_BASE_RATE = 0.8`** | Đo thật cho thấy operator sẽ gãy ở **giây 213** của một trận **222 giây** → hệ thống **không bao giờ xuất hiện**. Nhân đôi lên 1,6 |
| **Chuẩn hoá Ý chí theo phần trăm vị thứ trong roster** | Thêm một operator mới là Ý chí của tất cả những người còn lại đổi theo → kiến thức người chơi không ổn định |
| **Ràng buộc runway = 1,5 × `STRESS_RETREAT_COUNTDOWN` = 12 giây** | Suy ra từ **một con số có sẵn gần đó**, không phải từ thứ nó cần đo. Countdown chỉ khoá **đúng operator vừa rút**; người chơi còn 7 slot khác đặt được ngay. Thứ thật sự phải lọt vào cửa sổ đó là **một thao tác kéo thả (~2–3s)**, không phải chu kỳ gọi lại. Đo thật ra **4,0s** trên map 21 ô — bar 12s không map nào đạt nổi. Hạ xuống **4s**. *Loại lỗi đáng nhớ: lấy một hằng số đang có rồi nhân hệ số, thay vì hỏi ràng buộc này gác cái gì* |
| **Năm mức độ khó** (Easy · Normal · Hard · Extreme · Nightmare) | Đo cho thấy game thắng được với **2 operator và không phải rút lần nào** — một nấc dưới mức đó không dạy người chơi điều gì. Và năm nấc nghĩa là hai nấc cạnh nhau chênh ~15% HP, mức không ai cảm nhận được. Còn **ba**: Normal · Hard · Nightmare, hpMult 1,0 → 1,2 → 2,0. Enum đánh 0/1/2 nên mọi level trong `Level Config` **dịch lên một nấc** — Easy thành Normal, Normal thành Hard: đó là chủ ý, không phải tai nạn migration |
| **Vùng áp lực melee = `rangeOffsets`** | Melee mặc định chỉ với tới **ô đang đứng**, mà ô đó chỉ chứa được đúng `blockCount` con. Ngưỡng lại là `1 + blockCount` → N1 hụt đúng 1, **vĩnh viễn, theo cấu trúc**. Đo thật: **0% trong 110 giây giao chiến**. Đổi sang khối 3×3. *Loại lỗi đáng nhớ: định nghĩa một đại lượng bằng thứ có sẵn trong code (`rangeOffsets`) thay vì bằng thứ nó phải mô tả (**bị vây**) — và cái trần của nó nằm ngoài tầm nhìn cho tới khi có số* |
| **Tách asmdef cho code riêng** (`TD.Model` ← `TD.Control` ← `TD.View`) | Không mở khoá được test (`.api` tĩnh vẫn không mock được) · Model kéo theo Services nên ranh giới assembly ≠ ranh giới kiến trúc · **không làm compile nhanh hơn** vì `Assembly-CSharp` tham chiếu mọi asmdef nên 378 file third-party vẫn biên dịch lại. Chi tiết + 3 điều kiện quay lại ở [mục 15](#-04-asmdef--đã-bỏ-khỏi-kế-hoạch) |
| **NUnit / Unity Test Framework ngay bây giờ** | `.asmdef` không tham chiếu được `Assembly-CSharp` → test assembly không nhìn thấy code. Dùng Editor validator trong `Assembly-CSharp-Editor` thay thế — và với một studio đề cao *craftsmanship*, **một menu item designer bấm được là công cụ, mạnh hơn một bộ test** |

---

## 15 · Kế hoạch triển khai

> Nguyên tắc xếp thứ tự: **phụ thuộc trước, tính năng sau.** Mỗi bước có tiêu chí
> "xong khi nào" kiểm được. Bốn **chốt kiểm tra** là điểm bắt buộc dừng lại và đánh
> giá trước khi tiêu thêm ngày công.

### Tổng quan

| Giai đoạn | Nội dung | Ngày | Chốt |
|:--:|---|--:|:--:|
| **0** | Nền móng — trả nợ kỹ thuật + đo baseline | 2–3 · *0.2 hoãn* | |
| **1** | Địa hình hội tụ **+ 1.7 chất lượng đọc được** | 5–7 → thực ~9 | ✅ **A đạt** |
| **2** | Lõi stress, chưa có UI đẹp | 3 | 🛑 **B** |
| **3** | Suy sụp · Rescue · Quyết tử | 3–4 *(+1 dự phòng)* | |
| **4** | Truyền đạt — icon, âm thanh, phản hồi | 4–5 | 🛑 **C** |
| **5** | Hai loại địch | 3–4 | |
| **6** | Hiệu năng | 2–3 | |
| **7** | Nội dung và cân bằng | 3–5 | 🛑 **D** |
| | **Tổng** | **25–34 ngày ≈ 5–7 tuần** | |

---

### GIAI ĐOẠN 0 · Nền móng — 2–3 ngày

> Không viết một dòng nào của hệ tinh thần cho tới khi xong ba việc này.

| # | Việc | Vị trí | Xong khi | |
|:--:|---|---|---|:--:|
| 0.1 | **Sửa bug #3** — `Die()` huỷ ngay, không có cả trigger animation | `TDOperatorView` | Clip Die chạy hết mới huỷ; xác không chặn, không chọn được | ✅ |
| 0.2 | **Đo baseline hiệu năng** — ms/frame, GC alloc, draw call ở wave giữa, **trên máy Android thật** | — | Có ảnh chụp Profiler. Đây là số "trước" của câu chuyện portfolio | ⬜ |
| 0.3 | **Assertion cho `Distribute()` bản 4 loại hiện tại** | `Editor/TDBalanceValidator.cs` | 600 ca (3 độ khó × total 1→200) qua cả 4 assertion | ✅ |

#### Ghi chú 0.1 — bug nặng hơn tài liệu cũ ghi

Ghi chú ban đầu nói *"`Die()` gọi `TriggerSafe("Die")` rồi `Destroy()` ngay"*. Code thật
**không gọi trigger nào cả** — chỉ `OnRemove()` → bắn event → `Destroy()`. Và `DoRetreat()`
với `Die()` **giống hệt nhau từng dòng**, đúng vấn đề "ba đường thoát" ở [mục 11](#nợ-kỹ-thuật-phải-trả-trước-khi-bắt-đầu).

Bản sửa: cờ `m_IsDying` (chặn `TakeDamage` và `DoRetreat` tác động lên xác) · kích hoạt
trigger `Die` · `Destroy(gameObject, OPERATOR_DIE_DURATION)` — dùng overload sẵn có, không
cần coroutine (enemy cần vì nó trả pool, operator chỉ huỷ) · ẩn HP bar.

`HasAnimatorTrigger()` kiểm tra Animator có thật sự khai báo trigger `Die` không; không có
thì huỷ ngay như cũ — **delay 1,5 giây trên một prefab không có clip Die còn tệ hơn bug gốc.**

Thứ tự có chủ đích: `OnRemove()` chạy **trước**, giải phóng ô ngay — giống `TDEnemyView.Die()`
(unblock trước, animate sau). Hệ quả cần biết: ô trống ngay nên có thể đặt operator mới lên
đúng ô đó trong lúc xác còn diễn → hai model chồng nhau tối đa 1,5 giây. Enemy đã có sẵn
đánh đổi y hệt.

#### Ghi chú 0.3 — vì sao là Editor validator, không phải NUnit

> **NUnit không khả thi ở cấu hình hiện tại**, và không phải vì thiếu package.
>
> Unity Test Framework đòi code-under-test nằm trong `.asmdef`, mà **assembly khai báo bằng
> `.asmdef` không tham chiếu được `Assembly-CSharp`** — nơi cả 117 file của project đang nằm.
> Dù cài package, test assembly vẫn **không nhìn thấy `DifficultyRatioTable`**.
>
> `TDBalanceValidator` nằm trong `Assets/2.Scripts/Editor/` nên thuộc `Assembly-CSharp-Editor`,
> assembly này **có** quyền nhìn `Assembly-CSharp`. Cùng bộ assertion, chạy được ngay, không
> cài gì, không đụng cấu trúc assembly.

**Bốn assertion**, chạy từ `Tools ▸ TD ▸ Validate Balance Tables`:

| | Kiểm gì |
|---|---|
| `SUM` | Tổng luôn = `total` — bất biến mà mẹo phần dư sinh ra để đảm bảo |
| `NEGATIVE` | Không loại nào âm (phần dư bị chìm) |
| `DRIFT` | Mỗi loại làm tròn tường minh lệch ≤1 so với lý tưởng — **bỏ qua index phần dư**, vì hấp thụ sai số là việc của nó |
| `NO_BOSS` | ⭐ Ratio đòi boss thì phải có boss |

> **VÌ SAO 0.3 ĐỨNG Ở ĐÂY, KHÔNG PHẢI Ở GIAI ĐOẠN 5**
>
> Bước 5.1 sẽ mở `Distribute()` lên 6 loại. Viết assertion **trước** thì `NO_BOSS` sẽ
> **tự bắt lỗi boss = 0** ở [mục 11.3](#-113--boss-không-được-là-phần-dư-nữa) thay vì phải
> phát hiện bằng mắt sau khi chơi một màn Easy không có boss.
>
> Hằng số `REMAINDER_INDEX` trong file có comment nhắc: lên 6 loại thì đổi sang index Normal.

> 🔴 **Validator này lần đầu được CHẠY THẬT là ở buổi rút gọn độ khó — và nó fail ngay.**
> Trước đó mục 0.3 ghi *"1000 ca qua cả 4 assertion"*: con số đó chưa bao giờ được kiểm.
> Lần chạy thật cho **3 lỗi**, cả ba nằm ở `Normal`, tức đã tồn tại từ đầu:
>
> ```
> [SUM]     Normal total=6:  counts sum to 7 — [4,2,1,0]
> [NO_BOSS] Normal total=38: wants 1.14 boss, got 0
> [NO_BOSS] Normal total=46: wants 1.38 boss, got 0
> ```
>
> **Mẹo "làm tròn ba loại, phần dư cho Boss" sai ở hai đầu.** Với total = 6, làm tròn
> 3,6 / 1,5 / 0,72 lên cho ra 7 chỗ trên 6 con; Boss thành −1 và `Mathf.Max(0, …)` **âm
> thầm biến thiếu hụt thành một con địch thừa** — đúng dòng comment tự tin ghi *"sum always
> equals total"*. Ở đầu kia, ba loại trước đã ăn hết sai số nên Boss đáng 1,14 mà nhận 0.
>
> Thay bằng **largest-remainder (Hamilton)**: mỗi loại lấy `floor(ideal)`, số chỗ còn thiếu
> chia cho các phần lẻ lớn nhất. Không còn "loại hứng phần dư" nào cả, và **cả bốn assertion
> đúng theo xây dựng, không theo may mắn** — tổng bằng `total` vì chỗ được phát từng cái một;
> không loại nào âm vì `floor` của số không âm là không âm; mọi loại lệch < 1 vì nó là `floor`
> hoặc `floor+1`; và loại nào có ideal ≥ 1 thì nhận ít nhất `floor(ideal) ≥ 1` — nên màn nào
> đáng có boss là có boss.
>
> Sau khi sửa: **0 lỗi**. `[4,1,1,0]`=6 · `[23,9,5,1]`=38 · `[28,11,6,1]`=46.
>
> *Bài học không nằm ở thuật toán mà ở chỗ: một công cụ kiểm tra chưa chạy thì chưa phải
> công cụ kiểm tra. Nó đã nằm trong repo suốt, ở trạng thái ✅, và bắt được lỗi ngay giây
> đầu tiên được bấm.*

#### ❌ 0.4 (asmdef) — đã bỏ khỏi kế hoạch

Từng nằm ở đây với ghi chú *"làm song song được, không chặn ai"*. **Cả hai đều sai** — nó
chặn 0.3 (xem trên), và khảo sát cho thấy nó không đáng làm:

| Lý do bỏ | |
|---|---|
| **Không mở khoá được gì** | Codex đã tự chẩn: hơn 20 class dùng `.api` tĩnh → *"không mock được"*. asmdef không sửa được điều đó. Thứ test được vẫn chỉ là hàm thuần — mà validator đã phủ |
| **Model không tách sạch được** | Cả 6 SO config gọi `TDResourceObject` (MonoBehaviour, tầng Services). `TD.Model` sẽ phải kéo Services theo → ranh giới assembly không phản ánh ranh giới kiến trúc → mất luôn lợi ích chính |
| **Không làm compile nhanh hơn** | 448 `.cs` trong `Assets`, 70 có asmdef che, **378 rơi vào `Assembly-CSharp`** (Modern UI Pack 92 · SidekickCharacters 61 · BuildReport 45 · TMP Examples 34…). Mà `Assembly-CSharp` **tự động tham chiếu mọi asmdef** → đẩy code riêng vào asmdef thì 378 file kia **vẫn biên dịch lại** mỗi lần sửa |
| **Chồng refactor** | Giai đoạn 1 đã là đổi bộ sinh map. Hai thay đổi cấu trúc chồng nhau thì lúc vỡ không biết vỡ vì cái nào |

**Nếu compile chậm thật thì sửa chỗ khác** — vấn đề là 378 file third-party ngồi chung
assembly, không phải thiếu asmdef. Rẻ hơn và không rủi ro: xoá `TextMesh Pro/Examples & Extras`
(34 file, Unity tạo lại được) · xoá scene demo của Modern UI Pack (~40) · asmdef cho
`BuildReport` (45, editor-only). **Và nó giúp luôn build size** — cùng hướng với việc đã kéo
255 → 102 MB. asmdef cho code riêng thì không được cái nào.

**Ba điều kiện quay lại làm**, chưa cái nào đúng: có người thứ hai sửa code song song · cần
CI chạy test tự động · compile thật sự thành nỗi đau (và kể cả lúc đó vẫn dọn third-party trước).

---

### GIAI ĐOẠN 1 · Địa hình hội tụ — 5–7 ngày ⚠️ rủi ro cao nhất

> Chặn mọi thứ phía sau. Tune stress trên bản đồ song song = tune trên bản đồ đang
> chống lại chính hệ thống đó.

| # | Việc | Xong khi | |
|:--:|---|---|:--:|
| 1.1 | Cấu trúc dữ liệu nguyên mẫu — đồ thị: nút + cạnh + tầng, toạ độ **flow-space** | Đọc được từ file, không hardcode | ✅ |
| 1.2 | Bộ đặt nút lên grid + đục hành lang xương sống | Xương sống hiện đúng hình trên grid bất kỳ | ✅ |
| 1.3 | Nối vào Recursive Backtracker sẵn có để mọc nhánh hai bên | Map vẫn trông như mê cung, nhưng có trục chính | ✅ |
| 1.4 | **Nguyên mẫu đầu**: Phễu · Thác ~~· Xương sống~~ chạy thử trong Unity | Ra đúng hình, có số chứng minh | ✅ |
| **1.7** | **Chất lượng đọc được của bản đồ** — hạng mục kế hoạch bỏ sót, xem dưới | Map đọc được trong 2 giây | ✅ |
| 1.5 | Bộ nghiệm thu [5 ràng buộc](#95--ràng-buộc-nghiệm-thu-cho-generator) + dây chuyền dự phòng | Sinh **200 map liên tiếp**, không map nào vi phạm, không map nào fail | ✅ |
| 1.6 | **ĐO ba số còn treo**: thời gian nút thắt→đích · ngưỡng phủ tối thiểu · số địch TB vây một operator | Có con số thật thay cho giả định | ✅ |

> **BA, KHÔNG PHẢI MƯỜI HAI.**
> Thuật toán chạy được với 3 nguyên mẫu thì 9 cái còn lại chỉ là file dữ liệu — để
> Giai đoạn 7. Làm 12 cái trước khi biết pipeline có đúng không là cách chắc chắn
> nhất để phải làm lại 12 lần.

#### Trạng thái 1.5 chi tiết

Bốn ràng buộc được thêm **ngoài danh sách** trong lúc gỡ lỗi, vì mỗi cái đều do một lần
hỏng thật ép ra: độ dài trục · chiều ngang · tỉ lệ road/wall · dây chuyền dự phòng.

| # | Ràng buộc | | Cài đặt |
|:--:|---|:--:|---|
| 1 | Giữ được bằng **≤60% roster** | ✅ | `WidestFront` — bề rộng mặt trận đồng thời, đếm theo **làn** (cụm liền nhau theo trục vuông góc), so với `0,6 × CONFIG_MAX_SLOTS`. Đo được **2 làn**, trần 4 |
| 2 | Nút thắt → đích **≥ 4 giây** | ✅ | Cảnh báo, **không retry**: đây là thuộc tính của kích thước map, roll lại không sửa được. Đo được **5,3s** |
| 3 | Nút thắt rộng 2–4 ô | ✅ | Nguyên mẫu bảo đảm bằng `width` |
| 4 | Mọi cổng còn đường tới đích | ✅ | A* lại trên grid cuối, sau final pass |
| 5 | Không lính nào bị cô lập | ✅ | Flood-fill từ cổng đầu, mọi ô route phải nằm trong |
| **6** | **Nút thắt không bịt kín được ở wave 1** | ✅ | Cảnh báo, không retry — bề rộng do nguyên mẫu quyết, khả năng mua do kinh tế. Hiện: hẹp nhất **3 ô** vs **2** melee mua nổi (30 vàng / Knight 15) |

> **VÌ SAO C6 CẦN TỒN TẠI**
>
> Melee đánh **một** mục tiêu và địch **không xếp hàng** — ô bị chặn thì địch đi vòng qua.
> Nên một hàng người phủ kín bề ngang nút thắt **chặn đứng dòng chảy**, và [N1](#04--bốn-nguồn-tăng)
> (đo địch lọt qua) không bao giờ kích hoạt được nữa.
>
> Đo ở ván Normal: tuyến xử lý **0,36 địch/giây** so với **0,40** đang tới — tỉ số 1,11, rò rỉ
> 14%. **Thêm đúng một Striker** là tỉ số về 0,74 và áp lực biến mất vĩnh viễn. Nút thắt vì
> vậy không phải một núm độ khó; nó là thứ giữ cho câu trả lời của người chơi không phải là
> *"mua thêm một thân người"*.
>
> ⚠️ **Ràng buộc này chỉ gác wave 1.** Vàng thụ động 1/3 giây nghĩa là sau ~45 giây người
> chơi thừa sức bịt kín — và đó là phần thưởng xứng đáng cho việc chơi tốt. Cái nó ngăn là
> **bịt kín ngay từ giây đầu**, trước khi trận đấu kịp đặt ra câu hỏi nào.

> **Ngưỡng 12 giây ở dòng 2 là con số sai, đã đổi thành 4.** Cách nó sinh ra là bài học
> đắt hơn bản thân con số — xem [nhật ký quyết định](#nhật-ký-quyết-định).

#### 1.7 · Chất lượng đọc được của bản đồ *(hạng mục bỏ sót)*

Kế hoạch coi 1.4 là *"mở Unity, nhìn map"* — một buổi. Thực tế nó nở thành một mảng
riêng sau **sáu vòng** mà bản đồ vẫn không đọc được. Nguyên nhân gốc không nằm ở thuật
toán mà ở chỗ **chưa ai hỏi bản đồ TD tốt trông thế nào và vì sao**.

| Việc | Vì sao phát sinh | |
|---|---|:--:|
| Research thiết kế map TD *(Doucet, Kingdom Rush, PCG metrics, Arknights)* | Bốn vòng sửa mà map vẫn xấu → phải đi học thay vì đoán tiếp | ✅ |
| **Ngân sách ô đặt tháp** + sinh theo **bệ** | ~170 vị trí đặt / ~8 quân mua nổi = **không có quyết định nào**. Kingdom Rush dùng 10–20 | ✅ |
| Dải an toàn camera cho cổng **và** bệ | Cổng nằm vành ngoài cùng — đúng dải UI che | ✅ |
| Mật độ cảnh trí | Ô trống không chỉ xấu, nó **mơ hồ**: không phân biệt được với slot chưa để ý | ✅ |
| 5 bug pipeline có sẵn | Lộ ra khi xương sống vào — xem [nhật ký](#nhật-ký-lỗi-đã-gỡ-trong-17) | ✅ |

**Bốn nguyên tắc rút từ research, chi phối mọi quyết định địa hình về sau:**

1. **Map nhỏ, không cuộn.** *"Scrolling maps are the enemy of FOCUS"* — Doucet. Ràng buộc
   camera cố định không phải phiền toái kỹ thuật, nó là **đầu vào đầu tiên**.
2. **Ô đặt phải khan hiếm.** Khan hiếm vị trí *chính là* quyết định. Arknights: ~13 ô cao
   trên map có Unit Limit 9 — tỉ lệ ~1,4.
3. **Hành lang ≠ Buồng.** Nghiên cứu PCG gọi tên: *corridor* để đi, *chamber* để đánh.
4. **Đường có sẵn, không phải mê cung.** Mê cung tăng tải nhận thức mà không tăng chiều
   sâu chiến thuật.

> ⚠️ **Nguyên tắc 4 đang mâu thuẫn với kiến trúc hiện tại.** Bộ sinh vẫn là mê cung rồi
> lọc bớt. Nếu 1.4 còn hỏng thêm một vòng nữa, việc cần xem lại là **giả định gốc** đó,
> không phải thêm một lần sửa vặt.

#### Nhật ký lỗi đã gỡ trong 1.7

Ghi lại vì mỗi cái là một loại bẫy sẽ tái diễn:

| Lỗi | Loại |
|---|---|
| `VisualizeAllPaths` vẽ đường **chỉ từ corridor** — ô xương sống walkable nhưng không có tile, và **không được `RegisterPathCell`** → nút thắt không đặt được lính | Hai nguồn sự thật cho "cái gì là đường" |
| `TDDeployController` cũng nghe `onValidTowerCellsReady` → nhận 129 ô làm "đặt được" trong khi chỉ 12 ô đăng ký | Sửa hai đầu, quên đầu thứ ba |
| Footprint obstacle nuốt ô slot → 12 tile dựng nhưng 1 cái khuất và **chết** | Tập hợp dùng sai phạm vi |
| `OpenCorridor` rẽ **ở phía cổng** → đường vào cổng 2 **xuyên qua cổng 1** | Hình học L đặt góc sai đầu |
| `IsCellValid` cho tháp thiếu `IsInTowerZone` | Lỗ **do chính thay đổi tạo ra**, không phải có sẵn — dán nhãn sai một lần |
| **Melee không đánh theo `rangeOffsets`** — `PathCellOperatorBehavior` chỉ đánh địch **nó đang chặn**, trong khi deploy controller và selection view đều **vẽ ô tầm** từ `rangeOffsets`. Với Knight/Striker/Ace/Layla (offsets không chứa `{0,0}`) thì vùng vẽ và vùng thật **không chung một ô nào**. Phát hiện khi test Chốt A | **Hai nguồn sự thật**, lần này giữa *thứ giao diện hứa* và *thứ code làm*. Sửa bằng hợp hai tập ở đúng một chỗ — bất biến "địch mình đang chặn thì phải đánh được" không nên phụ thuộc 4 dòng data khai đúng |
| **Bản sửa trên buff Striker ~4×** — mở tập mục tiêu ra cả tầm nhưng vẫn đánh *tất cả*, thành 80 sát thương lên mọi địch trong 3 ô | **Sửa một lỗ, mở một lỗ.** Khi nới một tập hợp, phải hỏi ngay *cái gì từng giới hạn nó?* |
| **Không có thời gian chuẩn bị trước wave 1** — `await PauseAwareDelay(waveInterval)` nằm ở **cuối** vòng lặp, nên chỉ có nghỉ *giữa* các wave. Wave đầu spawn ngay khi map gen xong. Normal che mất (30 vàng = 1 Striker, đủ cầm); Nightmare thành chí mạng: con thứ hai cần 9 giây tích vàng, mà ở máu ×2 thì tuyến đã vỡ trước đó | **Thiếu sót lộ ra nhờ tăng độ khó, không phải do tăng độ khó.** Nó luôn ở đó. Sửa: gọi delay một lần trước vòng lặp — dùng lại `waveInterval` thay vì đẻ hằng số, vì wave 1 có quyền được nghỉ như wave 2 |
| `RequiredAxisLength` so yêu cầu với **toàn bộ** trục, trong khi các nút chỉ sống trên một lát của trục. THÁC cần 5,0 ô giữa hai tâm, thực có 4,0 — ba buồng chồng lấn suốt mà check báo `16,1 ≥ 13 ✅` | **Phép đo im lặng chấp nhận, không kêu.** Bốn lần trước phép đo kêu sai và lộ ngay; lần này nó *giả vờ đạt*, chỉ lộ vì C2 kêu runway 2,7s. Sửa: `SpanU × axis / (tier−1) ≥ (w₁+w₂)/2 + gap` |

> **Chốt: melee luôn đánh ĐÚNG MỘT mục tiêu**, bất kể `blockCount` hay `attackType`.
> `blockCount` là số địch **giữ** được, không phải số địch **đánh** — Defender ghim ba con
> và vẫn chỉ vung vào một. Ưu tiên: con đang bị chặn; không có ai bị chặn thì lấy con
> **gần nhất** trong tầm (gần nhất chứ không phải gặp đầu tiên, để mục tiêu không nhảy
> loạn giữa hai nhát chém).
>
> Lần sửa hụt trước đó định dùng `IDeployableDTO.MaxTargets` (= `blockCount`) làm trần —
> vẫn sai, vì nó lẫn *giữ* với *đánh*. Một trần lấy từ field có sẵn nghe hợp lý hơn nó
> đúng: **cùng loại lỗi với hằng số 12 giây và với `rangeOffsets` làm vùng áp lực.**
| `WidestFront` đếm **ô** trong mỗi lát cắt, không đếm **làn** → khúc đứng của hành lang chữ L nằm trọn trong một cột, một làn đi vòng đọc thành 5 mặt trận song song → C1 loại sạch 10 lượt | **Đo sai thứ cần đo.** Dấu hiệu: 10 roll ngẫu nhiên ra **cùng một con số** — hỏng nằm ở phép đo, không ở lần tung |

#### 1.6 · Ba số — dụng cụ đo

| Số | Gác cái gì | Đo bằng | |
|---|---|---|:--:|
| **Nút thắt → đích** | Rút một người khỏi nút thắt cuối còn kịp thay không — vòng luân chuyển mà hệ tinh thần chạy trên đó | `WarnOnGoalRunway`, in lúc gen | ✅ **4,0 – 5,3s** |
| **Ngưỡng phủ tối thiểu** | Bệ đặt có nằm đúng chỗ không. Phủ thấp = bệ vô dụng; phủ 100% = không còn quyết định nào | `LogRealRangeCoverage`, in lúc gen | ✅ **57%** trần |
| **Số địch TB vây** | `STRESS_PER_OVERLOAD` của [N1](#04--bốn-nguồn-tăng): `0,5 × (số địch − ngưỡng)`. Nếu thực tế hiếm khi vượt ngưỡng thì N1 gần như không bao giờ chạy | `TDPressureProbe`, lấy mẫu 1 Hz, in lúc Victory/GameOver | ✅ xem dưới |

**Số địch vây — trước và sau khi sửa vùng ở [mục 03](#03--vùng-áp-lực-và-ngưỡng-chịu-đựng):**

| | Giây giao chiến | Trung bình | Đỉnh | Vượt ngưỡng |
|---|--:|--:|--:|--:|
| Vùng cũ (`rangeOffsets`), 4 lính | 110 | 1,36 | 3 | **0%** |
| Vùng mới (3×3), 4 lính | 155 | 1,78 | **5** | **8%**, vượt TB 1,54 |
| Vùng mới (3×3), **ép 2 lính** | 157 | 1,83 | 5 | **9%**, vượt TB 1,64 |
| Sau khi sửa tầm melee, **2 Striker** | **84** | 1,10 | 2 | **0%** |
| Sau khi chốt 1 mục tiêu, 2 Striker | 77 | 1,27 | 2 | 0% |
| PHỄU **Normal** *(sau khi bỏ Easy)*, 2 Striker | 109/185 = **59%** | 1,61 | **6** | **14%**, vượt TB 1,80 |
| THÁC 3 nút — *chồng lấn* | 186/192 = **97%** | **2,96** | 6 | **34%**, vượt TB 2,11 |
| THÁC 2 nút, buồng 3+3 | 125/193 = 65% | 1,20 | 3 | **1%** · road **19%** ⚠️ |
| **THÁC 2 nút, buồng 3+4** | 113/196 = 58% | 1,75 | 4 | **14%**, vượt TB 1,13 |
| THÁC @ Nightmare, **chưa có prep** — 🔴 THUA | 172/176 = 98% | 3,06 | 6 | 21%, vượt TB 1,14 |
| **THÁC @ Nightmare, có 10s prep** — 🔴 THUA | 222/231 = **96%** | **3,83** | **8** | **41%**, vượt TB **2,11** |

> ❌ **Một kết luận sai đã ghi ở đây, nay bác bỏ.** Dựa trên hai số 1,13 và 1,14, tài liệu
> từng viết *"hình học quyết định mức vượt — trần cứng, không núm cân bằng nào chạm tới"*.
> Điểm thứ ba là **2,11**, gần gấp đôi.
>
> Lý do hai số đầu giống nhau không phải vì có trần, mà vì **ván kết thúc trước khi gặp wave
> dày**. Thêm 10 giây chuẩn bị → sống lâu hơn 31% → gặp đúng những đợt đông nhất.
>
> Phát biểu đúng: **hình học đặt trần TRÊN** (buồng 4×4 = 16 ô, đỉnh 8 vẫn còn xa trần), còn
> mức vượt thực tế do **mật độ địch thật sự gặp phải** quyết định. Hai điểm dữ liệu trùng
> nhau không đủ để gọi là trần — đó là suy diễn từ mẫu bị cắt cụt.

> **Dự báo cho Giai đoạn 2** — tính lại trên số đầy đủ:
>
> ```
> R = 1,60 + 1,00 × 0,41 × 2,11 = 2,47 điểm/giây   →   gãy sau 72/2,47 = 29 giây
> ```
>
> Và cán cân nguồn stress ở Nightmare:
>
> | | Điểm cả ván |
> |---|--:|
> | N2 (nền) | 222 × 1,60 = **355** |
> | N1 (tuyến rò) | 0,41 × 222 × 2,11 = **192** |
>
> **N1 = 54% của N2.** Ở màn khó nhất, "tuyến rò" cuối cùng đã đứng ngang hàng với đồng hồ
> nền — đúng vai trò đã sửa cho nó ở [mục 04](#04--bốn-nguồn-tăng): không phải khối lượng
> lớn nhất, nhưng đủ nặng để *thời điểm* nó đổ xuống quyết định trận đấu.
>
> Gãy sau 29 giây trên một trận 150 giây+ nghĩa là người chơi **phải xoay vòng 4–5 lượt**.
> Con số đó rơi ra từ bản đồ, không phải từ một hằng số ai đó chọn.

> **Ba dòng THÁC là một thí nghiệm sạch, và kết quả đi ngược trực giác.**
>
> Bản **chồng lấn** — một lỗi hình học — cho gameplay mạnh nhất từng đo (97% / 34%). Không
> phải vì có ba nút, mà vì ba khối chồng nhau tạo **một vùng sàn liên tục lớn**.
>
> Sửa cho ba nút rời ra bằng cách bỏ một tầng thì sàn tụt 27 → 18 ô, road xuống **19%** (dưới
> sàn 20%) và áp lực sụp còn 1%. `OpenBlock` carve khối `width × width`, nên bỏ một tầng
> không mất 1/3 diện tích — **nó mất 1/3 chỗ đứng của melee**, và chỗ đứng chính là thứ
> quyết định bao nhiêu địch lọt vào vùng 3×3.
>
> Nới buồng sau lên 4 (sàn 25 ô) đưa áp lực về **14%** — ngang PHỄU ở cùng mức Normal, với
> sàn ít hơn 9 ô.
>
> **Bài học: thứ điều khiển N1 không phải số nút thắt mà là diện tích sàn liên tục tại điểm
> hội tụ.** Cần N1 mạnh hơn thì nới buồng, đừng thêm nút.

Đỉnh nhảy 3 → 5 là chỗ đáng nhìn nhất: **hai địch đứng chờ ngoài ô chặn vốn hoàn toàn vô
hình với phép đo cũ.** Đúng thứ tạo cảm giác bị vây, và đúng thứ định nghĩa cũ không thấy.

> **Ép từ 4 xuống 2 lính gần như không đổi con số.** 1,78 → 1,83 · 8% → 9%. Nghĩa là áp lực
> **bị chặn trên bởi hình học bản đồ, không phải bởi số quân**: hành lang rộng 1 ô chỉ có
> vài ô kề là đường, nên tối đa mấy con vây được một người là do bản đồ quyết định.
>
> Hệ quả thiết kế, và nó thuận: **muốn N1 nặng thì phải đứng ở nút thắt.** Buồng rộng 3–4 ô
> là chỗ duy nhất đủ ô kề để dồn đủ địch — đúng chỗ thiết kế muốn người chơi phải cân nhắc.
> Nếu sau này cần N1 mạnh hơn nữa thì cách đúng là **nới buồng**, không phải nâng hệ số.

> ⚠️ **Dòng cuối bảng: giao chiến giảm 46% (157 → 84 giây), N1 về lại 0%.**
>
> Lần này **không phải phép đo hỏng** — mà Striker vừa được trả lại tầm 3 ô nên xoá địch
> **trước khi chúng kịp dồn tới**. Hai Striker cạnh nhau chồng hoả lực lên cùng một hành
> lang, và không ai bao giờ bị vây.
>
> **Không kết luận gì thêm từ ván này.** Bốn thứ đổi cùng lúc: tầm melee, đội hình (2 Striker
> ngưỡng 2 thay vì Defender ngưỡng 4), vị trí đặt, và roll bản đồ. Một ván không tách được
> bốn biến.
>
> Nhưng nó chỉ ra một điều về **cách đo**: N1 là dấu hiệu *"bạn đang thua ở chỗ này"*.
> Trong một ván thắng gọn thì 0% là **đúng**, không phải lỗi. `STRESS_PER_OVERLOAD` không
> thể chốt bằng ván thắng dễ — phải đo ở màn thật sự căng.

> 🔧 **Chốt "một mục tiêu" xác nhận bằng độ dài trận, không bằng cột giao chiến:**
> cùng 2 Striker, gần như cùng vị trí — **44 giây → 84 giây**. Melee trở lại đúng sức.
>
> Cột "giây giao chiến" thì gần như đứng yên (84 → 77) và **đó là khuyết điểm của chính
> phép đo**: nó là TỔNG cộng dồn qua số lính và độ dài trận, nên một trận dài gấp đôi với
> áp lực loãng bằng nửa sẽ ra cùng con số. Đã thêm mẫu tổng để in kèm **% thời gian có địch
> trong vùng** — đó mới là số so sánh được giữa hai ván, và là số Giai đoạn 2 sẽ tune theo.
>
> *Lần thứ tư trong giai đoạn này thứ hỏng là cách đo, không phải thứ được đo.*

> 🔴 **Số thứ ba là lý do bước 1.6 tồn tại.** Nó bắt được N1 chết trước khi Giai đoạn 2 kịp
> viết một dòng — nếu bỏ qua, N1 sẽ được viết, được tune, và không ai hiểu vì sao đổi số
> chẳng thấy khác gì. Định nghĩa vùng melee đã sửa, xem [mục 03](#03--vùng-áp-lực-và-ngưỡng-chịu-đựng).

**Số phụ rơi ra, đủ để kiểm tra `STRESS_BASE_RATE`:** 110 giây giao chiến toàn đội trong một
màn 5 wave × 1,60 = **176 điểm**, chia cho ~3 người đang đánh. Người gánh nút thắt ăn phần
lớn nhất → gần cạn thanh khi màn kết thúc. Đúng cỡ mong muốn: một màn dài *là* căng, nhưng
chưa gãy nếu không có N1/N3 cộng thêm.

**Chênh 77% ↔ 57% là chênh giữa dụng cụ và kết quả**, và đúng bằng chênh diện tích: bán kính
quy hoạch quét ô vuông 5×5 = 25 ô, tầm bắn thật của Moon có 8. Bộ chọn bệ lạc quan gấp ba
lần về diện tích — đó chính là lý do phải đo lại bằng `rangeOffsets` thay vì tin con số sẵn có.

**Hai quyết định trong cách đo, cả hai đều để tránh đo nhầm dụng cụ:**

`TOWER_SLOT_COVERAGE_RADIUS` **không phải tầm bắn.** Nó là hình vuông dùng để *chọn* vị trí
bệ; `rangeOffsets` thật thì không vuông. Lấy con số của bộ chọn rồi gọi nó là "độ phủ" là
đo cái cưa thay vì đo tấm ván — đúng loại lỗi đã sinh ra hằng số 12 giây. Nên phép đo dựng
lại từ `rangeOffsets` của xạ thủ **hẹp nhất** trong roster, thử cả 4 hướng, lấy hướng tốt nhất.
Con số ra là **trần lý thuyết**: người chơi có ít quân hơn số bệ, nên không bao giờ chạm tới.
Trần thấp mới là tín hiệu — nó nói bệ đặt sai chỗ.

`TDPressureProbe.Count()` **không phải mã đo vứt đi.** Nó tính đúng *vùng áp lực* ở
[mục 03](#03--vùng-áp-lực-và-ngưỡng-chịu-đựng) — melee theo `rangeOffsets`, ranged theo 8 ô kề
— tức là chính đại lượng N1 và N2 sẽ đọc ở Giai đoạn 2. Viết nó bây giờ để **con số có trước
hằng số phụ thuộc vào nó**, thay vì ngược lại.

Mẫu có 0 địch bị bỏ, không tính vào trung bình: đứng gác chỗ vắng là miễn phí theo thiết kế
N2, gộp vào sẽ kéo trung bình xuống và giấu mất tải thật lúc giao chiến.

#### Hai nguyên mẫu, không phải ba — XƯƠNG SỐNG đã bị xoá

| | nút | sàn buồng | giãn cách/cần | dư | runway | Fits |
|---|--:|--:|:--:|--:|--:|:--:|
| **PHỄU** | 3 | **34 ô** | 6,44 / 5,50 | 0,94 | 4,8 ô | ✅ |
| **THÁC** | 2 | 25 ô | 5,80 / 5,50 | 0,30 | 6,1 ô | ✅ |
| ~~XƯƠNG SỐNG~~ | 3 | 27 ô | **3,20 / 5,00** | −1,80 | 4,8 ô | ❌ |

**Xoá hẳn, không phải tắt.** Ba tầng của nó trải trên 40% trục dòng chảy → chỉ **3,2 ô**
giữa hai tâm liên tiếp trong khi cần 5,0. Trên grid 21×9 cố định camera nó **không bao giờ**
resolve được, luôn rơi về PHỄU. Dữ liệu không bao giờ chạy tới chỉ mời người sau "sửa cái
fallback"; lý do và con số đã ghi ngay chỗ nó từng nằm. Cần map **cao** thì thêm lại.

*(Lý do cũ — cổng gắn theo Manhattan nên mọi cổng đều gần nút đầu — vẫn đúng, nhưng nó chỉ
là nửa sau. Nửa trước là hình học: ba tầng không nhét vừa trục 16 ô.)*

**Biên an toàn của PHỄU đã nới**: u ngoài 0,35 → 0,30, dư từ **0,1 → 0,94 ô**. Giãn cách đổi
theo mỗi lần đặt cổng, nên dư 0,1 nghĩa là một roll xấu là cả hình dạng **âm thầm** tụt xuống
THÁC mà không ai biết.

#### Hai hình dạng khác nhau về chất, không phải biến thể

```
PHỄU                             THÁC
 ▣╲                               ▣ ──→ ▣▣ ──→ đích
   ╲▣▣ ──→ đích                  nhỏ    lớn
 ▣╱                              nhường đất theo chiều sâu
 hai luồng chia lửa rồi dồn      một luồng, hai lần trụ
```

Sàn PHỄU rộng hơn (34 vs 25) nhưng runway ngắn hơn (4,8 vs 6,1) — đúng đặc tính: PHỄU dồn
về **một trận quyết định** gần đích, THÁC để lại **đường lùi**. Cả hai đều là thứ hệ tinh
thần cần.

#### ✅ CHỐT A — ĐẠT

> Chơi thử map mới **bằng gameplay hiện tại, chưa có stress gì cả**.

| Câu hỏi | Kết quả |
|---|:--:|
| Nút thắt có **đáng đặt quân** không, hay chỉ là hành lang rộng hơn? | ✅ **Đáng — đứng ở đó khác hẳn chỗ khác** |
| **Rút lui có tổ chức** được không? Bỏ buồng ngoài, lùi về buồng trong? | ✅ **Có** |

**Nền đã vững, đi tiếp sang Giai đoạn 2.**

Đường đi tới đây không thẳng, và điều đáng ghi nhất là **cả hai câu chỉ trả lời được sau khi
sửa một thiếu sót chẳng liên quan gì tới tô-pô**: không có thời gian chuẩn bị trước wave 1.
Hai lần đo đầu ở Nightmare đều thua trước khi câu hỏi kịp đặt ra — không phải vì tô-pô sai,
mà vì trận đấu chưa từng bắt đầu một cách công bằng.

> **Cảnh báo cho người đọc sau:** `Level Config` level 0 hiện để **Nightmare** để phục vụ đo
> đạc, không phải cân bằng thật cho màn 1-1. Bản gốc ở `Level Config.asset.bak`. Trả về
> `difficulty: 0` sau khi Giai đoạn 2 tune xong.

---

### GIAI ĐOẠN 2 · Lõi stress, chưa có UI đẹp — 3 ngày

| # | Việc | Xong khi | |
|:--:|---|---|:--:|
| 2.1 | Model stress — **C# thuần, nằm trong model của operator, không đẻ `.api` mới** | 4 nguồn tăng + 7 nguồn giảm + hệ số 3 trạng thái | ✅ |
| 2.2 | Ý chí suy ra + 3 điều chỉnh trong trận | Ra đúng [bảng 8 operator](#kết-quả-với-roster-thật) | ✅ |
| 2.3 | **`Tools ▸ TD ▸ Validate Stress Model`** — cùng mẫu `TDBalanceValidator`: công thức stress · ngưỡng kích hoạt đúng 1 lần · Ý chí đúng [bảng roster](#kết-quả-với-roster-thật) · ~~phân phối Quyết tử qua 10.000 lần chạy · lính gãy nhả hết địch đang chặn~~ *(hai mục cuối thuộc Giai đoạn 3)* | 0 failure | ✅ |
| 2.4 | Nối vào `TDGameEventBus` + registry tick | **Không thêm một `Update()` nào** | ✅ |
| 2.5 | **Debug overlay thô** — text `73.4 / STRESSED` nổi trên đầu operator | Xấu cũng được | ✅ |

#### Trạng thái 2.4 — nối dây, không đẻ hệ thống

Giữ được ràng buộc *"không thêm một `Update()` nào"*, và không thêm sự kiện nào vào
`TDGameEventBus` cả. Mỗi mẩu dữ liệu lấy từ nơi **đã** biết nó:

| Dữ liệu | Lấy từ |
|---|---|
| Địch trong vùng · ngưỡng | `TDPressureProbe` — viết ở [1.6](#16--ba-số--dụng-cụ-đo), giờ đổi đích đến |
| Đồng đội Calm kề | `TDOperatorRegistry` — nơi duy nhất biết ai đứng đâu |
| Aura boss | `TDEnemyRegistry`, qua probe |
| Nhịp tick | `TDOperatorView.Update()` **đã có sẵn** |
| Wave sạch | vòng lặp wave **đã có sẵn**, gọi thẳng registry |

Hai quyết định nhỏ đáng ghi:

**N3 phải bắn TRƯỚC `OnRemove`.** Ngược lại thì registry đã bỏ ô đó rồi và hàng xóm không
bao giờ biết có người vừa ngã.

**Wave relief gọi thẳng từ vòng lặp**, không qua event mới: vòng lặp đã sở hữu *"một wave
vừa xong"*, registry đã sở hữu *"ai còn đứng"*. Thêm một bên thứ ba mang tin giữa hai bên
chỉ tạo thêm một chỗ để chúng bất đồng — dự án này đã trả giá cho "hai nguồn sự thật" đủ
nhiều lần rồi.

#### Trạng thái 2.5 — overlay tự cài

`TDMoraleDebugOverlay` tự tạo qua `[RuntimeInitializeOnLoadMethod]`: không prefab, không gán
scene, không phải nhớ thêm gì. Hiện `73.4 STRESSED · 12s` theo màu ba trạng thái.

Con số thứ hai là **giây tới khi gãy**, không phải điểm/giây — vì đó là đại lượng duy nhất
người chơi cảm nhận được, nên cũng là đại lượng phải tune trên đó.

#### Trạng thái 2.1 · 2.3

`TDOperatorMorale` là C# thuần — không MonoBehaviour, không `static api`, không `Update`.
Ràng buộc đó của kế hoạch hoá ra là thứ đắt giá nhất: **toàn bộ luật chạy được từ một menu
editor, không scene, không play mode, không prefab.** Mọi con số kiểm được trước khi UI tồn tại.

`TDMoraleValidator` (~30 assertion) **viết cùng ngày và chạy trước khi tick**. Bốn assertion
bảo vệ quyết định thiết kế chứ không phải số học:

| | Bảo vệ điều gì |
|---|---|
| `SPIKE_UNMULTIPLIED` | N3 không nhân hệ số — 30 điểm ×2 ở Stressed là xoá sổ tự động |
| `ALLY_CANNOT_EAT_AURA` | đồng đội bình tĩnh không làm boss bớt đáng sợ |
| `ALLY_CAP` | cụm lính Calm không hồi nhanh hơn vòng lặp lõi |
| `DEATH_KEEPS_STRESS` | *"để nó chết cho sạch rồi gọi lại"* không được là nước đi tối ưu |

**Chạy lần đầu: 1 lỗi** — `SecondsToBreak` báo 55,4 giây thay vì 45. Lộ ra vì validator đo
cùng một quãng bằng **hai đường**: công thức chiếu và tích phân `Tick()` 0,05s/bước.

Nguyên nhân: hệ số của mỗi dải lấy bằng `StateOf(cursor)` — tức state ở **biên dưới**. Tại
`cursor = 33`, `StateOf(33)` trả `Calm`, nhưng đoạn 33→66 phải vượt ở `Steady ×1,5`. Sửa bằng
cách gắn hệ số **kèm theo biên** thay vì suy lại từ con trỏ.

*Loại lỗi: lấy giá trị tại một điểm làm đại diện cho cả khoảng.* Đọc thì xuôi, và không có
đường đo thứ hai thì không cách nào thấy.

Sau khi sửa: **0 lỗi**, và bốn con số [mục 10](#bảng-thời-gian-tham-chiếu) khớp tuyệt đối —
45,0 · 60,0 · 90,0 giây, và 9,5 giây cho ca 6 địch + aura boss.

#### Trạng thái 2.2 — Ý chí ra đúng bảng

`enduranceRatio` và `baseResolve` là **property suy ra trên `OperatorData`**, cùng khuôn với
`deployZone` đã có. Ba điều chỉnh trong trận nằm ở `TDOperatorMorale.ResolveChance` vì chúng
cần trạng thái runtime (`Setbacks`, đồng đội quanh).

| | cost | E | Ý chí |
|---|--:|--:|--:|
| Tart | 22 | 666,67 | **43%** |
| Defender | 20 | 300,00 | **40%** |
| **Knight** | **15** | 15,00 | **27%** |
| Layla · Ace · Striker | 18 | 4,4 · 4,2 · 3,9 | **21%** |
| Moon | 20 | 1,09 | **15%** |
| Ginger | 16 | 0,85 | **14%** |

**Tính vuông góc giữ được, và validator canh nó:** Knight **rẻ nhất roster** mà ý chí 27% —
cao hơn Moon (20 vàng, 15%) và Striker (18 vàng, 21%). Nếu một ngày ai đó chỉnh chỉ số làm
Ý chí bám theo giá, `RESOLVE_ORTHOGONAL` kêu ngay. Bảng cũng đối chiếu với **config sống**,
nên sửa `hp` hay `damage` mà quên cập nhật tài liệu là fail — Ý chí là **suy ra**, nên đổi
chỉ số *là* đổi Ý chí.

> **Một trở ngại đáng ghi:** validator không dùng được `TDFlyweightOperatorDataSettings.api`
> — nó đi qua `TDResourceObject`, một MonoBehaviour có danh sách gán trong scene, nên trả
> `null` ngoài play mode. Đây đúng là chỗ ghép nối đã ghi ở [bước 0.4](#-04-asmdef--đã-bỏ-khỏi-kế-hoạch)
> (*"cả 6 SO config gọi `TDResourceObject`"*) gặp lại trong thực tế.
>
> Điều đáng chú ý: **model tinh thần thuần túy nên test được ở bất cứ đâu**; thứ duy nhất
> cần lách là *đọc con số*. Validator load thẳng qua `AssetDatabase`. Nếu model cũng bị ghép
> vào Services như config thì cả bước 2.3 đã không tồn tại được.

> **2.5 LÀ CHỦ ĐÍCH, KHÔNG PHẢI LƯỜI**
>
> Cần tune 4 núm trên **số đọc được**, trước khi đổ 4–5 ngày vào icon, poof, âm thanh.
> Làm icon đẹp rồi mới phát hiện `STRESS_BASE_RATE` sai là lãng phí gấp đôi.

#### B‑0 · Hai thứ phải xong trước khi Chốt B có nghĩa

Phát hiện khi chuẩn bị chạy Chốt B. Cả hai đều **im lặng** — không exception, không assertion đỏ.

**① `Morale.OnRetreat()` chưa bao giờ được gọi trong game.** `DoRetreat()` gọi `Destroy()`, morale
chết theo GameObject, cắm lại là `new TDOperatorMorale()`. Rút quân = **xoá sạch stress, miễn phí** —
lại còn hoàn 50% vàng. `STRESS_RETREAT_RELIEF = 70` và `COUNTDOWN = 8.0` chỉ tồn tại trong validator.

> Nguy hiểm ở chỗ nó khiến **Chốt B đạt vì lý do sai**. Câu hỏi của B là *"tôi có bao giờ rút một
> con còn gần đầy máu ra không?"*. Với rút quân là nút xoá stress miễn phí thì câu trả lời hiển
> nhiên là có — và không chứng minh được gì về việc stress có phải trục thứ hai thật hay không.

**② Slot vứt bỏ `OperatorData` rồi năm chỗ khác tra lại bằng archetype.** 8 dòng config, 5 giá trị enum:

| Tên | operatorType cũ | Thực tế chơi bằng chỉ số của |
|---|---|---|
| Ace | 2 (Striker) | **Striker** — 700hp thay vì 800, 80dmg thay vì 70 |
| Layla | 2 (Striker) | **Striker** — 700hp thay vì 750 |
| Tart | 1 (Defender) | **Defender** — 3000hp thay vì 4000, Ý chí 40% thay vì 43% |

`GetData()` là `Find(o => o.operatorType == type)` — khớp đầu tiên thắng. Ba trên tám operator
chơi cả trận bằng chỉ số của người khác, nên cũng bằng **Ý chí của người khác**.

> Validator vẫn xanh vì nó tra roster bằng **`operatorName`**, còn game tra bằng **`operatorType`**.
> Hai đường truy cập, hai kết quả, không bên nào biết bên kia tồn tại.

**Hướng sửa đầu tiên đã bị bác, và bác đúng.** Phản xạ ban đầu là đẩy `Ace(5)` `Layla(6)` `Tart(7)`
vào `OperatorType`. Nó **chữa được triệu chứng nhưng làm hỏng thêm cái enum**: `OperatorType` vốn
đang gánh hai việc — *archetype* (quyết định `deployZone`, chọn VFX) và bị dùng nhầm làm *identity*.
Thêm giá trị là chính thức hoá việc thứ hai, và từ đó **mỗi operator mới bắt buộc phải sửa một enum
+ hai switch + một property suy ra** — trong khi Giai đoạn 7 của chính kế hoạch này là *nội dung và
cân bằng*, nơi thêm operator phải là thao tác **chỉ chạm dữ liệu**.

Gốc thật sự nằm chỗ khác: **`TDTowerSlotInfo` được dựng từ đúng dòng `OperatorData`, rồi vứt dòng đó
đi và chỉ giữ lại enum** — sau đó *năm* chỗ gọi `GetData(operatorType)` để lấy lại. Không chỉ chỉ số
sai: `TDDeployController` và `TDOperatorSelectionView` vẽ **vùng đánh của người khác**, nên cái UI
hứa và cái code làm là hai thứ — đúng loại lỗi đã trả giá ở bước 1.6.

> **Mang theo tham chiếu không sửa phép tra — nó xoá nhu cầu phải tra.**
> Và `GetData(OperatorType)` bị **xoá hẳn**, không phải sửa: một khẩu súng tự bắn vào chân mà vẫn
> biên dịch được là khẩu súng có người cầm lại.

Việc đã làm:

| | Việc |
|:--:|---|
| B‑0.1 | `TDTowerSlotInfo` mang thẳng `OperatorData`. **Xoá `GetData(OperatorType)`** · 5 call site chuyển sang dùng dòng có sẵn · `OperatorType` trả về đúng nghĩa **archetype**, 5 giá trị |
| B‑0.2 | **`TDOperatorRoster`** — morale thuộc về *operator*, không thuộc GameObject. Sống qua `Destroy`. Khoá là **chính dòng `OperatorData`**, không phải enum (archetype dùng chung) cũng không phải tên (khoá *là* vật thì không lệch khỏi vật được) |
| B‑0.3 | `DoRetreat` → `OnLeftField(voluntary: true)` = −70 + cooldown 8s · `Die()` → `voluntary: false` = giữ nguyên mọi điểm, không cooldown |
| B‑0.4 | Card khoá khi operator đang trên sân **hoặc** còn cooldown. Turret không bị ảnh hưởng — luật một-lượt là về *người*, không phải ụ súng |
| B‑0.4b | **`Button.interactable` không phải một cái khoá.** Xem ghi chú dưới |
| B‑0.5 | Cooldown ngoài sân đếm nhờ `TDDeployController.Update()` **đã có sẵn** — giữ nguyên ràng buộc "không thêm `Update()`" của 2.4 |
| B‑0.6 | `UNIQUE_OPERATOR_IDENTITY` (không hai dòng trùng tên) và `ARCHETYPE_ZONE` (`blockCount == 0` ⇔ `TowerZone`) — archetype giờ chỉ còn quyết định **một** thứ, nên thứ đó phải được canh |

> #### B‑0.4b · `Button.interactable` không phải một cái khoá
>
> Lần thử đầu, card vẫn bấm được dù đã `SetInteractable(false)`. Nguyên nhân: deploy bar khởi động
> deploy bằng **`EventTrigger` / `PointerDown`** (chủ ý — để kéo thẳng từ card ra bản đồ không cần
> nhấc tay). `Button.interactable = false` chặn `onClick` **của chính Button** và làm xám graphic,
> nhưng EventSystem **vẫn giao `PointerDown` cho mọi handler trên object đó** — `EventTrigger` là
> một handler khác. Card trông tắt mà vẫn sống.
>
> Và nó **không phải lỗi mới**: cổng chặn vàng vẫn luôn thủng đúng như vậy. `SpendGold(cost)` chỉ
> chạy trong `OnPlaceTowerSuccess`, tức **sau khi unit đã được tạo**, và giá trị trả về `false`
> bị bỏ đi — qua được cái nút là deploy miễn phí.
>
> Sửa ở hai tầng, và tầng dưới mới là tầng thật:
> - **`TDPlaceTowerControl.CheckPlaceTower`** — điểm cuối trước khi một unit tồn tại, nên là chỗ
>   duy nhất luật thực sự đúng. Chặn vàng ở funnel chung, chặn `CanDeploy` ở nhánh operator.
> - **`CanSelect(gold)`** — *một* biểu thức, hai nơi đọc: cái làm xám card và cái chặn cú bấm.
>   Trước đó là hai biểu thức ở hai file, và chúng lệch nhau ngay khi một cái thôi là cổng chặn.
>
> *Loại lỗi: nhầm phản hồi thị giác là sự thực thi.* Thứ trông như bị khoá không tự nó khoá gì cả.

> #### B‑0.7 · Chết phải đắt hơn rút, trên **cả hai** trục
>
> Bản đầu cho cái chết **không cooldown**, với lý lẽ "giữ nguyên stress là đủ phạt rồi".
> Không đủ. Giữ stress chỉ cắn được người **chết lúc đang căng** — một operator bị giết sớm
> lúc còn 5 điểm thì **không trả gì cả**, lại còn quay lại sân **ngay lập tức** trong khi người
> được rút chủ động vẫn đang đợi 8 giây.
>
> Tức **chết là lối ra nhanh hơn rút** — đúng cái kẽ hở mà `DEATH_KEEPS_STRESS` sinh ra để chặn,
> chỉ là mở lại ở **trục thời gian** thay vì trục điểm số. Một assertion canh đúng một chiều thì
> chiều còn lại vẫn trống.
>
> `STRESS_DEATH_COUNTDOWN = 16.0` (2× retreat) — mất người tốn hơn trọn một `waveInterval`.
> Tạm thời, mang hình dáng của **chết vĩnh viễn** ở bước 3.6 mà nó sẽ trở thành.
> Assertion mới `DEATH_COSTS_MORE` canh cả hai chiều, kể cả ca chết lúc stress bằng 0.

#### Ở Normal, **trung bình** là âm — nhưng trung bình là thống kê sai để hỏi

Đo từ chính ván vừa chơi (`engaged 201/421 = 48%`, `quá tải 11% thời gian, dư 1,64`):

| | điểm/giây trung bình |
|---|--:|
| N2 (giao chiến) | 0,48 × 1,6 = **+0,768** |
| N1 (quá tải) | 0,11 × 1,64 = **+0,180** |
| Nghỉ (`STRESS_IDLE_RELIEF`) | 0,52 × 0,6 = **−0,312** |
| Đồng đội Calm kề (1 người) | 0,48 × 0,4 = **−0,192** |
| | **+0,444 /giây** |

Một operator đứng ~210 giây tích được **~93 điểm**, trong khi thưởng wave sạch là **8 × 15 = 120**.
Trung bình cả trận: **âm**.

> 🔴 **Và từ đó tôi kết luận "ở Normal stress không bao giờ chạm 100" — sai.**
>
> Trung bình là thống kê sai để hỏi về một đại lượng mà **toàn bộ mục đích thiết kế của nó là
> dồn cục**. `mean 1.63` đi kèm `peak 5`, và chính cái peak mới làm gãy người.
>
> Knight (`blockCount 2` → ngưỡng 3) gặp peak 5 ⇒ dư 2 ⇒ N1 = 2,0/giây. Cộng N2:
>
> | Dải | tốc độ ròng | giây |
> |---|--:|--:|
> | Calm ×1,0 | 3,2 | 10,3 |
> | Steady ×1,5 | 4,8 | 6,9 |
> | Stressed ×2,0 | 6,4 | 5,3 |
> | | | **≈ 22,5 giây** |
>
> **Hơn hai mươi giây bị vây nặng là gãy, ngay ở Normal.** Thưởng wave sạch không cứu được, vì
> nó tới *sau khi* wave kết thúc — còn cú gãy xảy ra *giữa* wave.
>
> *Loại lỗi: lấy trung bình làm đại diện cho cả khoảng.* Đúng cùng một họ với lỗi
> `SecondsToBreak` ở [2.3](#trạng-thái-21--23) — ở đó là lấy giá trị tại một điểm đại diện cho
> khoảng; ở đây là lấy giá trị trung bình. Cả hai đều làm phẳng đúng cái thứ mà thiết kế cần gồ ghề.

Điều **vẫn đúng**: đừng hạ `STRESS_WAVE_CLEAR_RELIEF`. Trạng thái dừng ở Normal là an toàn, nên
stress ở đó đến **từ biến cố, không từ bào mòn** — và đó nhiều khả năng là thiết kế đúng cho màn
dạy luật. Vế tích luỹ vẫn thiếu N4 (aura boss còn stub) và **Bầy đàn** ở Giai đoạn 5; hạ số hồi
phục bây giờ là bù cho một cái lỗ, rồi Giai đoạn 5 phạt hai lần.

Kết quả: `[TDMoraleValidator] PASS — 0 failures` · `[TDBalanceValidator] PASS — 600 cases, 0 failures`.

Ngữ cảnh của operator ngoài sân là `secondsSinceHit = float.MaxValue`, không phải `default`. Để
mặc định 0 thì một operator suy sụp **kẹt suy sụp vĩnh viễn**, vì hồi phục lúc suy sụp gác đúng
lên biến đó — mà ngoài sân là chỗ yên tĩnh nhất có thể có.

---

#### 🛑 CHỐT B — Bài kiểm tra A4

> Chơi 5–10 ván với debug overlay. Tự hỏi:
>
> ### "Tôi có bao giờ rút một con lính còn gần đầy máu ra không?"
>
> - **Có** → stress và HP độc lập thật. Đi tiếp.
> - **Không** → **nó là thanh máu thứ hai. Dừng lại.** Không sửa bằng cách chỉnh số —
>   vấn đề nằm ở chỗ nguồn stress đang trùng với nguồn sát thương.

> ### ✅ ĐẠT — chơi ở `Normal` (`Stage_1-1 → LevelIndex 0 → difficulty 0`)
>
> Kết luận của người chơi: **stress và HP độc lập.** Có lúc phải rút một operator còn nhiều máu,
> vì lý do không liên quan gì tới thanh máu của nó.
>
> Đạt ở **Normal** — độ khó thấp nhất còn lại, và đúng chỗ mà phép tính trung bình ở trên bảo
> rằng hệ này *không nên* cảm thấy được. Đó là bằng chứng thực nghiệm rằng thứ làm gãy người là
> **cụm áp lực**, không phải tốc độ nền: `mean 1.63` không gãy ai, `peak 5` giữ trong 22 giây thì có.
>
> Mở khoá Giai đoạn 3.

---

### GIAI ĐOẠN 3 · Suy sụp · Rescue · Quyết tử — 3–4 ngày *(+1 dự phòng)*

| # | Việc | Ghi chú |
|:--:|---|---|
| 3.1 | Trạng thái **suy sụp**: không chặn, không đánh, không tự rút, sát thương ×3 | ✅ — xem ghi chú dưới |
| 3.2 | **Rescue**: nút thứ hai góc dưới diamond panel, [3 luật ẩn/mờ/sáng](#ba-luật-hiển-thị-nút-rescue) | ✅ — xem ghi chú dưới |
| 3.3 | Luồng chọn mục tiêu: 1 mục tiêu → cứu luôn · ≥2 → nhấp nháy chọn | ✅ |
| 3.4 | **SP**: +1/giây khi deploy, 50 cho rescue, ngừng tích khi rút về | |
| 3.5 | **Quyết tử**: nút hiện theo xác suất = Ý chí, người chơi tự bấm | |
| 3.6 | Chết vĩnh viễn + roster hữu hạn + van "wave sạch −15" | |
| 3.7 | Nút **RÚT KHỎI CHIẾN DỊCH** — 2 lựa chọn, nhớ `EnsureUnpaused()` | |

#### Ghi chú 3.1 — suy ra, không latch

Bốn hệ quả của suy sụp đều **đọc live từ `Morale.IsBroken`**, không có cờ riêng. §06 cho phép
operator bị bỏ mặc **tự hồi chậm tại chỗ**, nên nếu latch thì phải viết thêm một bước "gỡ suy sụp"
đối xứng — và mọi cặp set/unset là một chỗ để hai nửa lệch nhau.

Chỉ đúng **một** việc xảy ra tại khoảnh khắc chuyển: thả hết địch đang bị giữ. Dùng chung đường
với cái chết (`ReleaseBlockedEnemies`), vì địch không cần biết **vì sao** bức tường trước mặt nó
thôi làm tường.

> **Đếm phải reset bằng tay ở đây.** `ForceUnblock()` không gọi `OnEnemyUnblocked`, nên khi entry
> của ô vẫn còn (khác với chết — xoá hẳn entry), một cái `count` cũ sẽ khiến ô đó **"đầy" vĩnh viễn**.

#### 🔴 Ghi chú 3.1 — ×3 lúc đầu **không thể chạm tới**

Nối xong ×3 rồi truy ngược mới thấy: trong cả game chỉ có **một** nguồn sát thương lên operator,
và nó gác sau `if (m_IsBlocked)`. Suy sụp = không chặn ⇒ không ai đánh ⇒ **×3 không nhân vào gì**.

```
gãy → thả địch đang giữ → CanBlock = false → không ai chặn
     → không ai đánh → suy sụp là chỗ AN TOÀN TUYỆT ĐỐI
```

Trạng thái đáng lẽ là ngõ cụt đáng tiêu một Rescue lại hoá ra **chỗ nghỉ**. Hồi lại sau ~5 giây
yên tĩnh, không rủi ro gì.

**Chọn hướng A:** địch đi ngang ô có người suy sụp thì **đánh một nhát rồi đi tiếp**. Giữ nguyên
"không chặn" của §06, mà vẫn khiến suy sụp chết người.

> Một nhát **mỗi con đi qua**, không phải sát thương theo giây. Nên mối đe doạ **tỉ lệ với số
> địch còn đang tới**: gãy giữa wave là chí mạng, gãy sau con cuối cùng thì sống. Đó đúng là
> phán đoán mà Rescue sẽ bắt người chơi phải cân — cứu ngay, hay để nó tự hồi.

*Loại lỗi: nối một hệ số vào một đại lượng chưa bao giờ chạy qua đó.* Hằng số có, đường dây có,
validator không kêu — và hiệu ứng bằng không.

#### 🔴 Ghi chú 3.1 — suy sụp chỉ kéo dài **một frame**

Câu hỏi *"nếu wave hết mà nó còn sống thì tự hồi để làm gì"* lôi ra một lỗi. Đo trên code lúc đó:

```
[Latch] vào suy sụp:   value=100.0  broken=True   state=Broken
[Latch] thoát sau 0.1s → value=99.9  state=Stressed
```

`IsBroken` là ngưỡng trần trụi `m_Value >= 100`. Hồi phục chạy 1,0/giây, nên tick đầu tiên sau
cửa sổ 5 giây kéo xuống 99,98 → **hết suy sụp ngay lập tức**. Rồi một con địch trong vùng là
×2,0 đẩy về lại 100 trong nửa giây. Nó **nhấp nháy**, không kéo dài.

Mọi hệ quả của 3.1 nhấp nháy theo — thả địch, ×3, từ chối rút. Và `TickMorale` bắn **N3 spike
sang hàng xóm ở mỗi lần bật lại**, biến một cú gãy thành một tràng liên thanh.

> §05 định giá hồi phục là *"100 → 66 mất 34 giây yên ổn"*. Con số đó chỉ có nghĩa nếu operator
> **vẫn đang suy sụp** suốt 34 giây ấy. Bảng giá tồn tại, trạng thái để tính giá thì không.

**Sửa: latch có trễ.** Vào ở 100, ra ở **66** — không phải 99,99. Rescue không cần ngoại lệ:
100 − 50 = 50, dưới vạch ra, nên cùng một luật gỡ trạng thái. Mọi lệnh ghi giá trị đi qua đúng
một hàm `SetValue`, để latch không thể bị quên ở một trong ba chỗ ghi.

Đo lại sau khi sửa:

```
[Latch] suy sụp kéo dài 34.1s → thoát ở value=65.9 state=Steady
[Latch] bị đánh liên tục 30s → value=100.0 broken=True
```

Và **đó mới là lý do tự hồi tồn tại**:

| | Rescue | Tự hồi |
|---|---|---|
| Thời gian | tức thì | **39 giây** (5 chờ + 34 hồi) |
| Về mức | **50** | 66 |
| Điều kiện | 50 SP + đồng đội đứng kề | **5 giây liên tục không bị đánh** |

Điều kiện cuối là mấu chốt. Với [hướng A](#-ghi-chú-31--3-lúc-đầu-không-thể-chạm-tới), mỗi con
địch đi qua đánh một nhát và **reset cửa sổ 5 giây** — nên giữa wave, tự hồi không bao giờ khởi
động được. Nó chỉ tồn tại để suy sụp không thành **khoá cứng vĩnh viễn** khi hết SP và không có
ai đứng cạnh. 39 giây làm bia là cái giá thật, không phải reset miễn phí.

*Loại lỗi: một trạng thái được định giá kỹ lưỡng mà không ai kiểm nó sống được bao lâu.*

#### Ghi chú 3.1 — hoàn tiền trước khi hỏi

`TDOperatorRetreatControl` cộng lại nửa giá **rồi mới** gọi `DoRetreat()`. Vô hại suốt từ đầu vì
chưa bao giờ có ai từ chối. Suy sụp là người đầu tiên từ chối, và trả tiền trước biến nó thành
**máy in vàng**: bấm rút → nhận nửa giá → người vẫn đứng đó → bấm tiếp.

Đã đảo thứ tự và gác bằng `CanRetreat` — *một* biểu thức, đọc bởi cả `DoRetreat` lẫn chỗ hoàn
tiền, nên hành động và cái giá của nó không thể bất đồng.

---

#### Ghi chú 3.2 — nút nhân bản, không sửa prefab

`RescueButton` được **clone từ `RetreatButton` lúc chạy** rồi soi gương sang đầu đối diện:
`(-90, +90)` → `(-90, −90)`. Bản sao thừa hưởng khung, viền, icon, label miễn phí, và **không thể
lệch khỏi anh em của nó** khi ai đó đổi style cái diamond. Đúng yêu cầu *"không thêm prefab"*.

Vị trí không phải lựa chọn bố cục. Đối lập theo **trục dọc** là khoảng cách xa nhất hai vùng chạm
có thể có trên panel này. Trong game thời gian thực người chơi bấm bằng trí nhớ cơ bắp, và một cú
chạm nhầm **rút mất đúng người mình định cứu**.

Vẫn là `Mode.Retreat`, không thêm state — diamond có 4 đầu, cái này dùng 2. Thêm state chỉ nới
rộng đúng cái bề mặt đã sinh ra mấy con bug "diamond ma" từng phải sửa ở file đó.

**SP làm cùng lúc, vì ba luật hiển thị phụ thuộc vào nó.** Bản đầu đặt `Sp` **ngay trên
`TDOperatorMorale`** — vì morale là thứ *duy nhất* đã sống sót qua retreat, nên nó là chỗ tiện
tay. Tiện tay không phải là lý do đúng.

> 🔴 **SP là tài nguyên KỸ NĂNG, không phải một trường của tinh thần.** Chính §06 đã định giá nó
> như vậy: *"50 SP (kỹ năng thường = 100 SP)"*. Khi kỹ năng ra đời, code kỹ năng sẽ đọc và tiêu
> SP — mà code kỹ năng **không có việc gì phải thò tay vào mô hình stress**, và ngược lại một
> thay đổi về cách nạp SP không bao giờ được âm thầm động vào các nhịp của §04.
>
> Tách ra thành `TDOperatorSp` riêng, roster giữ **hai dictionary trên cùng một khoá** thay vì
> gộp thành một record: chúng là hai mối quan tâm không liên quan, chỉ tình cờ chung vòng đời.

Trần là **100** — giá một kỹ năng thường — chứ không phải 50. Nên một lần cứu tiêu **đúng nửa
lần kích hoạt kỹ năng**: cứu đồng đội luôn được trả bằng thứ đáng lẽ đã bắn ra.

| | |
|---|---|
| `SP_MAX` | 100 |
| `SP_PER_SECOND` | 1, **chỉ khi đang deploy và không suy sụp** |
| `SP_INITIAL` | **20**, cấp một lần lúc tạo — xem dưới |
| `RESCUE_SP_COST` | 50 |

> 🔴 **Không có `SP_INITIAL`, Rescue nằm ngoài tầm với ở đúng lúc cần nó.**
>
> Log một ván thật: trận dài **85 giây**, và **chưa ai từng chạm 50 SP**.
> ```
> Tart · 1 mục tiêu · SP  1.6/50 → chưa đủ     (cắm ở giây 55)
> Tart · 1 mục tiêu · SP 23.5/50 → chưa đủ
> GameOver
> ```
> Quân tiếp viện được cắm ra **đúng lúc mọi thứ đã hỏng** — tức muộn — và chính họ là người phải
> cứu. Người có SP thì đã gãy, người còn tỉnh thì vừa mới tới. Cùng một sự đảo ngược với
> [van chống chết chùm](#05--nguồn-giảm-và-hồi-phục): tài nguyên dồi dào khi không cần, cạn khi cần.
>
> 20 điểm khởi điểm đưa lần cứu đầu từ **50 xuống 30 giây**. Cấp lúc **tạo object**, không phải
> lúc deploy — roster trả về đúng một `TDOperatorSp` cho mỗi operator, nên cắm-rồi-rút không farm
> được. Không đụng `SP_PER_SECOND` hay `RESCUE_SP_COST`, nên quan hệ *"cứu = nửa kỹ năng"* của
> mục này và kinh tế SP của Giai đoạn 7 giữ nguyên.
>
> Assertion `SP_FIRST_RESCUE_SECONDS` canh **thời gian**, không canh con số — đó mới là đại lượng
> quyết định cơ chế có với tới được hay không.
| `RESCUE_RESCUER_STRESS` | +15 cho người cứu |
| `RESCUE_RESCUER_CEILING` | 90 — kẹp, **không** từ chối |

Mỗi bên tự thu phần của mình: `TDOperatorSp.Spend()` lấy SP, `TDOperatorMorale.PayRescueStress()`
lấy thần kinh. Và `Spend()` **chính là cái cổng** — một lời gọi hoặc lấy được tiền hoặc từ chối,
không phải kiểm tra đủ tiền rồi trừ ở hai bước tách rời có thể bất đồng.

> Kẹp thay vì từ chối là chỗ đáng ghi: người cứu đang ở 88 vẫn được ra tay, chỉ là hành động ấy
> không đẩy họ qua ngưỡng. Để nước đi **đúng đắn duy nhất** trong game trở thành thứ làm gãy chính
> mình thì hệ thống đang dạy người chơi đừng đi nước đó.

Còn thiếu, thuộc **3.3**: khi có ≥2 mục tiêu thì hiện đang chọn con **stress cao nhất** —
xác định được và gần như luôn trùng với lựa chọn của người chơi. Luồng nhấp nháy để chọn tay
là việc của 3.3.

---

#### Ghi chú 3.3 — chọn mục tiêu

Một mục tiêu thì **cứu luôn, không có bước xác nhận**: một màn hình hỏi lại cho quyết định chỉ có
đúng một đáp án là thuế đánh vào sự chú ý, đúng lúc người chơi không còn chút nào để trả.

Từ hai trở lên mới đưa lựa chọn về tay người chơi — vì **cứu ai chính là quyết định**. Tự chọn hộ
(bản 3.2 tạm chọn con stress cao nhất) là âm thầm tự động hoá khoảnh khắc thú vị nhất của cả hệ.

Nhấp nháy dùng lại `SelectionIndicator` **có sẵn trên prefab** — cái quad vàng dưới chân đã bị bỏ
không từ khi diamond thay vai trò chỉ báo lựa chọn. Không thêm asset, không thêm prefab.

| Chi tiết | Vì sao |
|---|---|
| Diamond **ẩn** suốt lúc chọn | Hai nút của nó nằm gần đúng chỗ operator kề hiện trên màn hình — để lại là panel đè lên chính các ứng viên cần bấm |
| `Time.unscaledTime` | Nhịp nháy phải đọc giống nhau ở x1 và x2. Đây là nhịp UI, không phải sự kiện trong thế giới |
| Chạm **bất cứ đâu khác** = huỷ | Người mở nhầm phải luôn có đường ra, trong khi wave vẫn đang chạy |
| Kiểm ứng viên **mỗi frame** | Thế giới không dừng lại chờ. Ai chết, ai tự hồi, hoặc chính người cứu gãy — đều tự rớt khỏi danh sách; hết ứng viên thì thoát |
| Tap trong lúc chọn kiểm **trước** `IsPointerOverUI` | Bấm nhầm vào một cái nút còn sót lại không được phép nhốt người chơi trong chế độ có operator đang nháy mà không thấy lối thoát |

> `c == null` chứ không phải `c?.` khi lọc ứng viên: một Unity object đã bị `Destroy` **không phải
> null thật**, nên toán tử null-conditional sẽ đi thẳng qua nó rồi ném exception.

---

> ⚠️ **BƯỚC 3.1 SẼ LÀM LỘ HAI MÓN NỢ**
>
> `ForceUnblock()` + `m_CurrentPathIndex++` chuyển từ đường hiếm thành **đường nóng**,
> và hai grid song song bắt đầu lệch nhau thường xuyên. Dự trù thêm **1 ngày** ở đây.
> Nếu nó vỡ nặng thì đó là lúc hợp nhất hai grid — Codex đã bảo *"nên hợp nhất ngay
> khi có dịp"*, và đây là dịp.

---

### GIAI ĐOẠN 4 · Truyền đạt — 4–5 ngày · **70% công sức thật**

| # | Việc |
|:--:|---|
| 4.1 | Icon 3 trạng thái trên model đã deploy — rút từ trên xuống, poof chuyển trạng thái |
| 4.2 | Icon trên **card deploy bar** — bảng theo dõi tình trạng toàn đội |
| 4.3 | Stressed: tô background tròn + nhấp nháy + **âm thanh riêng + nhịp phóng to** |
| 4.4 | Decal vòng tròn dưới đất cho aura Boss/Herald |
| 4.5 | **Kiểm tra silhouette** — chuyển ảnh sang đen trắng, vẫn phân biệt được 3 trạng thái |

> Mục 4.3 không phải trang trí. Ở tình huống bị vây nặng, cửa sổ Stressed chỉ dài
> **2,4 giây** — không có âm thanh thì không thể phản ứng.

#### 🛑 CHỐT C — Bài kiểm tra 10 giây

> Đưa build cho **một người chưa từng xem game**, không giải thích gì. Để họ chơi tới
> lúc một operator gãy. Hỏi: *"Vừa xảy ra chuyện gì với anh chàng đó?"*
>
> - **Trả lời được** → đi tiếp.
> - **Không** → **vấn đề ở hiển thị, không phải ở toán. Đừng chỉnh số.**

---

### GIAI ĐOẠN 5 · Hai loại địch — 3–4 ngày

| # | Việc | Ghi chú |
|:--:|---|---|
| 5.1 | Mở `EnemyType`, `RatioRow`, `Distribute()` lên 6 loại | **Test từ 0.3 sẽ đỏ ngay** vì boss = 0 → sửa: boss tính tường minh, Normal làm phần dư |
| 5.2 | Hai field `fearAuraRadius` / `fearAuraRate` + logic aura | Boss aura hết hardcode |
| 5.3 | **Prefab Horde tối giản**: không Animator, không HP bar, mesh nhẹ, bật GPU Instancing | |
| 5.4 | **Nở pack trong `BuildWavePlans`, SAU Fisher–Yates, nhịp 0,2s** | 🔴 Sai chỗ = **game không kết thúc được** |
| 5.5 | Prefab Herald + `baseAttackSpeed = 0` | |

> **Kiểm chứng 5.4 ngay tại bước đó:** HUD hiện đúng tổng sau khi nở pack, và **thắng
> được**. Đây là bug nguy hiểm nhất trong cả kế hoạch — nó không crash, chỉ làm game
> treo ở màn hình chiến thắng không bao giờ tới.

---

### GIAI ĐOẠN 6 · Hiệu năng — 2–3 ngày

| # | Việc |
|:--:|---|
| 6.1 | Dựng stress test: ép 135 địch đồng thời |
| 6.2 | Profile **trên máy Android thật**, so với baseline 0.2 |
| 6.3 | Tối ưu → đo lại → **ghi con số trước/sau vào README** |

> Đây là chỗ sinh ra câu chuyện portfolio: *"thêm một loại địch số lượng lớn đẩy enemy
> đồng thời lên 135, profile trên device, bỏ Animator và HP bar cho loại đó, bật GPU
> instancing, X ms → Y ms."*

---

### GIAI ĐOẠN 7 · Nội dung và cân bằng — 3–5 ngày

| # | Việc |
|:--:|---|
| 7.1 | 9 nguyên mẫu còn lại — **chỉ là file dữ liệu**, không đụng thuật toán |
| 7.2 | Hiện tên sơ đồ đầu màn (`SƠ ĐỒ: THÁC`) + [rải theo stage](#96--rải-theo-stage--đường-cong-dạy-học-miễn-phí) |
| 7.3 | Cân bằng: 4 núm, **mỗi lần một núm, đúng thứ tự 1→4** |
| 7.4 | Editor tool cảnh báo tương quan Ý chí ↔ cost > 0,5 |

#### 🛑 CHỐT D — Bài kiểm tra tiếc nuối

> Người chơi thua rồi có tự nói *"đáng lẽ tôi phải rút nó ra sớm hơn"* không?
>
> - **Có** → xong. Đã có narrative sinh ra từ cơ chế.
> - **Nói "game này xui"** → đang có hộp đen ở đâu đó. Rà lại tính công bằng của từng
>   nguồn stress.

---

### Bản cắt gọn nếu cần demo sớm — ~4 tuần

**Bỏ:**
- **Herald** — giữ Horde, vì nó mới là con chứng minh stress ≠ HP
- **9 nguyên mẫu còn lại** (GĐ 7.1)
- **Giai đoạn 6** — giảm tỉ lệ Horde xuống cho demo thay vì tối ưu

**Giữ nguyên: cả bốn chốt kiểm tra.** Cắt phạm vi thì được, cắt chốt thì không — chốt
là thứ ngăn bạn đổ 5 tuần vào một cơ chế không vui.

### Ba rủi ro lớn nhất

| Rủi ro | Ở đâu | Chặn thế nào |
|---|---|---|
| **Tô-pô mới không thật sự cho rút lui có tổ chức** | GĐ 1 | Chốt A — chơi thử *trước khi* có stress |
| **`TDOperatorView` vỡ khi có đường thoát thứ ba** | GĐ 3.1 | Sửa 0.1 trước · dự trù +1 ngày · sẵn sàng hợp nhất hai grid |
| **Nở pack sai chỗ → game không kết thúc được** | GĐ 5.4 | Kiểm chứng ngay tại bước đó, không để tới cuối |

---

> **Nguyên tắc chi phối toàn bộ tài liệu này:**
> mọi con số đều truy được về dữ liệu thật trong project, và mọi luật đều phải qua bốn
> câu hỏi — *có trong 30 giây đầu trận đầu không · chi phối mọi lần đặt lính không ·
> gỡ ra game còn chơi được không · cần unlock gì không.*
