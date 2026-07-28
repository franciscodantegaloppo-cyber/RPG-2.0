# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

A third-person action-RPG built on the **ExplosiveLLC / RPG Character Mecanim Animation Pack FREE** (the "Fighter RPG 2.0" pack). It is a separate Unity project from `C:\Users\franc\Miciudad 3d` (the Soretopo zombie-shooter) — do not assume any of the Miciudad 3d code/scenes exist here, and vice versa.

- **Engine:** Unity 6000.4.11f1, **URP 17.4.0**, **Input System 1.19.0**, Cinemachine, NavMesh (AI), TextMesh Pro.
- **Open the project from** `C:\Users\franc\rpg 2.0\` (the `.sln` is `rpg 2.0.sln`, two `Assembly-CSharp*.csproj` projects).
- **Two buildable scenes** (in build order, see `ProjectSettings/EditorBuildSettings.asset`):
  1. `Assets/_RPG/Scenes/SpawnVillage.unity` (index 0) — village, NPCs, dungeon entrance.
  2. `Assets/_RPG/Scenes/Dungeon.unity` (index 1) — interior dungeon scene.
  - Plus `Assets/Scenes/SampleScene.unity` (from the pack, not in build) and `Assets/ExplosiveLLC/.../Scenes/RPG-Character.unity` (the pack's demo scene).

## Build, play, test

This is a normal Unity project — there is no Makefile or custom CLI. Run/test from the Editor:

- **Build a player:** `File ▸ Build Profiles… ▸ Build` (the two scenes above are already in the build list).
- **Play in Editor:** open `SpawnVillage.unity` and press Play. The `Player` GameObject (tag = "Player") must be in the scene — the village scene ships with one pre-placed; the dungeon scene does not (it expects the player to be transferred in by `DungeonEntrance`).
- **PlayMode tests:** the project depends on `com.unity.test-framework` 1.6.0 but **no tests are written yet** — `Window ▸ General ▸ Test Runner` will open with an empty list.
- **Regenerate C# projects / fix IDE glue:** from the Editor, `Edit ▸ Preferences ▸ External Tools ▸ Regenerate project files` (or just delete the two `.csproj`s / `.sln` and reopen them — Unity will recreate them).

## Asset packs in the project (and their roles)

Multiple packs live side by side. The first one is the character/animation core; the rest supply world art.

| Folder | Source | Use it for |
|---|---|---|
| `Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack FREE/` | ExplosiveLLC / Unity Asset Store | **The character rig.** Prefabs: `Prefabs/Character/RPG-Character.prefab` and `RPG-Character-NPC.prefab`. Animator: `Animation Controller/RPG-Character-Animation-Controller.controller`. Animations: `Animations/Unarmed/` and `Animations/2Hand-Sword/`. Scripts: `Code/RPGCharacterController.cs`, `RPGCharacterInputController.cs`, `RPGCharacterMovementController.cs`, `RPGCharacterWeaponController.cs`, `IKHands.cs`, `RPGCharacterAnimatorEvents.cs`, plus `Actions/`, `Lookups/`, `Extensions/`, `AnimationData.cs`, `CoroutineQueue.cs`. The `_RPG/Scripts/...` layer is built **on top of** this pack — do not modify the pack's `Code/` files; extend the bridge instead (see "Code architecture" below). |
| `Assets/RPGPP_LT/` | RPGPP_LT | Buildings, nature, props (dungeon/village dressing). |
| `Assets/URP GanzSe Free Modular Character Pack/` | GanzSe | The **NPC skin** material used by `BlacksmithSetup` and `MerchantSetup` (a darker/different URP material on top of the pack's `RPG-Character.mat`). |
| `Assets/Gridness Studios/Elementary Dungeon Pack Lite/` | Gridness | Dungeon interior prefabs. |
| `Assets/Nicrom/3D_PolyArt/` | Nicrom | Poly-art props/nature. |
| `Assets/Proxy Games/Stylized Nature Kit Lite/` | Proxy Games | Trees/nature. |
| `Assets/Bitszer/` | Bitszer | Auction-house assets + `link.xml` (don't delete — it's a managed-code stripping link file). |
| `Assets/TextMesh Pro/`, `Assets/Settings/` | Unity | TMP essentials + URP render pipeline asset. |

The pack ships with its own materials that aren't URP. There are **two** auto-fixers, only one of which is needed:

- `Assets/_RPG/Scripts/Editor/AutoFixURPMaterials.cs` — runs on script reload, converts `Assets/ExplosiveLLC/.../Materials/*.mat` to URP Lit if they aren't already.
- `Assets/_RPG/Scripts/Editor/FixNPCMaterials.cs` — manual `RPG ▸ Fix NPC Materials (Convert to URP)`. Same effect; use it when you don't want to wait for a script reload.

## Code architecture (big picture)

The project is a single player + Unity scene + scripts. Two parallel systems handle the player:

- **The pack's own controllers** (`Assets/ExplosiveLLC/.../Code/RPGCharacter*.cs`) — generic, parameter-driven, attach-anywhere.
- **The project's own layer** (`Assets/_RPG/Scripts/...`) — built for *this* game: stats, stamina, dodge, draw/sheath, damage, UI, save. The single point of contact is `PlayerAnimatorBridge`, which translates the project's state into the pack's animator parameters (`Moving`, `Velocity X/Z`, `Jumping`, `Trigger`, `TriggerNumber`, `Weapon`, `RightWeapon`, `Action`, `AnimationSpeed`).

### Runtime systems (in addition order at boot, in `SpawnVillage.unity`)

1. **`GameManager`** (singleton, `DontDestroyOnLoad`) — `GameState` machine: Exploration / Combat / Dialogue / InMenu / Loading. Everything that ticks gameplay guards with `GameManager.Instance.IsGameplayActive()`. Keeps `PlayerObject` + `PlayerTransform` references and rebinds on scene load.
2. **`PlayerController`** — `CharacterController` + input + jump/gravity. Feeds `PlayerAnimatorBridge`.
3. **`PlayerStats`** — health, stamina (regen with delay), `baseAttack`/`baseDefense`, `OnHealthChanged`/`OnStaminaChanged`/`OnDeath` events, knockdown flag.
4. **`PlayerAnimatorBridge`** — the only thing that talks to the pack's `Animator`. Sets the parameters, fires triggers. Important constants live at the top of the file (see "Animator trigger numbers" below).
5. **`CombatSystem`** — combo chains (unarmed 1–6, sword 1–11), `comboWindow` / `attackCooldown`, stamina cost, hitstop, sphere hit detection in front of the player. The hit list is reduced by `PlayerStats` (defense) and handed to `EnemyStats.TakeDamage`.
6. **`PlayerDodge`** — Alt (or C) to roll, 0.4s i-frames, costs stamina.
7. **`WeaponDrawSystem`** — F to draw/sheath. Sets `Weapon` animator parameter (0=unarmed, 1=2H sword). Auto-draws on equip.
8. **`WeaponSocket`** — attaches the equipped `WeaponData` prefab to the right hand bone (`HumanBodyBones.RightHand`) via `Animator.GetBoneTransform`, with position/rotation offsets.
9. **`ThirdPersonCamera`** — orbit, zoom, sphere collision, `InteriorMode` (closer distance for indoor scenes).
10. **`PlayerInteraction`** — sphere-overlap on layer 10 (the "Interactable" layer) for `IInteractable` objects, prompts HUD, **E** to confirm.
11. **`HUDController`** — health/stamina bars, interaction prompt, damage numbers (`DamageNumberPool`).
12. **`InventoryManager`** (singleton) — 24 slots, gold, `OnInventoryChanged`/`OnGoldChanged` events. `ItemData` is a `ScriptableObject` with `ItemType` (Weapon / Helmet / Chest / Legs / Boots / Shield / Consumable).
13. **`EquipmentManager`** — equips items from the inventory to armour slots; stats flow back into `PlayerStats`.
14. **`SaveData` / `PlayerSaveData`** — serializable snapshot (health, stamina, position, scene, inventory, equipped item IDs). No file I/O wired in `SaveData.cs` itself — `GameManager`/`SceneLoader` are the spots to call it.
15. **`SceneLoader`** (singleton) — `LoadScene(int)` with a screen-overlay fade.
16. **`AudioManager`** — `PlaySFX(AudioClip)` singleton. Other systems call it; nothing plays audio directly.
17. **`EnemyAI` + `EnemyStats` + `EnemySpawner`** — `NavMeshAgent` state machine (Idle/Patrol/Chase/Attack/Hurt/Dead). Detection by `playerMask` overlap.
18. **NPC layer** — `NPCAnimationEvents` (animator event hookup), `NPCWander` (random walk with `Configure(speed, radius, step, minWait, maxWait)`), `NPCDialogue`, `NPCMerchant` (gives starter kit), `NPCHerrero` (blacksmith, `ShopEntry` list, opens `BlacksmithShopPanel`).
19. **World layer** — `VillageLayout` (procedural house placement), `HouseDoor` (hinge-animated, opens on E), `HouseInterior` (interior visibility toggle), `IInteractable` interface.
20. **Dungeon layer** — `DungeonEntrance` (particle crack, transitions to `dungeonSceneIndex=1` via `SceneLoader`), `DungeonExit` (back to village `0`).

All scripts that gate by game state use `GameManager.Instance.IsGameplayActive()`. Animations on the pack are driven by the bridge — direct `Animator.SetTrigger` calls from other systems are an anti-pattern (the bridge owns the `Trigger` / `TriggerNumber` parameter pair).

### Animator trigger numbers (from `RPGCharacterAnims.Lookups.AnimatorTrigger`)

The pack's animator expects a single `Trigger` paired with `TriggerNumber`. Constants used in this project (encoded in `PlayerAnimatorBridge.cs`):

- `4` Attack · `12` GetHit · `20` Death · `21` Revive
- `15` WeaponSheath · `16` WeaponUnsheath · `18` Jump
- `26` Knockback · `27` Knockdown · `28` DiveRoll

### Editor / setup scripts (top menu `RPG/...`)

These are the workflows the project relies on — most of them assume the scene already has the relevant tag/player/NPC and operate on whatever they find.

- `RPG/Setup Project` — `Assets/_RPG/Editor/RPGProjectSetup.cs` — creates tags (`Player`, `Enemy`, `SpawnPoint`, `Interactable`, `DungeonEntrance`), configures layers, makes scenes, fixes build settings. **Run this once on a fresh clone.**
- `RPG/Setup Player Components` — `Scripts/Editor/PlayerSetup.cs` — adds `PlayerDodge` + `WeaponDrawSystem` to the tagged Player.
- `RPG/Spawn NPCHerrero in Scene` / `RPG/Fix NPCHerrero Visuals` / `RPG/Move Herrero to Spawn Area` / `RPG/Apply Herrero Dark Tint` / `RPG/Setup Blacksmith Shop UI` — `Scripts/Editor/BlacksmithSetup.cs` — five "make the blacksmith work" entry points. `Setup Blacksmith Shop UI` is the one that builds the full shop panel.
- `RPG/Convert Selected to NPCHerrero` — `Scripts/Editor/ConvertToHerrero.cs` — converts a selected GameObject.
- `RPG/Setup Merchant (RPG skin + Wander + Dialogue UI)` / `RPG/Fix Merchant Wander Values` — `Scripts/Editor/MerchantSetup.cs`.
- `RPG/Fix Herrero - Todo (textura + interaccion + posicion)` / `RPG/Fix Herrero Final` / `RPG/Setup Herrero Completo` — three legacy/alt "fix the blacksmith" entry points; **prefer the `BlacksmithSetup` ones above**, these overlap and were kept for compatibility.
- `RPG/Fix NPC Materials (Convert to URP)` — `Scripts/Editor/FixNPCMaterials.cs` — manual version of the auto-fixer.
- `RPG/Setup House Colliders` / `RPG/Remove Added BoxColliders` — `Editor/HouseColliderSetup.cs` — walks every Default-layer `MeshRenderer` without a collider and adds a fitted `BoxCollider`. Reversible.
- `RPG/Terrain/Make Mountains Around Spawn` — `Editor/TerrainMountainizer.cs` — sculpts mountains around the spawn point on the `SpawnVillage` terrain. (Prompts to save first.)

### Input map

`Assets/InputSystem_Actions.inputactions` defines the actions used everywhere (`Player` map): `Move`, `Look`, `Attack`, `Interact` (Hold), `Crouch`, `Jump`, and the standard look bindings. Keyboard hotkeys that are hard-coded (not driven by the asset):

- **E** — Interact (`PlayerInteraction`)
- **F** — Draw/Sheath weapon (`WeaponDrawSystem`)
- **Left Alt** or **C** — Dodge (`PlayerDodge`)

Mouse/look goes through the existing `Look` action → `ThirdPersonCamera`.

## Conventions in this project

- UI is **uGUI + TextMesh Pro** (the project depends on `com.unity.ugui` and ships `Assets/TextMesh Pro/`). For new panels, mirror `BlacksmithShopPanel` / `HUDController` style.
- Items are `ScriptableObject`s created via `RPG/Item Data` menu (`ItemData.cs`); weapons also have a separate `RPG/Weapon Data` `ScriptableObject` (`WeaponData.cs`) referenced from `ItemData.weaponData`. Starter set: `Assets/_RPG/ScriptableObjects/Items/` + `Weapons/`.
- Tag conventions: `Player` on the player root, `Enemy` on AI agents, `SpawnPoint` on spawn markers, `Interactable` on the layer (layer 10) used by `PlayerInteraction`, `DungeonEntrance` on the entrance trigger.
- Audio: do **not** call `AudioSource.Play*` directly from gameplay scripts — go through `AudioManager.Instance.PlaySFX(clip)`. The audio manager is the only place volume/mixing lives.
- The pack's `RPGCharacter*.cs` is treated as a third-party library — extend the bridge, don't patch the pack.
