# ThemeParkGame - Scene Setup Guide

## Overview

This project includes Editor extension scripts that automatically construct the Unity scene hierarchy and generate all required prefabs. Since Unity scene/prefab files use a binary serialization format with internal GUIDs, they cannot be created outside the Unity Editor.

## Prerequisites

- Unity 2021.3 LTS or later
- TextMeshPro (imported via Package Manager)
- AI Navigation package (for NavMeshAgent/NavMeshSurface)

## Setup Steps

### 1. Open the Project in Unity

Open the `ThemeParkGame` folder as a Unity project. Wait for all scripts to compile.

### 2. Register Tags & Layers

**Menu: ThemeParkGame > Setup Tags & Layers**

This registers the following custom tags and layers used throughout the codebase:

**Tags:**
- `Attraction`, `FoodShop`, `DrinkShop`, `SouvenirShop`
- `Toilet`, `Bench`, `TrashCan`, `InfoBoard`
- `ParkExit`, `Staff`, `Visitor`
- `StaffRoom`, `ResearchLab`, `Pathway`

**Layers:**
- Layer 8: `Ground` (used by BuildPanelUI for placement raycasting)
- Layer 9: `Facility` (all placed facilities)
- Layer 10: `Visitor` (visitor entities)
- Layer 11: `Staff` (staff entities)

### 3. Build the Scene Hierarchy

**Menu: ThemeParkGame > Setup Scene**

This creates the full scene structure:

```
--- Managers ---
  GameManager       (GameManager + all subsystems + SceneBootstrapper)
  AudioManager      (AudioManager singleton)
  InputManager      (InputManager singleton)

--- Environment ---
  MainCamera        (Camera + AudioListener, positioned at bird's eye view)
  DirectionalLight  (Warm sunlight with soft shadows)
  Ground            (200x200 ground plane on Ground layer)
  ParkEntrance      (Tagged ParkExit, trigger collider)

--- Park Content ---
  Attractions       (Parent for placed attractions)
  Shops             (Parent for placed shops)
  Facilities        (Parent for toilets, benches, etc.)
  Pathways          (Parent for path segments)
  Decorations       (Parent for decorative items)

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

### 4. Generate Prefabs

**Menu: ThemeParkGame > Generate Prefabs**

This generates all prefabs under `Assets/Prefabs/`:

| Prefab | Path | Key Components |
|--------|------|----------------|
| Visitor | `Prefabs/Visitor/` | NavMeshAgent, CapsuleCollider, VisitorAI, EmotionBubble (child) |
| Staff_Mechanic | `Prefabs/Staff/` | NavMeshAgent, MechanicStaff |
| Staff_Cleaner | `Prefabs/Staff/` | NavMeshAgent, CleanerStaff |
| Staff_Entertainer | `Prefabs/Staff/` | NavMeshAgent, EntertainerStaff |
| Staff_Guard | `Prefabs/Staff/` | NavMeshAgent, GuardStaff |
| Staff_Scientist | `Prefabs/Staff/` | NavMeshAgent, ScientistStaff |
| Attraction_Generic | `Prefabs/Attraction/` | Attraction, BoxCollider, FacilityDirt, QueueArea |
| Shop_Food | `Prefabs/Shop/` | Shop, BoxCollider, FacilityDirt |
| Shop_Drink | `Prefabs/Shop/` | Shop, BoxCollider, FacilityDirt |
| Shop_Souvenir | `Prefabs/Shop/` | Shop, BoxCollider, FacilityDirt |
| Facility_Toilet | `Prefabs/Facility/` | ToiletFacility, FacilityDirt |
| Facility_Bench | `Prefabs/Facility/` | BenchFacility |
| Facility_TrashCan | `Prefabs/Facility/` | GenericFacility |
| Facility_InfoBoard | `Prefabs/Facility/` | GenericFacility |
| Facility_StaffRoom | `Prefabs/Facility/` | GenericFacility |
| Facility_ResearchLab | `Prefabs/Facility/` | GenericFacility |
| Facility_ParkEntrance | `Prefabs/Facility/` | BoxCollider (trigger) |
| Facility_Pathway | `Prefabs/Facility/` | Quad mesh |
| UI_ChatBubble_Player | `Prefabs/UI/` | Image, TextMeshProUGUI, LayoutElement |
| UI_ChatBubble_NPC | `Prefabs/UI/` | Image, TextMeshProUGUI, LayoutElement |
| UI_CategoryTab | `Prefabs/UI/` | Button, Image, TextMeshProUGUI |
| UI_ItemCard | `Prefabs/UI/` | Button, CanvasGroup, Image, TextMeshProUGUI |
| UI_StaffListItem | `Prefabs/UI/` | Button, Slider (Fatigue/Skill), TextMeshProUGUI |
| UI_QuickReplyButton | `Prefabs/UI/` | Button, TextMeshProUGUI |

### 5. Bake NavMesh

After scene setup, bake the NavMesh for visitor/staff pathfinding:

1. Select the `Ground` object
2. Open Window > AI > Navigation
3. In the Bake tab, click "Bake"

### 6. Wire SerializeField References

After generating prefabs and the scene hierarchy, use the Unity Inspector to wire up SerializeField references (sprites, prefab slots, etc.) that cannot be auto-assigned by the editor scripts.

### 7. Save and Play

Save the scene as `Assets/Scenes/MainScene.unity` and enter Play mode.

## SceneBootstrapper

The `SceneBootstrapper` component (on the GameManager object) provides a safety net: if you enter Play mode from any scene that lacks Manager objects, it automatically creates them. This is useful during development when testing individual scenes.

## Troubleshooting

- **"Tag not found" errors**: Run Setup Tags & Layers first
- **Missing components on prefabs**: Re-run Generate Prefabs
- **NavMeshAgent errors**: Ensure NavMesh is baked on the Ground
- **TMPro missing**: Import TextMeshPro via Package Manager (Window > Package Manager)
