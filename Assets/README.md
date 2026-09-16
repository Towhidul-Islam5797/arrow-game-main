# MultiTech Escape PUZZLE GAME

A complete Unity game template for creating arrow rope puzzle games with customizable themes and level management.

## Overview

MultiTech Escape PUZZLE GAME is a Unity game template that provides a complete foundation for building arrow rope puzzle games. The template includes:

- **Level Editor**: Visual level creation and editing tools
- **Theme System**: Customizable themes with multiple color options
- **Grid Management**: Flexible grid-based gameplay system
- **Arrow System**: Advanced arrow movement and collision detection
- **UI System**: Complete UI management for menus, gameplay, and level selection

## Quick Start

### Installation

1. Import the package into your Unity project (Unity 2021.3 or later recommended)
2. Open the main scene: `Assets/MultiTech Studio/Escape Game/Scenes/Level Screen.unity`
3. Access the setup window: `Tools > MultiTech Studio > Escape Game Setup`

### First Steps

1. **Setup Themes**: 
   - Open `Tools > MultiTech Studio > Escape Game Setup`
   - Navigate to the "Theme" tab
   - 7 default themes are automatically created on first launch
   - Customize themes by selecting them and editing their properties
   - Click "APPLY" to apply a theme to the current scene
   - (Optional) Add `ThemeManager` component to enable runtime theme switching

2. **Configure Game Settings**:
   - Adjust game parameters in the LevelManager and GridManager components
   - Customize UI elements in the UiManager component
   - (Optional) Setup runtime theme system by adding ThemeManager component

## Features

### Theme System

The theme system allows you to create and manage multiple visual themes for your game:

- **Theme Icon**: Visual representation for theme selection
- **Arrow Prefab**: Custom arrow prefab for each theme
- **Color Options**: Single or multiple colors per theme
- **Skybox**: Custom skybox material per theme
- **Grid Icon**: Custom grid dot sprite per theme
- **Background**: Optional background sprite

**Editor-Based Theme Application:**
- Apply themes directly in the editor using the Theme Editor window
- Themes are applied to scene objects and prefabs
- Automatically creates `CurrentTheme.asset` for runtime access

**Runtime Theme System (Optional):**
- **CurrentTheme ScriptableObject**: Holds the reference to the active theme
- **ThemeManager Component**: Applies themes at runtime and supports theme switching
- Add `ThemeManager` to your scene for runtime theme support
- Themes can be switched during gameplay
- Fully backward compatible - works without runtime system

> **Note**: If a selected sprite appears too small or too big when applied via the editor window, adjust its **Pixels Per Unit** setting in the sprite's Import Settings. Select the sprite in the Project window, then in the Inspector, adjust the "Pixels Per Unit" value (typically between 100-1000 depending on your sprite resolution).

### SDK Integration

The template includes optional integration with Google Mobile Ads (AdMob), Firebase, Unity In-App Purchases (IAP), and DOTween:

- **DOTween Integration**: Smooth animations for arrows, UI, and grid (optional)
  - Install from Unity Asset Store (free version available)
  - Enable/disable through Settings tab
  - Fallback mode: Instant state changes when disabled
- **AdMob Integration**: Full AdMob support with banner, interstitial, and rewarded ads
- **Firebase Integration**: Firebase analytics and crash reporting
- **Unity IAP Integration**: Complete in-app purchase system with product management
- **Conditional Compilation**: Safe compilation with or without SDKs installed
- **Auto-Detection**: Automatic SDK detection and define management
- **Centralized Configuration**: All monetization settings managed through ProjectSetupData ScriptableObject
- **Easy Configuration**: Configure SDKs through the Settings tab in the setup window

**Access SDK Settings:**
1. Open `Tools > MultiTech Studio > Escape Game Setup`
2. Click on the "Settings" tab
3. View SDK status, enable/disable SDKs, and configure settings

**Project Setup Data:**
- Create a `ProjectSetupData` asset (Right-click > Create > Tools > MultiTech Studio > Escape Game Project Setup)
- Assign the asset to `AdsManager` and `IapManager` components
- All IAP and Ads configuration is managed through this centralized asset

**AdMob Configuration:**
- Create and assign `ProjectSetupData` asset
- Add `AdsManager` component to a GameObject in your scene
- Assign `ProjectSetupData` to AdsManager
- Configure ad unit IDs and interstitial settings in the Settings tab
- All AdMob code is conditionally compiled - project compiles safely without AdMob

**Unity IAP Configuration:**
- Create and assign `ProjectSetupData` asset
- Add `IapManager` component to a GameObject in your scene
- Assign `ProjectSetupData` to IapManager
- Configure IAP products in the Settings tab
- Assign UI references (Button, PriceText) in IapManager Inspector
- All IAP code is conditionally compiled - project compiles safely without Unity IAP

**Firebase Setup:**
- Add `FirebaseManager` component to a GameObject in your scene
- Configure Firebase in your Firebase console
- Automatic initialization when enabled
- All Firebase code is conditionally compiled - project compiles safely without Firebase

### Level Editor

- Visual grid-based level creation
- Arrow path painting tools
- Real-time preview
- Level asset serialization
- Easy level management

### Gameplay Features

- Smooth arrow movement (with DOTween) or instant movement (fallback)
- Collision detection between arrows
- Exit detection when arrows leave the grid
- Level progression system
- Coin and lives management
- Runtime theme switching (optional)
- Conditional animation system (works with or without DOTween)

## Project Structure

```
Assets/MultiTech Studio/Escape Game/
├── Animations/          # Animation controllers and clips
├── Documents/           # License and documentation
├── Editor/              # Editor scripts and tools
│   ├── EscapeGameSetupWindow.cs
│   ├── LevelSaveTool.cs
│   └── ArrowLineBrushEditor.cs
├── Fonts/               # Font assets
├── Levels/              # Level ScriptableObject assets
├── Materials/           # Material assets
├── Prefabs/             # Game prefabs (Arrows, Snakes, Trains, etc.)
├── Scenes/              # Unity scenes
├── Scripts/             # Runtime scripts
│   ├── ThemeData.cs     # Theme configuration ScriptableObject
│   ├── CurrentTheme.cs  # Active theme reference ScriptableObject
│   ├── ThemeManager.cs  # Runtime theme manager component
│   ├── LevelAsset.cs    # Level data ScriptableObject
│   ├── ProjectSetupData.cs # IAP and Ads configuration ScriptableObject
│   ├── ArrowLine.cs     # Arrow line behavior
│   ├── GridManager.cs   # Grid management
│   ├── LevelManager.cs  # Level loading and management
│   ├── AdsManager.cs    # AdMob integration (optional)
│   ├── IapManager.cs    # Unity IAP integration (optional)
│   ├── FirebaseManager.cs # Firebase integration (optional)
│   └── ...
├── Editor/              # Editor scripts
│   ├── EscapeGameSetupWindow.cs  # Main setup window
│   ├── ThemeOperations.cs        # Theme apply/delete operations
│   ├── ThemeHelpers.cs           # Theme helper functions
│   ├── AutoDefines.cs   # SDK auto-detection
│   └── ...
├── Sprites/             # Sprite assets
└── Themes/              # Theme ScriptableObject assets
    ├── CurrentTheme.asset  # Active theme reference (auto-created)
    └── Theme_*.asset       # Theme configuration assets
```

## Requirements

- Unity 2021.3 LTS or later
- Universal Render Pipeline (URP)

### Optional Dependencies

- **DOTween**: For smooth animations (optional - project works without it)
  - Free version available on Unity Asset Store: "DOTween (HOTween v2)"
  - Can be enabled/disabled through the Settings tab
  - Without DOTween: Animations are instant (no tweening), but all functionality works
- **Google Mobile Ads SDK**: For AdMob integration (optional)
- **Firebase SDK**: For Firebase analytics and crash reporting (optional)
- **Unity In-App Purchasing**: For IAP integration (optional)

> **Note**: The template works perfectly without these SDKs. They are optional and can be enabled/disabled through the Settings tab. DOTween provides smooth animations but is not required - the game will use instant state changes as fallback.

## Support

For detailed documentation, see `Document.md`.

For issues or questions, please refer to the project documentation or contact support.

## License

See `Assets/MultiTech Studio/Escape Game/Documents/LICENSE.txt` for license information.

