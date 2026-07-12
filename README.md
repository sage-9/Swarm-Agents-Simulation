# Swarm Agents Simulation

A Unity-based simulation of a heterogeneous drone swarm performing autonomous search-and-rescue (SAR) in an indoor environment. Scout, Rescuer, and Relay drones coordinate through a shared voxel grid, frontier-based exploration, A* pathfinding, and range-limited communication to find and "rescue" victims scattered through a mapped space.

## Overview

Each drone is built from a common `BaseAgent` plus a set of composable systems (movement, obstacle avoidance, sensing, communication), and specializes into one of three roles:

- **Scout Drone** — flies and explores unknown space using frontier-based exploration (FBE): it scans its personal voxel grid for the boundary between explored and unexplored regions and picks the next frontier to fly toward, with stuck-detection and recovery behavior if progress stalls.
- **Rescuer Drone** — a ground unit that path-finds to reported victim locations with A*, runs a small state machine (`Idle → MovingToVictim → Rescuing → ReturningToBase`), and returns to base after completing a rescue.
- **Relay Drone** — repositions itself to maintain communication bridges between scouts and the base station, using the other agents' positions to estimate an optimal relay point.
- **Monolithic Drone** — an experimental single high-cost agent that uses an information-gain heuristic over the grid to simulate a more "intelligent" exploration strategy, for comparison against the swarm approach.

Agents don't share a global map directly — each one maintains its own personal voxel `Grid`, and grids are merged opportunistically when agents come within communication range and have unobstructed line of sight, simulating realistic, decentralized information sharing.

## Core Systems

All scripts live under [`Assets/Scripts/`](Assets/Scripts/).

| System | Script | Responsibility |
|---|---|---|
| `BaseAgent` | `Assets/Scripts/BaseAgent.cs` | Shared lifecycle, state (`Idle`, `Searching`, `Guarding`, `Returning`), and movement/rotation toward a target |
| `Grid` / `WorldGridManager` | `Assets/Scripts/Grid.cs`, `Assets/Scripts/WorldGridManager.cs` | Voxel representation of the world; each agent holds its own copy seeded from a shared default grid |
| `SensingSystem` | `Assets/Scripts/SensingSystem.cs` | Performs a spherical (golden-ratio/Fibonacci) raycast scan each interval to update the grid and detect victims |
| `AvoidanceSystem` / `AgentBehaviors` | `Assets/Scripts/AvoidanceSystem.cs`, `Assets/Scripts/AgentBehaviors.cs` | Obstacle avoidance (grid-based), inter-agent separation, and immediate physics-based avoidance, combined into a steering force |
| `CommunicationSystem` | `Assets/Scripts/CommunicationSystem.cs` | Periodically shares/merges grid data with other agents within range and line of sight |
| `AStarPathfinder` / `PathFollower` | `Assets/Scripts/AStarPathfinder.cs`, `Assets/Scripts/PathFollower.cs` | Grid-based A* pathfinding and waypoint following for ground units |
| `AgentSpawnManager` / `DroneSpawnConfig` / `DroneConfig` | `Assets/Scripts/AgentSpawnManager.cs`, `Assets/Scripts/DroneSpawnConfig.cs`, `Assets/Scripts/DroneConfig.cs` | Data-driven, extensible spawning of drone types via `IAssignable`, decoupled from specific drone classes |
| `SemanticRoom` / `SemanticDoorway` | `Assets/Scripts/SemanticRoom.cs`, `Assets/Scripts/SemanticDoorway.cs` | Trigger volumes marking rooms/doorways as explored, for higher-level map semantics |
| `SimulationTelemetry` | `Assets/Scripts/SimulationTelemetry.cs` | Records exploration %, victim discovery/rescue events, and periodic snapshots, then exports them to CSV for analysis |
| `DroneDisabler` | `Assets/Scripts/DroneDisabler.cs` | Randomly disables a percentage of drones at runtime to test swarm resilience/degraded-resource scenarios |

Drone roles (`ScoutDrone.cs`, `RescuerDrone.cs`, `RelayDrone.cs`, `MonolithicDrone.cs`) and shared interfaces (`IAssignable.cs`, `IDroneRole.cs`, `StateMachine.cs`) are also in `Assets/Scripts/`.

## Scenes

- **Single Agent** — a minimal scene for testing an individual drone's behavior in isolation.
- **Multi Agent** — the full swarm scenario with multiple Scouts, Rescuers, and Relays operating together in a mapped office-style environment.

## Getting Started

### Requirements

- Unity **6000.4.0f1** (Unity 6) or later, using the Universal Render Pipeline (URP).
- Unity's Input System package (project uses the new Input System, not the legacy one).

### Running the simulation

1. Clone the repository:
   ```bash
   git clone https://github.com/sage-9/Swarm-Agents-Simulation.git
   ```
2. Open the project folder in Unity Hub (Unity 6000.4.0f1).
3. Open `Assets/Scenes/Multi Agent.unity` (or `Single Agent.unity` for a single-drone test).
4. Press Play. Drones will spawn per the configuration on the `AgentSpawnManager` in the scene and begin exploring/rescuing automatically.

### Telemetry output

While running, `SimulationTelemetry` writes CSV files (periodic snapshots, per-victim discovery/rescue times, and an end-of-run summary) to a configurable output directory, useful for comparing swarm configurations (e.g. drone counts, disabled-drone percentage) across runs.

## Project Structure

```
Assets/
├── Scenes/          # Single Agent and Multi Agent test scenes
├── Scripts/          # All simulation logic (agents, systems, pathfinding, telemetry)
├── prefabs/           # Drone and victim prefabs
├── Material/          # Materials
├── Textures/          # Textures
└── Settings/          # URP / project rendering settings
```

## Notes

This project is a research/coursework simulation exploring frontier-based exploration, A* pathfinding, and potential-field-style avoidance for multi-agent drone SAR coordination. Some systems (e.g. `MonolithicDrone`'s "deep learning simulation") are explicitly simplified heuristics rather than trained models, intended as a baseline for comparison against the decentralized swarm approach.

## License

No license file is currently included in this repository. Add one (e.g. MIT) if you intend for others to reuse this code.
