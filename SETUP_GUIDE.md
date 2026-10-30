# Sky Coin Adventure — Complete Setup & Run Guide

A 3D coin-collection game for **Unity 6**, desktop (keyboard + mouse).
Two levels, French UI, third-person camera, CharacterController player, a chaser enemy,
a countdown, disappearing floors and a moving platform.

This document tells you **everything** needed to get from the current project to a
playing game: materials, layers, prefabs, scenes, UI, build settings, controls,
test checklists and troubleshooting.

---

## Table of contents

- [Part 0 — TL;DR (the fast path)](#part-0--tldr-the-fast-path)
- [Part 1 — What is in the project](#part-1--what-is-in-the-project)
- [Part 2 — Prerequisites (one-time)](#part-2--prerequisites-one-time)
- [Part 3 — Recommended: the one-click builder](#part-3--recommended-the-one-click-builder)
- [Part 4 — Manual build, step by step](#part-4--manual-build-step-by-step)
  - [4.1 Layers](#41-layers)
  - [4.2 Materials](#42-materials)
  - [4.3 Prefabs](#43-prefabs)
  - [4.4 Scene: MainMenu](#44-scene-mainmenu)
  - [4.5 Scene: Level_01](#45-scene-level_01)
  - [4.6 Scene: Level_02](#46-scene-level_02)
  - [4.7 Build Settings](#47-build-settings)
- [Part 5 — Controls](#part-5--controls)
- [Part 6 — Test checklists (milestones 1–6)](#part-6--test-checklists-milestones-16)
- [Part 7 — Troubleshooting](#part-7--troubleshooting)
- [Part 8 — Build & publish](#part-8--build--publish)
- [Part 9 — How the systems work](#part-9--how-the-systems-work)
- [Part 10 — File inventory](#part-10--file-inventory)
- [Part 11 — Optional polish](#part-11--optional-polish)

---

## Part 0 — TL;DR (the fast path)

If you only want to play, do these four things:

1. **`Window → TextMeshPro → Import TMP Essential Resources`** (mandatory, once).
2. **`Sky Coin Adventure → Setup → 1. Create Materials`**.
3. **`Sky Coin Adventure → Setup → 2. Build The Whole Game`**.
   It creates every prefab, both levels, all UI, the level assets and registers the
   scenes in Build Settings. Watch the Console; you should see a dialog summarising
   `Level_01: 12 coins on the map at once, 30 to collect` and
   `Level_02: 16 coins on the map at once, 50 to collect`.
4. Press **Play**. The game starts on the Main Menu.

Everything after this point is either verification, the manual (backup) path, or
explanation. **Part 3** is the automated path in detail; **Part 4** is the
do-it-by-hand path.

---

## Part 1 — What is in the project

```
Game 3D/
├── Assets/
│   └── _Game/
│       ├── Audio/         (Music/, SFX/)          empty by default — audio is generated in code
│       ├── Data/                                  LevelDefinition assets go here
│       ├── Materials/                             the 12 shared materials
│       ├── Prefabs/       (Player, Enemies, Collectibles, Environment, Hazards, UI)
│       ├── Scenes/        (MainMenu.unity, Level_01.unity, Level_02.unity)
│       ├── Scripts/       (28 C# files — see Part 10)
│       └── Settings/                              (URP settings live under Assets/Settings normally)
├── ProjectSettings/      (layers are written by the builder)
└── SETUP_GUIDE.md        (this file)
```

Already present and **not** to be changed: the URP pipeline asset (URP 17.4.0),
Input System, AI Navigation, TextMeshPro (bundled in `com.unity.ugui`).

Script count: **28** `.cs` files under `Assets/_Game/Scripts/`.

---

## Part 2 — Prerequisites (one-time)

| # | Requirement | How to check | How to fix |
|---|-------------|--------------|------------|
| 1 | **Unity 6** (`6000.4.4f1` or newer 6.x) | Help → About Unity | Open the project with Unity 6 |
| 2 | **TextMeshPro Essential Resources** | `Assets/TextMesh Pro/Resources/TMP Settings.asset` exists | `Window → TextMeshPro → Import TMP Essential Resources` |
| 3 | **Input System** active | Edit → Project Settings → Player → *Active Input Handling* = `Input System Package (New)` or `Both` | Set it; Unity restarts |
| 4 | **AI Navigation** installed | Package Manager shows `com.unity.ai.navigation` | Install it (free) |
| 5 | URP active | Project Settings → Graphics → default render pipeline = a URP asset | Already correct in this project |

> **Why TMP Essentials matters:** the builder calls `TMP_Settings.defaultFontAsset`
> to style every label. Without the imported resources, Unity has no default font and
> the builder stops with a dialog before creating anything. Do it first.

Nothing else needs downloading. All sounds are synthesised in C# at runtime, so no
asset store music or SFX are required.

---

## Part 3 — Recommended: the one-click builder

The builder is the fastest and least error-prone route. It writes real `.prefab` and
`.unity` files, sets layers, and updates Build Settings — identical to doing Part 4 by
hand, just automated.

### 3.1 Create the materials

Menu: **`Sky Coin Adventure → Setup → 1. Create Materials`**

Creates/updates these 12 URP Lit materials in `Assets/_Game/Materials/` (see
[4.2](#42-materials) for the exact values). Safe to run repeatedly: existing
materials are updated in place so nothing loses its reference.

### 3.2 Build the game

Menu: **`Sky Coin Adventure → Setup → 2. Build The Whole Game`**

This performs, in order:

1. Re-runs the material step (so you can skip 3.1 if you like).
2. Creates tags **and layers**: `6 Ground`, `7 Player`, `8 Enemy`, `9 Coin`, `10 Hazard`.
3. Creates the **prefabs**: `Player`, `Coin`, `Enemy`, `Floor_Hole`, `Moving_Platform`,
   plus the UI prefabs.
4. Creates the **LevelDefinition assets** in `Assets/_Game/Data/`:
   `Level_01.asset` (30 coins, no enemy, no timer) and
   `Level_02.asset` (50 coins, 1 enemy, 180 s timer, holes + moving platform).
5. Creates the **scenes**: `MainMenu`, `Level_01`, `Level_02` — with full geometry,
   coins, hazards, enemy, navmesh surface, player rig, camera, lights, EventSystem and UI.
6. Adds a **`CoinSpawner`** to each level. Coins are no longer authored by hand: the
   spawner drops its pool onto real ground, then moves **every** coin to a new random
   position every **3 seconds**. A collected coin comes back on the next wave, so the
   30 / 50 coin objective can always be reached.
7. Adds both levels (and the menu) to **Build Settings**.

Expected Console dialog:

```
Sky Coin Adventure — build finished
Level_01: 12 coins on the map at once, 30 to collect
Level_02: 16 coins on the map at once, 50 to collect
All coins jump to new random spots every 3 seconds.
```

If a level ever reports fewer coins than expected, check the **`CoinSpawner`** object in
the scene: its `areaCenter` / `areaSize` box must cover the islands, and its `groundMask`
must include the `Ground` layer.

### 3.3 Verify

Menu: **`Sky Coin Adventure → Setup → 3. Validate The Built Scenes`**

Re-opens each scene and asserts: a `LevelController` exists, a `Player` exists, a
`PlayerSpawn` exists, a `ThirdPersonCamera` exists, exactly one `CoinSpawner` exists,
the enemy exists on Level 2, the timer is configured, and the hazard scripts are wired.
Any failure is printed with the scene name and the missing object.

### 3.4 Reset saved progress

Menu: **`Sky Coin Adventure → Setup → 4. Reset Saved Progress`**

Deletes the PlayerPrefs keys the game writes (unlocked levels, best times). Useful
when testing the menu's *Continuer* / *Sélection de niveau* buttons.

---

## Part 4 — Manual build, step by step

Use this if the builder fails, or if you want to understand/rebuild every part by hand.
It reproduces exactly what the builder does.

### 4.1 Layers

`Edit → Project Settings → Tags and Layers`, add these **user layers** (index matters):

| Index | Name |
|-------|------|
| 6 | `Ground` |
| 7 | `Player` |
| 8 | `Enemy` |
| 9 | `Coin` |
| 10 | `Hazard` |

Assign them as you build:

- Every solid walkable surface → **Ground**
- The Player root → **Player** (and all its children)
- The Enemy root → **Enemy**
- The Coin prefab root → **Coin**
- Disappearing floors, moving platform → **Hazard**

There is **no** special collision matrix to edit: the enemy has no collider (capture is
a distance test), and all triggers (`Coin`, pickup sensor) work out of the box.

### 4.2 Materials

Create each as **Material** with shader **`Universal Render Pipeline/Lit`**
(right-click → Create → Material, then set Shader). Set **Base Color** (the RGB
values are 0–1; multiply by 255 for the colour picker), **Metallic**, **Smoothness**.
For emissive ones tick **Emission** and set its colour.

| Material | Base Color (R,G,B) | Metallic | Smoothness | Emission (R,G,B) × intensity |
|----------|--------------------|----------|-----------|------------------------------|
| `M_Robot_Body`     | 0.86, 0.90, 0.94 | 0.10 | 0.35 | — |
| `M_Robot_Dark`     | 0.15, 0.18, 0.24 | 0.30 | 0.30 | — |
| `M_Robot_Accent`   | 1.00, 0.45, 0.10 | 0.20 | 0.40 | 1.00, 0.45, 0.10 × 1.5 |
| `M_Visor`          | 0.04, 0.07, 0.10 | 0.10 | 0.60 | 0.30, 0.95, 1.00 × 2.5 |
| `M_Ground_Grass`   | 0.30, 0.62, 0.38 | 0.00 | 0.15 | — |
| `M_Ground_Rock`    | 0.42, 0.44, 0.50 | 0.05 | 0.25 | — |
| `M_Bridge_Wood`    | 0.55, 0.38, 0.24 | 0.00 | 0.20 | — |
| `M_Coin`           | 1.00, 0.78, 0.20 | 0.65 | 0.70 | 1.00, 0.72, 0.15 × 1.2 |
| `M_Enemy_Body`     | 0.72, 0.18, 0.22 | 0.20 | 0.35 | — |
| `M_Enemy_Eye`      | 0.05, 0.05, 0.06 | 0.00 | 0.70 | 1.00, 0.15, 0.15 × 2.5 |
| `M_Hazard_Safe`    | 0.35, 0.55, 0.65 | 0.10 | 0.35 | — |
| `M_Hazard_Warning` | 0.95, 0.35, 0.10 | 0.10 | 0.40 | 1.00, 0.35, 0.05 × 2.0 |

> `M_Hazard_Safe` is the blue plate shown **before** a floor disappears;
> `M_Hazard_Warning` is the flashing orange state during the warning window.

### 4.3 Prefabs

#### 4.3.1 `Coin` (in `Prefabs/Collectibles/`)

```
Coin                         [Layer: Coin]
├─ SphereCollider            isTrigger = ON, radius = 0.45
├─ Coin (script)
└─ CoinVisual                (empty, this is the spin pivot)
   └─ CoinDisc               Cylinder, Scale (0.75, 0.05, 0.75), Rotation X = 90
                             Material M_Coin
```

- Remove the Cylinder's default `CapsuleCollider` (the trigger on the root handles pickup).
- The disc is rotated so it stands upright like a coin facing the camera on spin.
- The spin and bob are driven by the `Coin` script.

#### 4.3.1b `CoinSpawner` (a script, added to a scene object)

`CoinSpawner` owns every coin in a level. There are **no** hand-placed coins any more.

```
Coins                         empty, pos (0, 0, 0)
└─ CoinSpawner                + CoinSpawner (script)
   coinPrefab                 = Prefabs/Collectibles/Coin.prefab
   activeCoins                = 12  (level 1)  /  16  (level 2)
   relocateInterval           = 3
   areaCenter / areaSize      = the XZ box that covers the islands
   heightAboveGround          = 1.0
   groundMask                 = Ground
   excludedParentNames        = Decorations, Hazards
```

What it does, every `relocateInterval` seconds:

1. **Moves every coin** to a brand-new random position inside the box.
2. A coin that was already collected is **re-activated** at its new spot, which is what
   keeps the map stocked.
3. A candidate spot is used **only** if a ray cast from above finds real, walkable ground
   on the `Ground` layer that is not under `Decorations` (rocks, pillars) and not under
   `Hazards` (collapsing plates, moving platform). It must also be at least
   `minDistanceFromPlayer` (3.5 m) away from the player and
   `minDistanceBetweenCoins` (2.5 m) away from the other coins.

A coin can therefore never float over the void, sit inside geometry, or end up on top of
the 4 m landmark pillar where nobody could reach it. There is no upper limit on how many
coins the player may collect: the pool keeps refreshing, so the objective is always
reachable.

#### 4.3.2 `Player` (in `Prefabs/Player/`)

```
Player                       [Layer: Player]
├─ CharacterController       Height 1.7, Radius 0.4, Center (0, 0.85, 0),
│                            Slope Limit 45, Step Offset 0.3,
│                            Min Move Distance 0, Skin Width 0.03
├─ PlayerController (script)  (assign cameraTransform + visualRoot)
├─ PlayerVisuals  (script)
├─ Visuals                    (empty parent — assigned to PlayerController.visualRoot)
│  ├─ Torso      Capsule   pos (0, 0.85, 0)   scale (0.55, 0.55, 0.55)   M_Robot_Body
│  ├─ Head       Sphere    pos (0, 1.55, 0)   scale (0.5, 0.5, 0.5)      M_Robot_Body
│  ├─ Visor      Cube      pos (0, 1.57, 0.24) scale (0.34, 0.14, 0.06)   M_Visor
│  ├─ Arm_L      Capsule   pivot (0.43, 1.22, 0)                          M_Robot_Dark
│  ├─ Arm_R      Capsule   pivot (-0.43, 1.22, 0)                         M_Robot_Dark
│  ├─ Leg_L      Capsule   pivot (0.17, 0.78, 0)                          M_Robot_Dark
│  └─ Leg_R      Capsule   pivot (-0.17, 0.78, 0)                         M_Robot_Dark
└─ PickupSensor               (empty)
   ├─ SphereCollider         isTrigger = ON, radius = 0.55, center (0, 0.85, 0)
   └─ Rigidbody               isKinematic = ON, Collision Detection = Continuous Speculative
```

> **Important:** a bare `CharacterController` does **not** raise `OnTriggerEnter`.
> That is why `PickupSensor` exists: a kinematic Rigidbody + trigger sphere that does
> raise the event, and forwards hits to the player. Do not delete it.

> **Important:** never add a `Rigidbody` to the Player root — movement is fully
> CharacterController-driven (custom gravity, jump and teleport).

#### 4.3.3 `Enemy` (in `Prefabs/Enemies/`)

```
Enemy                        [Layer: Enemy]
├─ EnemyChaser (script)
├─ NavMeshAgent               Speed 4.6, Angular Speed ~720,
│                             Acceleration ~24, Stopping Distance 0.4,
│                             Height 1.6, Radius 0.4, Auto Braking = ON
└─ Visuals
   ├─ Body   Sphere   pos (0, 0.7, 0)   scale (0.9, 0.9, 0.9)   M_Enemy_Body
   └─ Eye    Sphere   pos (0, 0.85, 0.35) scale (0.3, 0.3, 0.3)  M_Enemy_Eye
```

- **No collider on the enemy.** Capture is a distance test (radius 0.9 m, height
  tolerance 1.6 m) so the enemy never physically shoves the player.
- `EnemyChaser` reports capture to `LevelController` — it never decides the outcome itself.
- The enemy activates after a **1.5 s** delay so it does not instantly trap you.

#### 4.3.4 `Floor_Hole` (in `Prefabs/Hazards/`)

```
Floor_Hole                   [Layer: Hazard]
├─ BoxCollider (from Cube)   Size (3, 0.25, 3)
├─ MeshRenderer              M_Hazard_Safe  (Cube primitive, scale (3, 0.25, 3))
└─ DisappearingFloor (script)
```

`DisappearingFloor` fields:

| Field | Meaning | Suggested |
|-------|---------|-----------|
| `warnDelay` | seconds on the plate before warning | 0 |
| `warningDuration` | flashing window ("telegraph") | 1.2 |
| `respawnDelay` | seconds gone before it returns | 3.0 |
| `safeMaterial` | plate material before warning | `M_Hazard_Safe` |
| `warningMaterial` | flashing material | `M_Hazard_Warning` |
| `startDelay` | per-plate offset | 0 / 1.3 / 2.6 (level-specific) |

The plate flashes before it drops, so the hazard is always telegraphed — you are never
killed by an invisible trigger.

#### 4.3.5 `Moving_Platform` (in `Prefabs/Hazards/`)

```
Moving_Platform              [Layer: Hazard]
├─ BoxCollider (from Cube)   Size (3, 0.4, 3)
├─ MeshRenderer              M_Bridge_Wood  (Cube primitive, scale (3, 0.4, 3))
├─ MovingPlatform (script)
└─ PlatformCarrier (script)
```

`MovingPlatform` fields: `travelOffset` (e.g. `(5, 0, 0)`), `speed` / `duration`,
`pingPong = ON`. `PlatformCarrier` carries the player: it runs at
`[DefaultExecutionOrder(-150)]`, **before** `PlayerController` (-100), so carried
movement has no one-frame lag.

#### 4.3.6 UI prefabs (in `Prefabs/UI/`)

UI is easier to author directly in each scene (Part 4.4–4.6 describe the hierarchy).
If you prefab it, the three panels are: **Hud**, **PauseMenu**, **ResultPanel**, plus a
shared **OptionsPanel**.

### 4.4 Scene: MainMenu

`Assets/_Game/Scenes/MainMenu.unity`. Contents:

```
MainMenu
├─ Main Camera           (background colour dark blue; no ThirdPersonCamera here)
├─ Directional Light
├─ EventSystem           (EventSystem + InputSystemUIInputModule)
├─ Canvas                (Screen Space - Overlay, CanvasScaler Scale With Screen Size 1920×1080)
│  ├─ Background         (full-screen Image or a slowly rotating 3D coin behind)
│  ├─ Title              TMP "Sky Coin Adventure"
│  ├─ Button_NewGame     TMP "Nouvelle partie"
│  ├─ Button_Continue    TMP "Continuer"      (disabled until a save exists)
│  ├─ Button_LevelSelect TMP "Sélection de niveau"
│  ├─ Button_Options     TMP "Options"
│  ├─ Button_Quit        TMP "Quitter"
│  ├─ LevelSelectPanel   (hidden by default)
│  │  ├─ Button_Level1   "Niveau 1"
│  │  ├─ Button_Level2   "Niveau 2"
│  │  └─ Button_Close    "Fermer"
│  ├─ ConfirmPanel       (hidden; "Effacer la progression ?" + Oui / Non)
│  └─ OptionsPanel       (see 4.5.4)
└─ MainMenuController (script)  — wire every button/panel field
```

`MainMenuController` fields to assign: `newGameButton`, `continueButton`,
`levelSelectButton`, `optionsButton`, `quitButton`, `continueLabel`, `levelSelectPanel`,
`confirmPanel`, `optionsPanel`, `level1Button`, `level2Button`, `levelSelectCloseButton`,
`confirmYesButton`, `confirmNoButton`.

All buttons/panels should have **Navigation = None** so arrow keys and Enter stay
gameplay-only and never steal focus.

### 4.5 Scene: Level_01

`Assets/_Game/Scenes/Level_01.unity`. 30 coins required (32 placed), **no enemy**, **no
timer**, fall = Game Over.

#### 4.5.1 Hierarchy

```
Level_01
├─ Environment
│  ├─ Island_Start      Cube pos (0, -0.5, 0)  scale (18, 1, 18)   M_Ground_Grass   top y=0
│  ├─ Bridge_01         Cube pos (0, -0.3, 14) scale (4, 0.6, 12)  M_Bridge_Wood    top y=0
│  ├─ Island_Mid        Cube pos (0, -0.5, 27) scale (14, 1, 14)   M_Ground_Grass   top y=0
│  ├─ Step_01           Cube pos (6, 0.5, 30)  scale (3, 1, 3)     M_Ground_Rock    top y=1.0
│  ├─ Platform_High     Cube pos (9, 1.0, 34)  scale (6, 1, 6)     M_Ground_Rock    top y=1.5
│  ├─ Rock_01           Cube pos (-6.5, 0.5, 5)   scale (2, 1, 2)     M_Ground_Rock  (scaled/decor)
│  ├─ Rock_02           Cube pos (6, 0.3, 23)     scale (2.5, 1.6, 2) M_Ground_Rock
│  ├─ Rock_03           Cube pos (-5, 0.2, 26)    scale (3, 0.9, 2.4) M_Ground_Rock
│  ├─ Landmark_Pillar   Cube pos (-5.5, 2, 30)    scale (1.5, 4, 1.5) M_Ground_Rock
│  └─ Landmark_Orb      Sphere pos (-5.5, 4.4, 30) scale (0.8,0.8,0.8) M_Robot_Accent
├─ Coins                (CoinSpawner — creates 12 coins at runtime)
├─ PlayerSpawn          empty, pos (0, 0.05, -4)  + PlayerSpawnPoint (script)
├─ Player               (Player prefab instance at PlayerSpawn)
├─ Main Camera          (ThirdPersonCamera script, target = Player)
├─ Directional Light
├─ RuntimeNavMeshBaker  (script; collectObjects = All, useGeometry = PhysicsColliders,
│                        layerMask = Ground only)
├─ LevelController (script)
│  └─ definition = Level_01.asset ; player = Player ; playerSpawn = PlayerSpawn
│     deathHeight = -8 ; checkpoint = (none / off)
├─ EventSystem          (EventSystem + InputSystemUIInputModule)
└─ Canvas
   ├─ Hud                (HudController) — coin counter + objective
   ├─ PauseMenu          (PauseMenuController) — hidden
   └─ ResultPanel        (ResultPanelController) — hidden
```

#### 4.5.2 Coins (Level 1)

No coins are placed by hand. A single **`CoinSpawner`** under `Coins` keeps **12** coins in
play, and every **3 seconds** all 12 jump to new random positions. Coins sit **1.0 m**
above the surface found by raycast.

The spawn box is `areaCenter = (1.5, 14)`, `areaSize = (22, 47)`, which covers every
walkable surface of the level:

- **Island_Start (0,0)** — 18 × 18, `y = 0`
- **Bridge_01 (0,14)** — 4 × 12, `y = 0`
- **Island_Mid (0,27)** — 14 × 14, `y = 0`
- **Step_01 (6,30)** and **Platform_High (9,34)** — `y = 1.0` / `y = 1.5`

Spots with no floor under them (the gaps between the islands) are rejected by the ray
cast, so a coin never floats over the void. Spots on top of the rocks or the landmark
pillar are rejected too, because those live under `Decorations`.

> **To build by hand:** add an empty object `CoinSpawner` under `Coins`, attach the
> `CoinSpawner` script, assign the `Coin` prefab, set `activeCoins = 12`,
> `relocateInterval = 3`, the box above, and `groundMask = Ground`.

> **Why the pool keeps refreshing:** the objective is to **collect 30 coins**, not to
> find 30 coins. Because collected coins come back on the next wave, the level is always
> winnable, no matter how many coins the player misses.

#### 4.5.3 UI on Level 1

`Hud` (`HudController`): `levelText` "Niveau 1", `coinText` "0 / 30", `objectiveText`
"Récupérez 30 pièces", `timerGroup` hidden (no timer on level 1).

`ResultPanel` (`ResultPanelController`): `titleText` shows **"Victoire !"** on win and
**"Perdu"** on loss; `reasonText` explains why; `nextLevelButton` "Niveau 2",
`replayButton` "Réessayer" (also used as Rejouer), `mainMenuButton` "Menu principal".

`PauseMenu` (`PauseMenuController`): "Pause", buttons "Reprendre", "Réessayer",
"Options", "Menu principal" + shared options panel.

### 4.6 Scene: Level_02

`Assets/_Game/Scenes/Level_02.unity`. 50 coins required (52 placed), **1 enemy**,
**180 s** countdown, disappearing floors, one moving platform, fall = Game Over.

#### 4.6.1 Hierarchy

```
Level_02
├─ Environment
│  ├─ Island_Start      Cube pos (0, -0.5, 0)      scale (18, 1, 18)   M_Ground_Grass  top y=0
│  ├─ Bridge_01         Cube pos (0, -0.3, 14)     scale (4, 0.6, 12)  M_Bridge_Wood
│  ├─ Island_Mid        Cube pos (0, -0.5, 27)     scale (16, 1, 16)   M_Ground_Grass
│  ├─ Bridge_02         Cube pos (10, -0.3, 27)    scale (12, 0.6, 4)  M_Bridge_Wood
│  ├─ Island_East       Cube pos (24, -0.5, 27)    scale (16, 1, 16)   M_Ground_Grass
│  ├─ Hazard_Ledge      Cube pos (33.5, -0.5, 27)  scale (3, 1, 6)     M_Ground_Rock
│  ├─ Island_Goal       Cube pos (50.5, -0.5, 27)  scale (8, 1, 8)     M_Ground_Grass
│  ├─ Rock_01..Rock_06  decor (see builder array)  M_Ground_Rock
│  ├─ Landmark_Pillar   Cube pos (-5.5, 2, 31)     scale (1.5, 4, 1.5) M_Ground_Rock
│  └─ East_Pillar       Cube pos (30, 1.75, 33)    scale (1.5, 3.5, 1.5) M_Ground_Rock
├─ Hazards
│  ├─ Floor_Hole_01     Floor_Hole prefab pos (39, -0.125, 27)  startDelay 0.0
│  ├─ Floor_Hole_02     Floor_Hole prefab pos (42, -0.125, 27)  startDelay 1.3
│  ├─ Floor_Hole_03     Floor_Hole prefab pos (45, -0.125, 27)  startDelay 2.6
│  └─ MovingPlatform_01 Moving_Platform prefab pos (12, -0.2, 4)
│                       scale (3, 0.4, 3), travelOffset (5, 0, 0)
├─ Coins                (CoinSpawner — creates 16 coins at runtime)
├─ EnemySpawn           empty pos (5, 0.1, 33)
├─ Enemy                (Enemy prefab instance at EnemySpawn)
├─ PlayerSpawn          empty pos (0, 0.05, -4)  + PlayerSpawnPoint
├─ Player               (Player prefab instance)
├─ Main Camera          (ThirdPersonCamera, target = Player)
├─ Directional Light
├─ RuntimeNavMeshBaker  (collectObjects = All, useGeometry = PhysicsColliders, Ground only)
├─ LevelController
│  └─ definition = Level_02.asset ; player = Player ; playerSpawn = PlayerSpawn
│     deathHeight = -8
├─ EventSystem
└─ Canvas
   ├─ Hud                (level, coin "0 / 50", timer "3:00", tutorial panel)
   ├─ PauseMenu
   └─ ResultPanel
```

> **Navigation caveat (Option B):** the enemy must never be assumed to follow the
> disappearing floors or the moving platform. The hazard plates and the moving platform
> are separated from the permanent routes by **~2.5 m / 1.5 m** jump gaps, so their
> navmesh patches are **disconnected** from the enemy's route. The enemy therefore stays
> on permanent ground; it will not walk off a plate that vanished.

> **Bonus area:** `Island_Bonus` at (22, 4) size 8×6 is placed for the moving platform
> route (offsets/positions are in the builder array too). Its navmesh is likewise
> separate from the enemy's, keeping the chaser on solid ground.

#### 4.6.2 Coins (Level 2)

Same system as Level 1, with a bigger pool: **16** coins in play, relocated every
**3 seconds**. The box is `areaCenter = (23, 13)`, `areaSize = (64, 45)`, which covers:

- Island_Start, Bridge_01, Island_Mid, Bridge_02, Island_East
- Hazard_Ledge and the collapsible plates are inside the box, but the plates sit under
  `Hazards`, so **no coin is ever placed on a plate** — and never on the moving platform
  either. That is deliberate: a coin left hovering where the floor collapsed would be
  unreachable.
- Island_Goal (46.5 … 54.5, z 23 … 31) and Island_Bonus (18 … 26, z 1 … 7) are both
  inside the box, so the far corners of the level do get coins.

> Level 2 teaches the same rule as before, only with moving coins: pick up **50 coins**
> before the 3-minute timer runs out, and don't get caught by the chaser.

#### 4.6.3 UI on Level 2

`Hud` (`HudController`): `levelText` "Niveau 2", `coinText` "0 / 50", `timerText`
counts down from "3:00"; `timerGroup` visible; `tutorialPanel` shows the aim
("Récupérez 50 pièces avant la fin du temps ! Évitez l'ennemi.") and can auto-hide.

`ResultPanel`: on timeout, `reasonText` = **"Temps écoulé !"**; on capture,
**"Attrapé !"**; on fall, **"Chute !"**. `nextLevelButton` on level 2 loops back to
the menu or restarts level 2 (per `LevelDefinition.nextScene`).

### 4.7 Build Settings

`File → Build Profiles` (Unity 6) or `File → Build Settings`:

1. Add scenes in this order:
   1. `Assets/_Game/Scenes/MainMenu.unity`
   2. `Assets/_Game/Scenes/Level_01.unity`
   3. `Assets/_Game/Scenes/Level_02.unity`
2. Disable the template's default `SampleScene` if present.
3. Confirm `EditorBuildSettings.asset` lists all three with enabled checkmarks.

> `SceneLoader` refuses to load a scene that is not in this list, printing a clear
> error. If `SceneLoader` is missing, `LevelController` falls back to
> `Application.CanStreamedLevelBeLoaded` + `SceneManager.LoadScene`.

---

## Part 5 — Controls

| Action | Keyboard | Gamepad |
|--------|----------|---------|
| Move | `W A S D` **or** arrow keys | left stick |
| Sprint | `Left Shift` | left stick click |
| Jump | `Space` | south button (A / ✕) |
| Look | Mouse | right stick |
| Pause | `Esc` | Start |
| UI navigate | `Esc` then mouse | — |

Movement is **camera-relative**: forward always means "away from the camera". The
`GameInput` class defines the `Gameplay` action map **in code** (WASD + arrows + gamepad
in one 2D vector), so an AZERTY keyboard works without remapping. Only the UI
`EventSystem` uses the template `.inputactions` file.

**Tuning** (in `PlayerController`): move 6 m/s, sprint ×1.45, jump height 1.4 m,
gravity −22 m/s², coyote time 0.12 s, jump buffer 0.12 s. Max jump distance ≈ 4.28 m,
so the 2.5 m gaps in Level 2 are comfortably jumpable.

---

## Part 6 — Test checklists (milestones 1–6)

Run these in order. Each milestone depends on the previous one.

### Milestone 1 — Foundation, player, camera

- [ ] Project opens with no compile errors (Console is clean).
- [ ] Press Play on `Level_01`: you fall onto `Island_Start` and stand still.
- [ ] `W A S D` / arrows move you; movement is camera-relative.
- [ ] `Space` jumps; you cannot double-jump in mid-air.
- [ ] The camera follows smoothly and does **not** clip through the island when you
      back into a wall.
- [ ] Mouse look orbits; the player turns to face movement direction.

### Milestone 2 — Coins, HUD, single win condition

- [ ] Level 1 shows `0 / 30` in the HUD and **12 coins** are visible on the map.
- [ ] Every **3 seconds** all the coins jump to new random positions on the islands.
- [ ] Coins only ever appear on solid ground — never floating over the void, never
      inside a rock, never on top of the landmark pillar.
- [ ] Walking through a coin increments the counter **exactly once** (and the coin comes
      back somewhere else on the next wave).
- [ ] Standing in one spot for 5 seconds never lets a coin spawn on top of you.
- [ ] At **29** coins there is **no** victory.
- [ ] Collecting the **30th** coin shows **"Victoire !"** and freezes gameplay.
- [ ] Walking off the island drops you and shows exactly **one** Game Over.

### Milestone 3 — Pause, results, restart

- [ ] `Esc` opens the pause menu; time is frozen and the cursor appears.
- [ ] While paused, WASD/mouse do nothing.
- [ ] "Reprendre" resumes with the cursor hidden again.
- [ ] "Réessayer" from either pause or result **fully** resets the level
      (all coins back, counter at 0, player at spawn, timer at 3:00).
- [ ] The result panel shows the correct **reason** (fall / caught / time out / win).
- [ ] Only one result panel ever appears; you can never trigger two outcomes.
- [ ] "Menu principal" returns to the main menu.

### Milestone 4 — Main menu & level flow

- [ ] Playing `MainMenu` works with **no** bootstrap scene.
- [ ] "Nouvelle partie" loads `Level_01`.
- [ ] Winning Level 1 makes "Niveau 2" available.
- [ ] "Continuer" resumes the furthest unlocked level.
- [ ] "Sélection de niveau" can start Level 1 or 2 (2 only if unlocked).
- [ ] "Quitter" leaves play mode / closes the build.
- [ ] Options sliders (master / music / SFX / sensitivity) persist after restart.

### Milestone 5 — Level 2 systems (timer, enemy, hazards)

- [ ] Level 2 HUD shows the countdown from **3:00**.
- [ ] At 0:00 the result screen shows **"Temps écoulé !"** and freezes play.
- [ ] The enemy activates after ~1.5 s and chases the player across the navmesh.
- [ ] Touching the enemy ends the level with a single capture Game Over.
- [ ] The enemy **never** walks onto the moving platform or a floor hole.
- [ ] The orange floor plates **flash before dropping** (telegraphed), then you fall.
- [ ] The moving platform carries you with **no** jitter or sliding.
- [ ] Falling into a gap with a floor hole briefly gone gives one Game Over.

### Milestone 6 — Polish, audio, save, build

- [ ] Coin pickup plays a sound; the count reacts instantly.
- [ ] Victory / defeat play distinct sounds.
- [ ] Music loops in the menu and in levels.
- [ ] Best times / unlocked levels survive closing and reopening the game.
- [ ] Colour palette matches (grass green, wood brown, gold coins, red enemy,
      orange hazard).
- [ ] A desktop build runs at a stable frame rate and the window can be resized.
- [ ] All French labels render with **no** missing-glyph boxes.

---

## Part 7 — Troubleshooting

| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| Builder shows a dialog and does nothing | TMP Essentials not imported | `Window → TextMeshPro → Import TMP Essential Resources`, re-run |
| `Shader 'Universal Render Pipeline/Lit' not found` | Project not on URP | Assign the URP asset in Project Settings → Graphics |
| Text shows as empty/boxes | TMP font asset missing | Re-import TMP Essentials; check the TMP font on the label |
| Player floats or falls through | Player root has a Rigidbody, or ground not on `Ground` layer | Remove Rigidbody; set surface layer to `Ground` |
| Coins never get collected | Missing `PickupSensor` (kinematic RB + trigger sphere) | Re-add it under the Player root |
| No coins appear at all | No `CoinSpawner` in the scene, or its `coinPrefab` is empty | Re-run the builder; or add the spawner and assign the `Coin` prefab |
| Coins appear in odd places | Spawn box too large / covers decorations | Shrink `areaCenter` & `areaSize`; keep `Decorations` + `Hazards` in `excludedParentNames` |
| Coins are placed on a rock or pillar | The rock is not under a parent named `Decorations` | Put the rocks/pillars under `Decorations`, or add their parent name to `excludedParentNames` |
| Enemy stands still / never moves | NavMesh not baked, or agent not on navmesh | Ensure `RuntimeNavMeshBaker` exists, Ground layer set, enemy over ground |
| Enemy walks off the world | It was given a route across the hazard platform | Keep it on permanent ground (Option B) |
| "Scene X is not in the build list" | Scene not added to Build Settings | Add all three scenes (4.7) |
| Two result panels / mixed outcome | A second script also ends the level | Only `LevelController` may finalise a result |
| Arrow keys move the UI cursor | Button Navigation not None | Set every button's Navigation to `None` |
| `Duplicate assembly 'System.Runtime.CompilerServices.Unsafe.dll'` | Harmless template noise | Ignore |
| Clicking a button does nothing | Its `onClick` was never wired | Re-run the builder, or wire it in the Inspector |

---

## Part 8 — Build & publish

1. Finish Part 6 checklist first (no point shipping an untested build).
2. `File → Build Profiles` (Unity 6) → **Windows / macOS / Linux** desktop target.
3. Add the three scenes in order (see 4.7). Development Build **off** for a release.
4. Set Player settings: product name `Sky Coin Adventure`, company name, a default
   icon, resolution default 1920×1080, "Run in Background" as you prefer.
5. Click **Build**. Choose an output folder **outside** `Assets/`.
6. Run the produced `.exe` (or `.app`) and re-run the Milestone 6 checklist on the build.

To publish on itch.io or Steam you only need the built folder; nothing else is required.

---

## Part 9 — How the systems work

### 9.1 Single decision path (win/lose)

`LevelController` is the **only** authority for the outcome. It runs at
`[DefaultExecutionOrder(500)]`, after every other gameplay script.

Order of loss checks each frame: **fall → enemy capture → timer → victory.** The enemy
*reports* a capture (`ReportEnemyCapture()`); it does not decide. A win is deferred via
`_victoryPending` and committed **last**, so on a same-frame tie (you grab the final
coin the same frame you are caught) **defeat wins**. Exactly one final result is shown.

### 9.2 State, time and cursor

`GameManager` is the sole owner of `Time.timeScale` and the cursor:

| State | timeScale | cursor |
|-------|-----------|--------|
| Boot / MainMenu / Playing | 1 | hidden (Playing) |
| Paused / Victory / GameOver | 0 | visible |

### 9.3 Persistent manager layer

`GameManager`, `GameInput`, `AudioManager`, `SaveService` and `SceneLoader` are
**auto-created in code** by `GameBootstrap` using
`[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` + `DontDestroyOnLoad`. There is **no
Bootstrap scene** and no `GameRoot` in any scene — pressing Play on **any** scene works.

### 9.4 Movement

The Player uses **CharacterController only** (never Rigidbody + CharacterController
together). Gravity, jump, coyote time, jump buffering and teleporting are all custom.
The camera is a custom `ThirdPersonCamera` (Cinemachine is deliberately not used — it
is not installed). Anti-clipping keeps the camera out of geometry, and the camera does
not move while paused.

### 9.5 Moving platform

`MovingPlatform` runs at `[DefaultExecutionOrder(-150)]`, **before** `PlayerController`
at -100, and `PlatformCarrier` reports the platform's delta so the carried player moves
in the same frame — no rubber-banding.

### 9.6 NavMesh

`RuntimeNavMeshBaker` (`[DefaultExecutionOrder(-1000)]`) bakes the navmesh at runtime
using `NavMeshSurface.BuildNavMesh()` with `collectObjects = All`,
`useGeometry = PhysicsColliders`, `layerMask = Ground only`. This avoids a manual bake
step and keeps the navmesh consistent with the built geometry.

### 9.7 Coins

Coins are **created and repositioned at runtime** by a `CoinSpawner`. There is no fixed
list of coin positions in the level any more.

- The spawner keeps `activeCoins` coins alive (12 on Level 1, 16 on Level 2).
- Every `relocateInterval` seconds (3 s) it moves **all** of them to new random positions
  inside its spawn box, and re-activates any that were collected.
- A candidate position is accepted only when a downward ray cast finds real ground on the
  `Ground` layer, the ground is flat enough (≤ 45°), the hit object is not under
  `Decorations` or `Hazards`, the spot is ≥ 3.5 m from the player (XZ) and ≥ 2.5 m from
  the other coins. Otherwise the spot is re-rolled, up to `placementAttempts` times.
- The coin is placed at `hit.point + Vector3.up * 1.0`, so it always sits a jump-friendly
  height above a surface the player can stand on.
- Collecting is idempotent: `Coin` flags itself before reporting to `LevelController`, so
  a coin can never be counted twice. `LevelController` owns the count; the spawner only
  supplies the coins.

---

## Part 10 — File inventory

29 C# files under `Assets/_Game/Scripts/`:

**Core**
- `GameBootstrap.cs` — auto-creates the persistent managers before any scene loads.
- `GameManager.cs` — state machine; sole owner of timeScale + cursor; one-result guard.
- `GameState.cs` — the state enum.
- `GameScenes.cs` — scene-name constants (`MainMenu`, `Level_01`, `Level_02`, `ForLevelNumber`).
- `LevelController.cs` — single decision path; coin validation; retry/scene flow.
- `LevelDefinition.cs` — ScriptableObject per level (coins, timer, enemy, nextScene, practiceMode).
- `SceneLoader.cs` — safe scene loading with a clear error if a scene is not in Build Settings.

**Input**
- `GameInput.cs` — code-defined `Gameplay` action map (WASD + arrows + gamepad; Esc = Pause).

**Player**
- `PlayerController.cs` — CharacterController movement (move, sprint, jump, coyote, buffer, teleport).
- `PlayerSpawnPoint.cs` — the spawn marker.
- `PlayerVisuals.cs` — cosmetic limb animation.

**Camera**
- `ThirdPersonCamera.cs` — smooth third-person orbit + follow + anti-clipping.

**Collectibles**
- `Coin.cs` — spin, bob, single idempotent pickup; `PlaceAt()` re-arms a reused coin.
- `CoinSpawner.cs` — owns every coin; relocates all of them to validated ground every 3 s.

**Enemies**
- `EnemyChaser.cs` — navmesh chase; reports capture, never decides.

**Hazards**
- `DisappearingFloor.cs` — telegraphed drop + timed respawn.
- `MovingPlatform.cs` — ping-pong travel with a deterministic offset.

**Environment**
- `PlatformCarrier.cs` — carries the player with the platform.
- `RuntimeNavMeshBaker.cs` — runtime navmesh bake.

**UI**
- `HudController.cs`, `PauseMenuController.cs`, `ResultPanelController.cs`,
  `OptionsPanelController.cs`, `MainMenuController.cs`.

**Audio**
- `AudioManager.cs` — procedurally generated music + SFX; override clips via serialized fields.

**Saving**
- `SaveService.cs` — PlayerPrefs wrapper (unlocked levels, best times, options).

**Editor** (not shipped in the build)
- `SkyCoinProjectBuilder.cs` — `2. Build The Whole Game`, `3. Validate The Built Scenes`, `4. Reset Saved Progress`.
- `SkyCoinFactory.cs` — primitive/UI/serialized-field helpers.
- `SkyCoinMaterialBuilder.cs` — `1. Create Materials`.

---

## Part 11 — Optional polish

Not required to finish, but nice:

- A particle burst on coin pickup (assign a ParticleSystem prefab to `Coin`).
- Replace the generated music pad with a real loop: drop a clip into the
  `AudioManager` music override field.
- A **checkpoint / practice mode** per level (a `LevelDefinition.checkpoint` and
  `practiceMode` flag are already in the data model; it is **off** by default so a fall
  is always a Game Over in the shipped levels).
- More levels: duplicate a scene, swap the `LevelDefinition`, add coins and geometry.

---

*End of guide.*
