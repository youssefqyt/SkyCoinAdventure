# 🪙 Sky Coin Adventure (3D)

[![Unity 6](https://img.shields.io/badge/Unity-6000.4.4f1%20(Unity%206)-blue.svg?logo=unity)](https://unity.com/)
[![Render Pipeline](https://img.shields.io/badge/Render%20Pipeline-URP%2017.4-lightgrey.svg)](https://unity.com/srp/universal-render-pipeline)
[![Platform](https://img.shields.io/badge/Platforms-Windows%20%7C%20WebGL%20%7C%20Android-green.svg)](#cross-platform--controls)
[![Input System](https://img.shields.io/badge/Input-New%20Input%20System-orange.svg)](https://docs.unity3d.com/Packages/com.unity.inputsystem@latest)
[![CI/CD](https://img.shields.io/badge/CI%2FCD-GitHub%20Actions%20Cloud%20Build-purple.svg)](.github/workflows/build-android.yml)

A vibrant, fast-paced **3D collect-a-thon platformer** built from the ground up for **Unity 6**. 

Race against the clock across dynamic floating arenas, leap across moving and crumbling platforms, collect dynamically relocating coins, and evade relentless chaser enemies. Fully playable with **Keyboard & Mouse**, **Gamepads**, or **Mobile Touchscreens** (Android & WebGL Mobile).

---

## 🌟 Key Features

* **🏃 Responsive Character Controller Movement**
  * Built using Unity's `CharacterController` (no unpredictable Rigidbody physics slides).
  * Professional platformer feel with **Coyote Time** (0.12s) and **Jump Buffering** (0.12s).
  * Snappy custom gravity (-22 m/s²), ramp projection, smooth acceleration, and sprint multipliers.
  * Squash-and-stretch visual animation feedback.

* **🎯 Dynamic Procedural Coin Spawner**
  * No fixed, static coin placement! The custom `CoinSpawner` continuously maintains active coins on the map (12 on Level 1, 16 on Level 2).
  * Automatically relocates all coins every 3 seconds to randomly validated walkable ground points (≥ 3.5m from the player, flat terrain only).
  * Idempotent collection ensures zero duplicate scoring bugs.

* **🎥 Smooth Orbiting Third-Person Camera**
  * Smooth damping mouse/stick/touch look controls.
  * Real-time raycast anti-clipping collision prevention to keep geometry out of the camera's view.
  * Mouse wheel and stick zoom support.

* **🤖 Smart NavMesh Chaser Enemy**
  * Automated runtime NavMesh baking (`RuntimeNavMeshBaker`) — zero manual baking required in the editor.
  * State-machine AI that patrols, spots the player, and pursues them across the floating platforms.

* **⚠️ Interactive Environmental Hazards**
  * **Disappearing Platforms:** Telegraph fall timing with shaking / material transitions before dropping and respawning.
  * **Moving Platforms:** Zero-drift moving platforms with `PlatformCarrier` carrying physics running deterministically before player updates.
  * Fall detection trigger boundaries with instant player respawn and state evaluation.

* **📱 Native Mobile & Touchscreen Support**
  * Fully playable on smartphones and tablets.
  * Built-in on-screen **Virtual Joystick** for camera-relative movement.
  * Full-screen touch-drag area for rotating the camera.
  * Dedicated **SAUT** (Jump), **COURIR** (Sprint), and **II** (Pause) touch buttons.
  * **Safe-Area Fitter:** Automatically fits HUD and buttons inside phone display safe zones (notches, camera punch-holes, rounded corners).
  * Locks landscape orientation and targets **60 FPS** with zero battery drain when idle.

* **🎵 Procedural Audio Synthesizer**
  * Built-in C# sound synthesis (`AudioManager.cs`) — generates ambient background pads, jump chimes, coin pickup bells, and defeat sirens in code with zero external asset dependencies.

* **💾 Persistent Progression & Save System**
  * Saves unlocked levels, best completion times, and user settings (audio volume, camera look sensitivity) to persistent storage.

---

## 🎮 Cross-Platform Controls

The game automatically detects your input device and adjusts hints and visibility accordingly.

| Action | Keyboard (AZERTY / QWERTY) | Gamepad (Xbox / PS) | Mobile Phone Touchscreen |
| :--- | :--- | :--- | :--- |
| **Move** | `W` `A` `S` `D` / Arrow Keys | Left Analog Stick | Virtual On-Screen Joystick (Bottom-Left) |
| **Look Camera** | Mouse Delta | Right Analog Stick | Finger Drag anywhere on screen |
| **Jump** | `Space` | South Button (`A` / `✕`) | **SAUT** Button (Bottom-Right) |
| **Sprint** | `Left Shift` / `Right Shift` | Left Stick Click (`L3`) | **COURIR** Button (Held) |
| **Zoom** | Mouse Scroll Wheel | D-Pad Up / Down | — |
| **Pause** | `Escape` | Start / Options | **II** Button (Top-Right) |
| **Menu Tap** | Left Click | UI Submit (`A` / `✕`) | Direct Screen Tap on any button / slider |

---

## 🗺️ Level Overview

The project features a full game loop with progression, timers, and game-over/victory conditions:

1. **Main Menu (`MainMenu.unity`)**
   * Level selection with lock/unlock status, persistent best completion times, and settings modal.
2. **Level 1 — Discovery (`Level_01.unity`)**
   * Objective: Collect **30 coins**.
   * Focuses on movement mastery, jumping across disappearing tiles, and dynamic coin gathering.
3. **Level 2 — The Escape (`Level_02.unity`)**
   * Objective: Collect **50 coins** under a strict countdown timer while escaping a fast chaser enemy and timing moving platform leaps.

---

## 🏗️ Architecture & Technical Highlights

```
Assets/_Game/
├── Data/                   # ScriptableObject level definitions (coins needed, timers, enemies)
├── Materials/              # Curated URP materials (Ground, Hazards, Coins, Player, UI)
├── Prefabs/                # Player, Enemies, Coins, Hazards, Platforms, UI Canvases
├── Scenes/                 # MainMenu, Level_01, Level_02
├── Scripts/
│   ├── Audio/              # Procedural audio generator and sound effect manager
│   ├── Camera/             # Third-person orbit camera with clipping prevention
│   ├── Collectibles/       # Coin pickup and dynamic terrain-aware CoinSpawner
│   ├── Core/               # GameManager state machine, LevelController, Bootstrap
│   ├── Editor/             # Automated builders and headless AndroidBuilder CLI
│   ├── Enemies/            # NavMesh chaser AI and capture reporting
│   ├── Environment/        # PlatformCarrier and RuntimeNavMeshBaker
│   ├── Hazards/            # Disappearing floor and moving platform logic
│   ├── Input/              # Code-defined Input Actions & MobileControls runtime UI
│   ├── Player/             # CharacterController, ground probe, jump buffers, visuals
│   ├── Saving/             # SaveService (PlayerPrefs / JSON persistence)
│   └── UI/                 # HUD, Pause Menu, Options, Results, SafeAreaFitter
```

### ⚙️ Design Decisions
* **Zero Bootstrap Scene Requirement:** `GameBootstrap` runs `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`, instantiating persistent singletons (`GameManager`, `GameInput`, `AudioManager`, `SaveService`, `MobileControls`) so pressing **Play** in any scene works immediately.
* **Non-Destructive Touch Integration:** Touch controls are injected at runtime into a dedicated overlay canvas without altering existing level scenes or prefabs, preserving 100% desktop compatibility.
* **Deterministic Execution Orders:** Custom `DefaultExecutionOrder` attributes ensure physics platforms (`-150`) evaluate before the player (`-100`), input evaluates first (`-400`), and cameras update last in `LateUpdate`.

---

## 🚀 Getting Started (Unity Editor)

### Prerequisites
* **Unity 6** (`6000.4.4f1` or newer)
* **Packages included in project:** Universal RP (`com.unity.render-pipelines.universal`), Input System (`com.unity.inputsystem`), AI Navigation (`com.unity.ai.navigation`), TextMeshPro (`com.unity.ugui`).

### Fast Setup
1. Clone this repository:
   ```bash
   git clone https://github.com/youssefqyt/SkyCoinAdventure.git
   ```
2. Open the project in **Unity Hub** using **Unity 6 (6000.4.4f1)**.
3. In Unity:
   * Go to `Window → TextMeshPro → Import TMP Essential Resources` (if prompted).
   * Open `Assets/_Game/Scenes/MainMenu.unity`.
4. Press **Play** and enjoy the game!

---

## 📦 Building the Game

### 🖥️ Windows Desktop
1. In Unity, go to **File → Build Profiles** (`Ctrl + Shift + B`).
2. Select **Windows** and click **Build**.

### 🌐 WebGL (Playable in Browser on PC & Mobile)
1. Select **WebGL** in **File → Build Profiles** and click **Build** to `Builds/WebGL`.
2. Host locally using Python:
   ```powershell
   python -m http.server 8000 --directory Builds/WebGL
   ```
3. Open `http://localhost:8000` (or `http://<YOUR-IP>:8000` on your mobile phone connected to Wi-Fi) to play directly in browser with full touch controls.

### 📱 Android APK (Cloud Build — 0 MB Local Disk Space)
This repository includes a pre-configured **GitHub Actions CI/CD** pipeline (`.github/workflows/build-android.yml`) that compiles the `.apk` on GitHub's cloud runners for free:

1. Refer to [GITHUB_BUILD_GUIDE.md](GITHUB_BUILD_GUIDE.md) for the 1-time setup.
2. Go to the **Actions** tab on GitHub → select **Build Android APK (Cloud)** → click **Run workflow**.
3. Download the compiled `SkyCoinAdventure-Android-APK` from the artifacts list and install it on your Android phone!

---

## 📜 License & Credits

* **Developer:** Developed with Unity 6 & Universal Render Pipeline.
* **Code & Architecture:** Open for educational and commercial reference.
