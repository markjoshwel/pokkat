# Pokkat

Immersive Technology Development (ITD) Assignment 1 by Mark and Arwen

## Project Details

### About

Pokkat is an augmented reality (AR) tamagotchi-style cat care game built with Unity and AR Foundation. Players use their device's camera to scan a tracker image, spawning virtual neko (cat) characters into their real-world environment. The game features:

- **AR Image Tracking**

  Scan the Pokkat tracker to spawn your main neko into the AR scene

- **AR Plane Detection**

  The game detects horizontal surfaces (floors, tables) for realistic grounding

- **Multi-Cat Spawning**

  Use multiple physical tracker prints to spawn friend nekos (up to 3 total)

- **Tamagotchi Mechanics**

  Hunger and happiness stats that decay over time (0.1 per hour)

- **Interactive Care**

  Feed your cat by placing food bowls, pet them, and watch them play with friends

- **Persistent Stats**

  Your cat's hunger, happiness, and appearance are saved between sessions. _There exists code that would have been used by Darren to save and load data to a backend like Firebase, but due to how things played out, have been unused._ (`PokkatCore.Statskeeper.LoadFromDict` and `PokkatCore.Statskeeper.SaveToDict`)

- **Time-based Statistic Decay**

  While the app is closed, your neko's stats will decay over time. Both hunger and happiness decrease at a rate of **10% per hour** (0.1 per hour). When you reopen the app, the game calculates how many hours have passed since your last session and applies the appropriate decay. If your neko's hunger reaches 0%, the cat dies! Make sure to check in regularly to keep your neko happy and fed.

## Instructions

1. **Open the application**, you will be greeted with the main menu:
   - **Start Game:** Brings you to the game screen.
   - **Settings:** Allows you to configure audio volumes.
   - **Exit Game:** Leaves the application.

2. Optionally, tap on the **"Settings"** button to configure audio volumes. Within the Settings page, click on **"Exit"** to leave the menu.

   There are three audio sliders you may adjust:
   - **Master Volume:** Controls the overall volume of the game.
   - **Music Volume:** Controls the background music in relation to the master volume.
   - **Sound Effects:** Controls the sound effects volume in relation to the master volume.

   > Note: Volume uses logarithmic scaling for natural-sounding audio adjustment.

3. Tap on **"Start Game"** to enter the game screen and start the experience.

   You will be presented with a view of your device's camera, and a gameplay heads-up display (HUD) that shows two bars:
   - **Hunger Bar:** (Depicted with a food can icon) Indicates the cat's hunger level (0-100%).
   - **Happiness Bar:** (Depicted with a heart icon) Indicates the cat's happiness level (0-100%).

   > **Time Decay**: Stats decrease by 10% per hour while the app is closed. If hunger reaches 0%, the cat dies!

4. **Follow the prompt text** displayed on screen:
   - **"Scan the Pokkat tracker to spawn a cat!"** - Point your camera at the Pokkat tracker image to spawn your main neko.
   - **"Move your phone around to detect surfaces!"** - After spawning, slowly pan your phone to detect floor/table surfaces. The neko will fall and land on detected planes.
   - **"Move your phone around to detect more surfaces!"** - The neko needs sufficient plane detection for roaming behaviour.
   - **No prompt text (empty)** - Your neko is happy and the game is in a normal state. You can now interact freely.

5. **Interact with your cat** using the following methods:
   - **Spawning a Food Bowl**: Tap anywhere on a detected surface to place a food bowl. The main neko will walk towards the bowl and eat, restoring **+50% hunger**. Only one bowl can exist at a time; tapping again replaces the bowl.
   - **Petting via Touch**: Tap directly on the neko on your screen. The neko will turn to face you, blink, and bounce happily, restoring **+5% happiness**.
   - **Petting via UI Button**: Tap the "Pet" button on the HUD to pet the main neko (if available).
   - **Spawning Friend Nekos**: Either scan a new tracker at least 25cm away, or remove the existing tracker from view, then move it to a new location (at least 25cm away from any existing nekos) and scan again. A friend neko with a random texture will spawn! You can have up to 3 nekos total.
   - **Playing with Friends**: When a friend neko spawns near the main neko, they will turn to face each other and jump together happily, restoring **+50% happiness**.

6. **Satiation State**: Once the cat has been fed (hunger > 50%) and has been petted or played with a friend (happiness > 50%), the game displays: _"The cat is satiated, you can come back again later!"_

7. At any point, or once the cat is satiated and you need not continue interacting, tap on the **"Exit"** button to leave the game and return to the main menu. Your stats are saved automatically.

## Platform and Hardware Requirements

### Supported Platforms

| Platform | Minimum Version       | AR Framework |
| -------- | --------------------- | ------------ |
| Android  | Android 11.0 (API 30) | ARCore       |

> iOS should technically be supported via ARKit, and I see no reason for my code to not work on iOS, but I did not build for it.

### Hardware Requirements

- **Camera**: Device must have a rear-facing camera with AR capabilities
- **Motion Sensors**: Accelerometer and gyroscope required for AR tracking
- **ARCore/ARKit Support**: Device must be certified for ARCore (Android) or support ARKit (iOS)
  - [List of ARCore supported devices](https://developers.google.com/ar/devices)
  - ARKit requires iPhone 6s or later, iPad Pro, iPad (5th gen) or later

### Tracker Image

The Pokkat tracker image ([`Design/TrackingMarker/pokkat-tracking-marker.{jpg,pdf,png}`](https://forge.joshwel.co/mark/pokkat/src/branch/main/Design/TrackingMarker/pokkat-tracking-marker.pdf)) must be printed or displayed on another screen for scanning. For best results:

- Print A4-szied
- Use a flat, non-reflective surface
- Ensure good lighting conditions
- Print multiple copies to spawn friend nekos (or bring it in and out of frame)

## Limitations and Bugs

### Known Limitations

1. **Maximum Nekos**: Only 3 nekos can be spawned simultaneously (1 main + 2 friends). This is deliberate to prevent excessive resource usage and ensure smooth gameplay.

2. **Single Bowl**: Only one food bowl can exist at a time. Placing a new bowl destroys the previous one. This is deliberate.

3. **Idle Roaming Disabled**: The idle roaming behaviour (where nekos walk around randomly) is currently disabled due to edge detection issues with real-world AR plane boundaries. When previously enabled, nekos may walk off detected surfaces.

4. **NavMesh Not Supported**: Unity's NavMesh system does not work reliably with AR Foundation's dynamic plane geometry. The game uses direct AR plane projection instead.

5. **Image Tracking Limitation**: AR systems (ARCore/ARKit) cannot distinguish between multiple physical prints of the same reference image—they share the same trackable ID. The game uses distance-based detection (>25cm apart) to determine if a new neko should spawn.

6. **Plane Drift**: AR planes continuously update their position as tracking refines. Nekos and bowls use XZ-locked anchoring with Y-only stabilisation to prevent horizontal drift.

7. **Simulated Image Tracking May Not Work In Unity Editor/Play Mode**: This is beyond me.

    **Video**: <https://datashower.joshwel.co/public/pokkat/xrsimenv.mp4> \
    **Screenshot** of `ARTrackablesEventChangedArgs` being called on an Android build: <https://datashower.joshwel.co/public/pokkat/Screenshot%202025-12-22%20225926.png>

    ![Screenshot 2025-12-22 225926.png](https://datashower.joshwel.co/public/pokkat/Screenshot%202025-12-22%20225926.png)

    ```csharp
    // the code i tried to use to debug this
    using System;
    using PokkatCore;
    using UnityEngine;
    using UnityEngine.XR.ARFoundation;
    
    public class Liberty : MonoBehaviour
    {
        [SerializeField] private ARTrackedImageManager trackedImageManager;
        private void Awake()
        {
            Logkat.Dev($"Liberty: manager={trackedImageManager}");
        }
        private void OnEnable()
        {
            trackedImageManager.trackablesChanged.AddListener(TriggerG);
        }
        private void OnDisable()
        {
            trackedImageManager.trackablesChanged.RemoveListener(TriggerG);
        }
        private void TriggerG<T>(ARTrackablesChangedEventArgs<T> args) where T : ARTrackable
        {
            Logkat.Dev("Liberty.TriggerG");
        }
        public void Trigger()
        {
            Logkat.Dev("Liberty.Trigger");
        }
    }
    ```

### Known Bugs

1. **Spawn Overlap**: Occasionally, AR tracking blips may cause spawn attempts too close to existing entities. A 15cm minimum separation distance is enforced but may not always prevent visual overlap.

2. **Tracking Loss During Fall**: If image tracking is lost while the neko is mid-fall, the neko may land at an unexpected height.

## Answer Key / Cheats

- **Quick Friend Spawn**: After the main neko lands, immediately move the tracker to a new spot (>25cm away) and re-scan to spawn friends quickly.

- **Stats Persistence**: Stats are saved in PlayerPrefs. Clearing app data will reset all progress.

- **Respawn Threshold**: Image trackers must be at least 25cm apart to be considered separate neko spawns. Use this to control where friends appear.

## References and Credits

### Graphical Assets and Models

- **Neko Model and Textures**

  Ripped from Neko Atsume Purrfect Kitty Collector, released by Hit-Point Co., Ltd. for the Meta Quest 2+ on 14 December 2023. \
  <https://www.meta.com/en-gb/experiences/neko-atsume-purrfect-kitty-collector/8401739766534648/>

  Ripped by Windows98 on Sketchfab. \
  <https://sketchfab.com/3d-models/neko-47b59a0fc5084ad88657f43df85a4426>

- **Food Bowl** \
  Modelled by Arwen!

- **UI Font: One Little Font** \
  By Konstantina Louka \
  <https://www.fontspring.com/fonts/konstantina-louka/one-little-font>

### Audio and Sounds

- `Game/Assets/Pokkat Core Gameplay/Sounds/jump.mp3` \
  "Cartoon Boing Sound Effect 2" by Jessica Hartell on YouTube \
  <https://www.youtube.com/watch?v=d7vfbyFl5kc>

- `Game/Assets/Pokkat Core Gameplay/Sounds/eat.mp3` \
  "Nom Nom Nom Sound Effect" by UltraStorm on YouTube \
  <https://www.youtube.com/watch?v=UaMKUVxidpM>

- `Game/Assets/Pokkat Core Gameplay/Sounds/step.mp3` \
  "Wood step Sample 1" by Notarget (Freesound) on Pixabay \
  <https://pixabay.com/sound-effects/wood-step-sample-1-47664/>

- `Game/Assets/Pokkat Core Gameplay/Sounds/bowldown.mp3` \
  "Button push - clicky plastic button" by Gamemaster Audio on Uppbeat \
  <https://uppbeat.io/sfx/button-push-clicky-plastic-button-1/362/4895>

- `Game/Assets/Pokkat Core Gameplay/Sounds/pokkat bgm.mp3` \
  "Sweet Cafe | Cute Background Music (Royalty Free)" by Stream Cade on YouTube \
  <https://www.youtube.com/watch?v=6DhONAQfEVg>

- `Game/Assets/Pokkat Core Gameplay/Sounds/meow.mp3` \
  Meow SFX by DRAGON-STUDIO on Pixabay \
  <https://pixabay.com/sound-effects/meow-sfx-405456/>

- `Game/Assets/Pokkat Core Gameplay/Sounds/meow 1 (2).mp3` \
  Cat Meow 1 FX by SOUND_GARAGE on Pixabay \
  <https://pixabay.com/sound-effects/cat-meow-1-fx-306178/>

- `Game/Assets/Pokkat Core Gameplay/Sounds/meow 1 (3).mp3` \
  Cat Meow 7 FX by SOUND_GARAGE on Pixabay \
  <https://pixabay.com/sound-effects/cat-meow-7-fx-306186/>

- `Game/Assets/Pokkat Core Gameplay/Sounds/meow 1 (4).mp3` \
  Cat Meow 8 FX by SOUND_GARAGE on Pixabay \
  <https://pixabay.com/sound-effects/cat-meow-8-fx-306184//>

- `Game/Assets/Pokkat Core Gameplay/Sounds/meow 1 (5).mp3` \
  Cat meow by freesound_community on Pixabay \
  <https://pixabay.com/sound-effects/cat-meow-85175/>
