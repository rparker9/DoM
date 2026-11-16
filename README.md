# DoM: Dungeons of Mystery

A Mystery Dungeon-style turn-based roguelike prototype in Unity.

The game rules run as a data-driven simulation with no Unity code (`Assets/Scripts/Sim`).
A playback layer (`Assets/Scripts/Playback`) decides when each event animates, so actors
that don't affect each other animate at the same time. Unity scripts talk to the game
through one API class, `DungeonGame` (`Assets/Scripts/Api`).

## Run it
Open the project in Unity, open a scene with a GameObject that has **DungeonRunner**, press Play.

## Controls
WASD/arrows + Q/E/Z/C diagonals: step. 1–4 or Space: use a move. Shift+direction: turn. R: wait.
Tab or F1–F4: switch leader. F / H / T: party follows / holds / attacks the nearest enemy.
Hold Ctrl: fast-forward. M: toggle moves playing one at a time.

## Folders
| Folder | Unity? | Role |
|---|---|---|
| `Assets/Scripts/Sim` | No | Game rules: ECS, turn queue, moves, AI |
| `Assets/Scripts/Playback` | No | Event timing (locks) and the playback clock |
| `Assets/Scripts/Api` | No | `DungeonGame`, the class Unity scripts use |
| `Assets/Scripts/View` | Yes | `DungeonRunner`, gray boxes, ScriptableObject content |

## Making moves
A move is two assets:
- **MoveAsset** (Create → Dungeon → Move): what it does. Area (Line, Around, Self, Team), range, pierce,
  burst radius, filter (who it affects), delivery (Contact, Projectile, Beam), hits, and a list of effects
  (Damage, Heal, Knockback, Drain, Recoil, each with an amount and a % chance).
- **MoveVisual** (Create → Dungeon → Move Visual): what it looks like. A wind-up motion, then three phases
  (wind-up, travel, impact), each with an optional VFX Graph, a duration and a hit frame (release).
  Without one, the move plays with gray-box placeholders.

Every VFX graph gets these exposed properties if it declares them: `Origin`, `Target`, `Direction`
(Vector3), `Radius`, `Duration` (float). Timing always comes from the MoveVisual, never the particles.

The AI scores every move from its effects, so new moves need no AI code.
