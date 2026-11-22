# Roomba Cleaner Simulation (Unity ML-Agents)

A 3D autonomous vacuum cleaning simulation built with Unity and ML-Agents, utilizing Reinforcement Learning (PPO) and Ray Perception Sensors for obstacle avoidance and efficient coverage navigation.

---

## Features

1. **3D Cutaway Environment:**
   - Low-poly furnished living room featuring mint green walls, light wood flooring, and a central dotted rug.
   - Curved bay window with curtains.
   - Interactive furniture including dining set, sofa with cushions, coffee table, floor lamp, bookshelf, indoor plants, and rocking horse.

2. **Autonomous Vacuum Agent:**
   - Cylindrical vacuum body with directional navigation and collision detection.
   - Integrated Ray Perception Sensor 3D for real-time obstacle detection and raycast visualization.
   - Autonomous movement, obstacle avoidance, corner negotiation, and dust collection logic.

3. **Dirt Collection System:**
   - Procedural grid-based dirt distribution across the room floor.
   - Dynamic spawn checking to avoid placing items inside solid furniture.
   - Spinning dirt nodes with trigger-based collection.

4. **HUD and Metrics Display:**
   - Bottom overlay displaying real-time metrics:
     - Episode count, elapsed time, current cumulative reward.
     - Total step count, best recorded time, and remaining dirt count.

---

## Getting Started

### Prerequisites
- Unity 6 (6000.x) or compatible LTS release
- Python 3.9 - 3.10 with `mlagents` package (for model training)

### Step 1: Open the Project
1. Launch **Unity Hub**.
2. Click **Add** and select the repository root directory.
3. Open the project using Unity 6.

### Step 2: Open the Scene
- Navigate to `Assets/Scenes/` and open `RoombaCleanerScene.unity`.
- *Note: You can regenerate the entire scene anytime from the Unity toolbar via **ML-Agents > Build Roomba Simulation Scene (One Click)**.*

### Step 3: Run the Simulation
1. Press the **Play** button in the Unity Editor.
2. Manual control is available using **W / A / S / D** or **Arrow keys** via the heuristic mode.
3. Observe sensor rays, obstacle avoidance, and real-time statistics on the HUD.

---

## Training the Agent (ML-Agents Python)

The project includes training configuration located at `config/roomba_ppo.yaml`.

1. Run `train.bat` in the root folder, or execute the following command in terminal:
   ```bash
   mlagents-learn config/roomba_ppo.yaml --run-id=Roomba_Pro_v1 --resume
   ```
2. When the console displays:
   ```text
   [INFO] Listening on port 5004. Start training by pressing the Play button in the Unity Editor.
   ```
3. Press **Play** in the Unity Editor to begin training.
