# Space Cowboy

**Note:** Loading `ProceduralGeneration.unity` in `SpaceCowboy.unitypackage` may prompt the user to import additional packages to enable UI component functionality.

## Overview

SpaceCowboy is a procedurally generated, space‑western themed pod racing game in which players fly along a continuously spawning track, dodging randomized obstacles while attempting to survive for as long as possible. The player controls a fighter pod hovering above the terrain, using WASD keys to strafe and thrust while avoiding obstacles. The track continuously accelerates, increasing difficulty in real time. The final score combines total survival time (displayed via a stopwatch) with the peak velocity achieved.

The primary objective of the project was to create a responsive, skill‑based game that demonstrates advanced computational media concepts through procedural generation, real‑time animation, physics‑based movement, and dynamic difficulty scaling. The project incorporates concepts from class including on‑screen dynamics, animation, text, random numbers, design, coding, and visual cues.

## On‑Screen Dynamics

During gameplay, the cursor is hidden to prevent interference with ship control. When the game enters a game‑over state, the cursor becomes visible to allow menu interaction. Menu buttons change opacity when highlighted to indicate interactivity.

Player input directly affects ship motion, and collisions with obstacles immediately halt movement and stop the game timer. This creates an illusion of player‑controlled movement and physical interaction between the ship and the environment.

Terrain sections spawn through collision triggers and are appended to the scrolling environment, while obstacles placed within each section require continuous navigation adjustments. UI state changes communicate transitions between active gameplay and game‑over conditions.

In `StrafeHoverMovement.cs`, movement is composed of strafe acceleration, thrust, sway, hover force (`upwardForce = proportionalHeight * hoverForce`), and drag. Sway and drag are introduced using mathematical operations and Unity’s built‑in physics and sine functions to enhance realism and reinforce the illusion of physical motion.

`GameManager.cs` handles collision detection, triggers the game‑over state, disables gameplay UI, and displays the final score. `SectionCreator.cs` spawns new terrain sections when triggered and increases the global movement speed by 5 m/s per section.

## Animation and Visual Cues

Animation is used to visually represent player input and system state through manipulation of ship components. The fighter ship includes wing rotation based on strafe input, engine rotation during acceleration, and body tilt that banks up to ±25 degrees with spring‑based correction toward an upright orientation. Terrain sections move downward at a continuously updated speed to represent forward progression. All animations and movement calculations are scaled using `Time.deltaTime` to maintain frame‑rate independence.

Fighter tilt in `StrafeHoverMovement.cs` indicates strafe direction and magnitude, with automatic correction indicating neutral input. Wing and engine rotation correspond to thrust and directional input. Increasing terrain speed provides a visible indicator of difficulty scaling, while variation in obstacle size affects navigational requirements.

Text is used to display a running stopwatch during gameplay, which stops when the ship collides with an obstacle. In `FighterAnimations.cs`, wing rotation responds to A/D input at 120 degrees per second with a maximum rotation of 15 degrees, engine rotation reflects thrust input, and body tilt represents directional input. Tilt correction is implemented in `StrafeHoverMovement.cs` using spring physics (`springForce = tiltDifference * uprightSpring`). Terrain section movement is handled in `MoveSection.cs` using the current global speed multiplied by `Time.deltaTime`.

An earlier design planned to implement a pseudo Dynamic Bone system to give ship components inertia. The initial script was based on `tracker.cs` from the Flock project, but the behavior of individual ship objects could not be refined to a complete state. The system was intended to allow ship parts to collide and deform independently, but collisions were unreliable and motion remained stiff despite parameter adjustments.

## Random Numbers

Random number generation is used to vary track layout while maintaining navigable space. Obstacles are placed at randomized positions within each terrain section, with collision radii used to prevent complete path blockage. `RandomizeObjectsInSection.cs` performs constraint‑based procedural generation by randomizing object position within terrain bounds, scale within predefined minimum and maximum values, and rotation between 0 and 360 degrees.

Overlap checks using `Physics.OverlapSphere()` prevent obstacle intersections, with up to ten placement attempts per object before rejection. This approach maintains consistent and fair gameplay conditions.

Terrain section spawning is managed by `SectionCreator.cs`, with each new section increasing the global movement speed by 5 m/s. Within each section, obstacle size, rotation, and position are independently randomized.

## Coding and Design

Responsibilities are delegated across individual scripts to create a modular and readable codebase. `SectionCreator` manages terrain spawning, `MoveSection` controls synchronized terrain movement through a static `metersPerSecond` variable, `SectionDestroyer` removes terrain sections that move out of range, and `RandomizeObjectsInSection` handles obstacle placement. Together, these systems support continuous procedural generation while maintaining bounded memory usage.

Variables representing a program’s internal state are intentionally kept non‑public, while reusable scripts such as `RandomizeObjectsInSection.cs` expose appropriate public variables to allow flexibility and reuse.

Gameplay follows a repeated loop of movement, obstacle avoidance, and survival, with difficulty increasing through incremental speed changes. Visual consistency is maintained through repeated terrain sections and ship geometry, while variation is introduced through procedural obstacle placement and scaling.

Physics is implemented using proportional forces and built‑in rigidbody damping, including hover force applied based on distance from the terrain and spring‑based tilt correction. Collision detection uses raycasts for hover distance calculation and overlap sphere checks for obstacle placement. Infinite gameplay is achieved through an instantiate‑queue‑destroy cycle. Mathematical operations include angle normalization, sine‑based oscillation for sway, and linear interpolation for randomized values.

## Technical Achievements and Integration

The project integrates animation, physics, procedural generation, and UI state management into a single continuous system. Animation and physics operate together to reflect player input under increasing movement speed. Randomized obstacle placement functions within fixed constraints defined by terrain bounds and collision checks. Visual cues and UI logic communicate gameplay state transitions without interrupting player control flow.
