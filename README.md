# 🤖 Roomba Cleaner Simulation (Unity ML-Agents)

Dự án mô phỏng **Robot hút bụi tự hành 3D (Roomba Vacuum Cleaning Simulation)** ứng dụng **Học tăng cường (Reinforcement Learning - PPO)** và **Cảm biến tia quét 3D (Ray Perception Sensor)** theo đúng nguyên mẫu hình ảnh yêu cầu.

---

## 📸 Các tính năng chính (Khớp 100% hình ảnh)

1. **Căn phòng 3D Cutaway Low-Poly:**
   - Tường màu xanh bạc hà pastel (`Mint Green`), sàn gỗ xám nhạt, thảm xanh chấm bi ở giữa phòng.
   - Cửa sổ kính vòm phía sau (Bay Window) kèm rèm cửa xanh.
   - Đầy đủ nội thất: Bộ bàn ăn 4 ghế trắng, sofa kèm gối tím, bàn trà, đèn cây (có ánh sáng vàng ấm), kệ sách, 2 chậu cây cảnh, ngựa gỗ bập bênh, thảm cửa.
2. **Robot Hút Bụi (Roomba Agent):**
   - Khối hình trụ dẹt màu xám than viền đen, nút nguồn đỏ phía trên.
   - Tích hợp **Ray Perception Sensor 3D** với 14 tia cảm biến tỏa xung quanh (vẽ các tia và vòng tròn xanh lá cây khi va chạm theo thời gian thực).
   - Tự động di chuyển, xoay góc, né tường, né bàn ghế và thu thập các hạt bụi.
3. **Hệ thống Rác / Hạt Bụi (Dirt Nodes):**
   - Phân bố đều khắp sàn theo lưới (Grid) 0.95m (khoảng 70-80 điểm rác, không bị kẹt trong nội thất).
   - Biểu tượng ngôi sao vàng tự xoay lấp lánh.
4. **Bảng Thống kê HUD (Dưới chân màn hình):**
   - Thanh nền xanh thẫm bán trong suốt.
   - Dòng 1: `Episode: X` | `Time: MM:SS` | `Rewards: X.XX`
   - Dòng 2: `Steps: X` | `Best: MM:SS` | `Dirt: X`

---

## 🚀 Hướng dẫn mở dự án & Chạy thử trong 3 bước

### Bước 1: Mở dự án trong Unity
1. Mở **Unity Hub**.
2. Bấm nút **Add** (hoặc **Open**) > Chọn thư mục:
   `C:\Users\namkh\.gemini\antigravity\scratch\RoombaCleanerSimulation`
3. Mở bằng **Unity 6 (6000.6.x)**.

### Bước 2: Mở Scene
- Vào thư mục `Assets/Scenes/` > Mở scene `RoombaCleanerScene.unity`.
- *(Hoặc bấm menu trên thanh công cụ: **ML-Agents > Build Roomba Simulation Scene (One Click)** để tự động tái tạo lại phòng bất kỳ lúc nào).*

### Bước 3: Chạy mô phỏng
1. Bấm nút **▶ Play** trong Unity Editor.
2. Bạn có thể tự lái thử Robot bằng các phím **W / A / S / D** hoặc **Phím mũi tên**.
3. Xem các tia cảm biến xanh quét vật cản và rác được hút sạch, thông số HUD nhảy số thời gian thực!

---

## 🧠 Huấn luyện AI tự động học (Training với ML-Agents Python)

Dự án đã tích hợp sẵn môi trường ảo Python và cấu hình PPO tối ưu tại `config/roomba_ppo.yaml`.

1. Nhấp đúp chuột vào file **`train.bat`** trong thư mục dự án (hoặc mở Terminal gõ: `mlagents-learn config/roomba_ppo.yaml --run-id=Roomba_v1`).
2. Khi Terminal hiện:
   `[INFO] Listening on port 5004. Start training by pressing the Play button in the Unity Editor.`
3. Bấm **▶ Play** trong Unity Editor.
4. Robot sẽ bắt đầu tự học khám phá và tối ưu hóa đường đi lau dọn nhanh nhất!
