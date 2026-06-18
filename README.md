# Tower Defense — Unity Mobile Game

A tower defense game inspired by Arknights, built with Unity (URP) targeting Android. Features procedural maze generation, multi-gate pathfinding, and a two-phase deploy system (drag-to-place + direction select).

## Gameplay

- **Procedural maps** — Recursive Backtracker maze generation + A* pathfinding create unique layouts every stage
- **Multi-gate system** — enemies spawn from multiple gates with 4 assignment strategies (RoundRobin, Random, PerWave, Simultaneous)
- **Operators & Towers** — melee operators block enemy paths (Arknights-style), ranged towers fire projectiles
- **Difficulty scaling** — 5 difficulty levels with ratio-based enemy composition and HP/speed multipliers
- **Stage progression** — main menu, stage select, tutorial flow, victory/game-over panels with star rating

## Technical Highlights

| Area | Details |
|---|---|
| **Architecture** | MVC-like (Model/Control/View/Services), 14 design patterns documented |
| **Pathfinding** | A* (Manhattan heuristic), iterative Recursive Backtracker maze gen |
| **Performance** | Object pooling (enemy, VFX, SFX, range tiles), zero-alloc hot paths, Camera.main cached |
| **Data-Driven** | 7 ScriptableObject configs — balance tuning without recompilation |
| **SOLID** | Strategy (IOperatorBehavior, IGateAssignmentStrategy), DIP (IPathFinder injection), ISP (IDeployableDTO, IPlacedUnit) |
| **Async** | async/await wave system with PauseAwareDelay, CancellationToken, Task.WhenAll parallel spawning |
| **Audio** | 6-class SRP split (Facade + BGM/SFX players + contexts + prefs persistence) |
| **Build** | IL2CPP, ASTC 6x6, shader variant stripping — optimized from 255 MB to 102 MB |

## Project Structure

```
Assets/
  1.Scenes/          — DTLoadFirst (persistent) + DTGamePlay (per-stage)
  2.Scripts/
    Control/          — game logic, pathfinding, tower/operator behavior
    Model/            — DTOs, configs, enums, ScriptableObject settings
    View/             — MonoBehaviours, UI panels, visual effects
    Services/         — cross-cutting: event bus, audio, effects
    Init/             — bootstrap, resource registry
  3.Prefabs/          — enemy, tower, operator, UI prefabs
  4.ThirdParties/     — DOTween, Odin Inspector
```

## Key Systems

### Maze & Path Generation
Grid-based (cellSize = 2 units) with even-coordinate room cells. Each gate group generates 3 corridors via maze carve + A* extract, with cross-gate blocking to isolate paths. Section-based gate placement ensures even distribution across borders.

### Deploy System (Arknights-style)
State machine with 4 states: `Idle → Dragging → DirectionSelect → Committing`. Drag operator from slot bar onto grid, then select facing direction. Melee operators deploy on path cells and block enemies; ranged operators deploy on tower zones.

### Wave System
Async loop with difficulty-based enemy distribution (DifficultyRatioTable), boss wave scheduling, and per-type object pools. PauseAwareDelay respects Time.timeScale for pause/speed controls.

## Tech Stack

- **Engine**: Unity 2022+ (URP)
- **Platform**: Android (IL2CPP, ARM64)
- **Language**: C# (.NET Standard 2.1)
- **Libraries**: DOTween (animation), Odin Inspector (editor tooling)

