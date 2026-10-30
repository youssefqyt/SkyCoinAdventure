# Mobile version — what was added and how to build it

The game was made for keyboard + mouse. It is now also playable on a phone:
touch controls, safe-area aware UI, landscape lock, 60 fps target and Android
player settings. **No scene or prefab was changed** — everything is added at
runtime, so the PC version still works exactly as before.

---

## 1. What you get on a phone

| Zone | Control | What it does |
|------|---------|--------------|
| Bottom left | Round joystick | Move (same as WASD, relative to the camera) |
| Anywhere on screen | Finger drag | Turn the camera (same as the mouse) |
| Bottom right | **SAUT** | Jump |
| Bottom right, above SAUT | **COURIR** | Sprint while held |
| Top right | **II** | Pause |

The controls only appear **while a level is running**. They hide themselves in
the main menu, the pause menu, the options panel, the victory panel and the
game-over panel, so they can never block a menu button.

Also handled automatically on a phone:

- **Notch / rounded corners** — the UI is fitted to `Screen.safeArea`.
- **Landscape only** — portrait is disabled in Player Settings and at runtime.
- **60 fps** — `Application.targetFrameRate = 60` and vSync off.
- **Screen stays on** — the phone does not sleep while you read a menu.
- **Text** — the keyboard tutorial line becomes "Joystick / Glisse / Boutons",
  the main-menu footer becomes "Touche une option pour jouer", and the pause
  hint "Échap pour reprendre" becomes "Appuie sur Reprendre".

The touch controls also appear automatically on any device that reports a
touchscreen (this includes a WebGL build opened on a phone).

---

## 2. Files that were added

| File | Role |
|------|------|
| `Assets/_Game/Scripts/Input/MobileControls.cs` | Builds the whole touch UI in code and shows/hides it with the game state |
| `Assets/_Game/Scripts/Input/TouchJoystick.cs` | The left joystick |
| `Assets/_Game/Scripts/Input/TouchLookArea.cs` | The full-screen finger-drag area for the camera |
| `Assets/_Game/Scripts/Input/TouchButton.cs` | A round button that reacts on press (not on release) |
| `Assets/_Game/Scripts/UI/SafeAreaFitter.cs` | Keeps the UI inside the visible part of the screen |
| `Assets/_Game/Scripts/Core/MobilePlatformSettings.cs` | Frame rate, sleep timeout, landscape |

Files that were modified:

| File | Change |
|------|--------|
| `Assets/_Game/Scripts/Input/GameInput.cs` | New "virtual input" layer: touch values are merged with keyboard/gamepad |
| `Assets/_Game/Scripts/Player/PlayerController.cs` | Jump press is now *consumed* once, so one tap = one jump |
| `Assets/_Game/Scripts/UI/HudController.cs` | Mobile version of the tutorial text |
| `Assets/_Game/Scripts/UI/PauseMenuController.cs` | Mobile version of the pause hint |
| `ProjectSettings/ProjectSettings.asset` | Portrait disabled, Android package name, optimized frame pacing |

---

## 3. Test it in the Unity Editor (no phone needed)

1. Open the project and press **Play**.
2. In the main menu the touch controls are hidden — that is correct.
3. Click **Nouvelle partie**. The controls appear over the level.
4. With the mouse:
   - drag the round joystick to walk,
   - drag anywhere else to turn the camera,
   - click **SAUT**, **COURIR**, **II**.

In the Editor the cursor is deliberately **not locked**, otherwise the mouse
could not reach the buttons. To stop the preview from appearing, select
`MobileControls (auto)` in the Hierarchy while playing and untick
**Show In Editor**.

> In the Editor the mouse also turns the camera without dragging (that is the
> normal PC behaviour, kept on purpose). On a real phone there is no mouse, so
> only finger drags turn the camera.

---

## 4. Build for Android — step by step

### 4.1 Install the Android module (required, not installed yet)

Your Unity installation currently has only **Windows** and **WebGL** support.

1. Open **Unity Hub** → **Installs**.
2. Find `6000.4.4f1`, click the **gear / three dots** → **Add modules**.
3. Tick:
   - **Android Build Support**
   - **Android SDK & NDK Tools** (inside Android Build Support)
   - **OpenJDK** (inside Android Build Support)
4. Install and wait.

### 4.2 Switch the project to Android

1. In Unity: **File → Build Profiles** (`Ctrl + Shift + B`).
2. Select **Android** in the list on the left.
3. Click **Switch Platform**. Wait for the reimport.

### 4.3 Player Settings (most values are already set)

Open **Edit → Project Settings → Player**.

**Resolution and Presentation**
- Default Orientation: **Auto Rotation**
- Allowed Orientations: **Landscape Left + Landscape Right** only
  (portrait and portrait upside-down are already unticked)

**Other Settings**
- Package Name: `com.skycoin.adventure` (already set)
- Minimum API Level: **Android 7.1 (API 25)** (already set)
- Target API Level: **Automatic (highest installed)**
- Scripting Backend: **IL2CPP** (already set)
- Target Architectures: **ARM64** (already set; required by Google Play)

### 4.4 Scenes in the build

In **File → Build Profiles**, the scene list should contain, in this order:

1. `Assets/_Game/Scenes/MainMenu.unity`
2. `Assets/_Game/Scenes/Level_01.unity`
3. `Assets/_Game/Scenes/Level_02.unity`

Untick `SampleScene` if it is still in the list. (MainMenu must be **index 0**,
because the game returns to it by name.)

### 4.5 Build

1. In **Build Profiles**, click **Build**.
2. Choose an output folder, for example `Builds/Android`.
3. Unity produces `SkyCoinAdventure.apk` (name may vary).

### 4.6 Install on the phone

1. Copy the `.apk` to the phone (USB, Google Drive, etc.).
2. Open it on the phone and allow **Install unknown apps** for that file
   manager when asked.
3. Launch the game **in landscape**. The touch controls appear when the level
   starts.

---

## 5. Quick test without Android (optional)

WebGL support is already installed, so you can test on the phone browser:

1. **File → Build Profiles → WebGL → Build** into `Builds/WebGL`.
2. Serve the folder, for example with Python:
   `python -m http.server 8000` inside `Builds/WebGL`.
3. On the phone (same Wi-Fi), open `http://<your-PC-IP>:8000`.

The game detects the touchscreen and shows the controls. WebGL on a phone is
slow, so use this only to check the controls, not to judge performance.

---

## 6. Tuning

Select `MobileControls (auto)` in the Hierarchy **while playing**, or edit the
defaults in `Assets/_Game/Scripts/Input/MobileControls.cs`:

| Field | Default | Effect |
|-------|---------|--------|
| `lookScale` | 1.25 | Camera speed per finger pixel. Raise = faster turning |
| `deadZone` | 0.15 | Joystick dead zone before movement starts |
| `joystickDiameter` | 280 | Joystick size in 1920×1080 reference pixels |
| `jumpButtonSize` | 200 | SAUT button size |
| `sprintButtonSize` | 150 | COURIR button size |
| `pauseButtonSize` | 100 | II button size |

The in-game **Options → Sensibilité** slider also changes touch sensitivity
(it multiplies the same value used by the mouse).

---

## 7. Troubleshooting

**No controls on the phone**
- Check the build target is Android (or a touchscreen is detected).
- Look at the log: `adb logcat -s Unity` — an exception during
  `MobileControls` creation would be printed there.
- The controls are hidden in menus on purpose; start a level.

**Buttons are too small**
- Increase `jumpButtonSize`, `sprintButtonSize`, `pauseButtonSize` and
  `joystickDiameter`, or lower the CanvasScaler reference resolution of
  `MobileControlsCanvas` at runtime.

**The camera turns too fast / too slow**
- Change `lookScale` (1.0 = neutral, 1.25 = default, 2.0 = fast).

**The game runs at 30 fps**
- Some phones ignore `targetFrameRate` when the quality level forces vSync.
  `MobilePlatformSettings` already sets `vSyncCount = 0`; check the Quality
  settings if you added your own quality levels.

**A button stays pressed after pausing**
- Not possible by design: hiding the controls calls `ResetVirtualInput()`,
  which clears move, sprint and jump.

---

## 8. Why it is built this way

- **No scene edits.** The touch UI is created at runtime, so the existing
  scenes, prefabs and PC controls keep working and nothing can break if a
  scene is rebuilt.
- **One input source.** `GameInput` is still the only place gameplay reads
  input. Touch values are merged there, so `PlayerController`,
  `ThirdPersonCamera` and `GameManager` needed almost no changes.
- **Consumed jump.** A tap sets a pending flag that is cleared when read, so
  one tap cannot jump every frame.
- **Frame-stamped look.** A drag delta is valid for exactly one frame, which
  is the frame the camera reads it in `LateUpdate`.
