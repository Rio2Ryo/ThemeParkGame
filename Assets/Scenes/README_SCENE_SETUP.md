# ThemeParkGame - Scene Setup Guide

## Overview

This project includes Editor extension scripts that automatically construct the Unity scene hierarchy and generate all required prefabs. Since Unity scene/prefab files use a binary serialization format with internal GUIDs, they cannot be created outside the Unity Editor.

## Prerequisites

- Unity 2021.3 LTS or later
- TextMeshPro (imported via Package Manager)
- AI Navigation package (for NavMeshAgent/NavMeshSurface)

## Quick Start (Recommended)

**Menu: ThemeParkGame > Full Setup (All)**

This runs all three setup steps in sequence:
1. Tags & Layers registration
2. Scene hierarchy construction
3. Prefab generation

After Full Setup, save the scene and enter Play mode.

## Individual Setup Steps

### 1. Register Tags & Layers

**Menu: ThemeParkGame > Setup Tags & Layers**

Registers the following custom tags and layers:

**Tags:**
- `Attraction`, `FoodShop`, `DrinkShop`, `SouvenirShop`
- `Toilet`, `Bench`, `TrashCan`, `InfoBoard`
- `ParkExit`, `Staff`, `Visitor`
- `StaffRoom`, `ResearchLab`, `Pathway`
- `Litter`, `Vomit`, `Hooligan`, `QueueArea`, `Decoration`

**Layers:**
- Layer 8: `Ground` (used by BuildPanelUI for placement raycasting)
- Layer 9: `Facility` (all placed facilities)
- Layer 10: `Visitor` (visitor entities)
- Layer 11: `Staff` (staff entities)

### 2. Build the Scene Hierarchy

**Menu: ThemeParkGame > Setup Scene**

Creates the full scene structure:

```
--- Managers ---
  GameManager       (GameManager + subsystems + SceneBootstrapper + RuntimeGameSetup)
  AudioManager      (AudioManager singleton)
  InputManager      (InputManager singleton)

--- Environment ---
  MainCamera        (Camera + AudioListener, bird's eye view)
  DirectionalLight  (Warm sunlight with soft shadows)
  Ground            (200x200 ground plane, Ground layer, NavMeshSurface)
  ParkEntrance      (Tagged ParkExit, trigger collider)
  SpawnPoint        (Visitor spawn position)
  ExitPoint         (Visitor exit position, tagged ParkExit)

--- Park Content ---
  Attractions       (Parent for placed attractions)
  Shops             (Parent for placed shops)
  Facilities        (Parent for toilets, benches, etc.)
  Pathways          (Parent for path segments)
  Decorations       (Parent for decorative items)
  Litter            (Parent for litter instances)
  Vomit             (Parent for vomit instances)

--- Entities ---
  Visitors          (Parent for spawned visitor instances)
  Staff             (Parent for hired staff instances)

--- UI ---
  Canvas            (ScreenSpaceOverlay, 1920x1080 reference)
    HUD             (HUDController with TopBar/SpeedControls/InfoBar/BottomBar)
    BuildPanel      (BuildPanelUI, initially hidden)
    StaffPanel      (StaffPanelUI, initially hidden)
    VisitorInfoPanel(VisitorInfoPanel, initially hidden)
    ConversationUI  (ConversationUI, initially hidden)
    TutorialPanel   (TutorialSystem, initially hidden)
  EventSystem       (EventSystem + StandaloneInputModule)
```

### 3. Generate Prefabs

**Menu: ThemeParkGame > Generate Prefabs**

Generates all prefabs under `Assets/Prefabs/`:

| Category | Prefabs | Path |
|----------|---------|------|
| Visitor | Visitor (NavMeshAgent, VisitorAI, EmotionBubble) | `Prefabs/Visitor/` |
| Staff | Mechanic, Cleaner, Entertainer, Guard, Scientist | `Prefabs/Staff/` |
| Attraction | Attraction_Generic (QueueArea, Entrance/Exit points) | `Prefabs/Attraction/` |
| Shop | Food, Drink, Souvenir (FacilityDirt) | `Prefabs/Shop/` |
| Facility | Toilet, Bench, TrashCan, InfoBoard, StaffRoom, ResearchLab, ParkEntrance, Pathway | `Prefabs/Facility/` |
| Environment | Litter, Vomit, Decoration_Generic | `Prefabs/Environment/` |
| UI | ChatBubble (Player/NPC), CategoryTab, ItemCard, StaffListItem, QuickReplyButton, NotificationToast, AchievementToast | `Prefabs/UI/` |

### 4. NavMesh

NavMeshSurface is automatically added to the Ground object by Scene Setup.
NavMesh is baked at runtime by `RuntimeGameSetup` when the game starts.

For manual baking in the editor:
1. Select the `Ground` object
2. Open Window > AI > Navigation
3. Click "Bake"

### 5. Save and Play

Save the scene as `Assets/Scenes/MainScene.unity` and enter Play mode.

## Runtime Bootstrapping

The project supports two modes:

1. **Editor workflow**: Use the editor menus above to set up the scene, then Play.
2. **Code-only workflow**: `GameBootstrapper` (via `[RuntimeInitializeOnLoadMethod]`) creates all managers and UI at runtime. `RuntimeGameSetup` spawns sample attractions, shops, facilities, and staff. This is used for WebGL builds.

Both modes coexist — `SceneBootstrapper` checks for existing managers before creating new ones.

## Troubleshooting

- **"Tag not found" errors**: Run Setup Tags & Layers (or Full Setup)
- **Missing components on prefabs**: Re-run Generate Prefabs
- **NavMeshAgent errors**: Ensure NavMesh is baked (auto-baked at runtime)
- **TMPro missing**: Import TextMeshPro via Package Manager
- **Build errors in Editor scripts**: These are `#if UNITY_EDITOR` guarded and excluded from builds
