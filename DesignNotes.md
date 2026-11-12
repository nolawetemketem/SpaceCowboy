# Space Cowboy: Design Notes

Everything you've said about the game so far, in one place: your vision and scope docs from the web project, what you decided while we built the Unity version, and your newest ideas.

Last updated 2026-11-09.

## The game

**New direction: a multiplayer, competitive arena shooter** with thematic and narrative elements. Agile spaceships fight each other and use abilities, in space, a sphere or a box. The goal is **capture the flag**; the ball and the 0-0 scoreboard have been removed from the game. Earlier notes below describe where the idea started, as a ball sport.

- **Shooting:** one system that can apply to many ships, each with its own weapon stats.
- **Aim:** a cursor showing where the bullets will go.

Where it started: a space-western sports game. You pilot a hovercraft and score on the other side's goal. It's a familiar ball-and-goal sport plus "my 10%": a late-'90s anime look, hovercrafts instead of cars, and a world Bebop fans will recognize, **inspired by, not copied from**, the anime.

- More playful than Rocket League, which takes a lot of practice to get into. Still skill-based, with fantasy mechanics: "not just cars pushing a ball."
- "Not rewriting the whole gaming experience": a familiar sport with a few distinguishing mechanics.
- Development has moved from the web prototype to Unity. The Unity game is now the main version.
- **Bigger goal:** this becomes your capstone, a complete game that brings your CS, graphics and creative skills together. You want to understand every part of the code.
- **Social goal:** "that social aspect, to bring people together."

## Who it's for

Anime fans, Cowboy Bebop fans especially, who want to play with friends, in person or online.

## Inspirations

- **Show:** Cowboy Bebop, especially Ed. Akira for the late-'90s anime look.
- **Gameplay:** Rocket League, Quidditch, Racket Rivals (Roblox), wings.io.
- **Shader:** Messenger (https://messenger.abeto.co).

## Why Ed

- Ed is a Black character, and Black representation in anime is rare. You connect with her.
- She's a young creative technologist who experiments, sometimes breaks things, keeps learning and has fun: "that's what technology should be about."
- She's the hope and lightness of a crew carrying trauma, someone whose impact you only fully feel once she's gone.
- In the show she pilots crafts with a controller, which ties directly to the core mechanic.
- Ed is the starting point, not necessarily the center of the whole project.

## Tone

**Playful and dark.** Ed's joy alongside the show's weight and melancholy. Bebop is a space western with serious moments; it feels real. Made with love for the anime and its fans.

## Look

**World shading:** keep Messenger's techniques and swap in a Bebop palette.
- Flat cel bands (2–3 light bands), not smooth gradients.
- Solid, hard-edged, cool-toned shadows.
- Ink outlines with slightly uneven thickness.
- Hand-drawn wobble and posterized edges (not built yet).
- Painterly textures (not built yet).
- Moodier and higher contrast than Messenger: warm neon against dusky space blues, film grain, a jazz-noir feel.

**Palette:** CRT black, amber, phosphor green, device teal, emoticon yellow, blush red, sunset red, and **deep dark purple** (one of your favorite colors). Purple is for dusk and night skies, shadows, and rim light on dark surfaces, so black reads as purple-black.

## Interface: Ed's tech

The world is space-western; the interface feels like Ed built it. Free, playful, chaotic, joyful.

- UI framed inside a chunky teal device, with rounded corners and speaker dots.
- CRT screens with glowing amber typewriter text, binary columns and green phosphor.
- Alert panels in bold condensed caps with big kanji.
- An LCD clock or readout.
- Floating yellow emoticons with black outlines and red blush, tilted and layered.
- Dense and a little chaotic, not neat and corporate.
- Fonts: Courier Prime, Oswald, VT323.

**Emoticon details:**
- Speech-bubble "chin": a sharp point with a flat, straight top edge running out sideways from the circle; the lower edge sweeps back into the circle.
- Eyes: a sideways comma: a solid black circle with a tapered curl arcing up and over, all one black shape.
- Grin: closed arched eyes with tapered ends, blush right under them, a wide D-shaped mouth with big white teeth.
- Smirk: the same comma eyes, two blush ovals, a small angular mouth.

**Full main menu (later):**
- Holographic and translucent, like the holograms in the show. Not a solid black screen.
- The menu floats over a real 3D world.
- Ed's paper pinwheel is mounted in that world, spinning in the wind behind the hologram, as a nod to her departure.
- Pinwheel look: handmade folded paper, four pink blades with lighter fronts and darker folded flaps, curled tips, a small pin, a thin pale stick. Spinning, it blurs into a soft purple-pink disc with a red streak cross.
- Menu on the left: mode select and start.

## Flying and handling

**New direction: an agile, omnidirectional spaceship.** You're imagining fights in space, inside a sphere or a box, with no fixed "down." It's less of a hovercraft now. Get this movement feeling right first, then build on it.

- The left stick (W/S on keyboard) pitches the nose: stick up is up, stick down is down. Mixed with left and right, it makes diagonals.
- Accelerate on the right trigger, decelerate on the left trigger, so you go forward and back with just those two, in any direction.
- That leaves the face buttons free for attacks, specials and gun abilities.
- **Quick flips on the right stick** (arrow keys on keyboard). Up and down rotate around the ship's own X axis; left and right rotate around its own Z axis. A flip is a set move that plays out in full, not a value you steer. A flick or tap is 90°. Holding it is 180°, and so is a double-tap.
- The right stick no longer moves the camera, because camera look doesn't add anything to the gameplay.

The notes below are from the hovercraft version. The Space Flight checkbox on the HoverCraft component switches between the two.

Your #1 priority is that maneuvering feels clean.

**What you wanted the hovercraft to feel like:**
- Floaty, like a hovercraft floating in space, not a car.
- Easy-to-understand controls, on both keyboard and controller.
- Full X, Y and Z range of motion.
- A slight punishment for hitting things, without disorienting the player.
- Turning must not slow the craft down.
- Default forward speed should feel fast.

**Rise and fall (replaced the jump):**
- Hold Space to rise. The nose tilts up, so you can see the craft is climbing.
- Hold C to fall. This must feel like the jets cut out and gravity takes the ship, not like the ship pushing itself down.
- The drop should be quick, so you can adjust fast and do unique movements, without feeling crazy.
- The nose dips only very, very slightly while falling.

**Ship rules:**
- The ship never phases through the ground. If it hits the ground, it collides.
- No wing animation on turns; a jet doesn't do that. Braking opens the engines.
- **No flame or thruster effects.** Each plane model will be different, so effects like that don't belong on the shared craft.

**Camera:**
- Follows on its own, so the player never has to manage it.
- Keeps your lerp: the camera lags behind and catches up, which gives movement a dynamic feel.

**Craft stats:** one clean control scheme with a high skill ceiling. Each craft differs by stats (speed, power, agility, acceleration), for example heavier with more power, or lighter with less.

**Default controls (the player can remap all of these):**

| Action | Keyboard | Controller |
|---|---|---|
| Nose up / down | W S | Left stick |
| Turn | A D | Left stick |
| Accelerate | Space | RT |
| Brake, reverse | C | LT |
| Fire | Enter | A |
| Flip 90° | Tap an arrow key | Flick the right stick |
| Flip 180° | Hold or double-tap an arrow key | Hold the right stick |
| Boost | Left Shift | B |
| Reset ship | R | View |

## Ships and characters

- Each hovercraft is inspired by a Cowboy Bebop character and modeled on the show's crafts.
- Each craft has a **special** with a cooldown and an **ult** with a longer cooldown. Both should reflect the character.
- **Ed's craft:** light, agile, fast and slippery, with less power, matching her personality and build. Playful, inventive, a bit chaotic, joyful.
- **Ed's special idea:** hack or stun other players' crafts.

**New ability ideas (2026-11-09):** small, short abilities that differ by ship and personality:
- Invisibility
- Force field
- Speed boost
- Shockwave that stuns nearby ships

## Game modes

**Ball and goals (the original Training, since removed; the goals remain for capture the flag):**
- Solo practice with one craft, a ball, two goals and two separate goal counts.
- The ball respawns in the middle after a goal; the craft stays put.
- No walls in Training. R and B resets are for Training only; matches won't have them.

**Capture the flag (new idea, 2026-11-09):**
- Collect the flag by hitting or passing through a specific chamber or goal.
- To end the game, carry it through your own goal.
- If your ship is hit while carrying the flag, the flag can drop near the ship's body.
- Ships are destroyed in one hit.

The last two points come from a garbled part of your message ("the flag can fall half ear ship's body, as well as ensuring that ships one shot and destroyed"), so check they match what you meant.

**Match formats and play:** 1v1, 3v3, local split screen, and online with friends. A CPU opponent is possible, low priority.

## Arenas

1. **Desert (built first):** references Session 17, "Mushroom Samba": an abandoned field with posts as goals, cactuses, a watermelon stand, and a train. Your Unity desert packs add more variety than the web game's cactuses.
2. **Water:** hovercrafts skim over animated water.
3. **City:** evokes one of the anime's cities.

## Later ideas

- Wall climbing: the hovercraft sticks to and drives up steep slopes and walls, Rocket League style.
- Sideways rotation and flips.
- Design a HUD slot for abilities; the old "SPECIAL // COMING SOON" placeholder has been removed.
- **Controls and settings (built):** two tabs, SENSITIVITY and CONTROLS, each fitting on one screen with no scrolling (switch with Tab, LB/RB, or a click). In game language rather than device language. Sensitivities run 0 to 10 and apply to every device: up/down, left/right, thrust, brake, and dead zone. On the CONTROLS tab, every mechanic can be remapped: select it, press the new key or controller input, confirm. If another mechanic already uses that input, the two swap. Esc and the controller's Menu button always open and close the page and can't be remapped. It's on the start menu and opens during Training. In Training it also has a MAIN MENU button that takes you back to the start menu. The game doesn't pause; the ship coasts while it's open. Everything is saved between sessions.
- **Destruction and respawn (built):** a destroyed ship or target bursts into pieces with a flash, the pieces fall and bounce on the ground, then each one turns to a hologram and disintegrates. The ship then returns as an LED-green wireframe of its own model: lines trace along the mesh's edges from corner to corner, starting under the ship, a faint glow fills in between them, and the real ship takes over as the grid fades. It can't be hit until it's solid. Works for any model with Read/Write turned on in its import settings. Everything both effects need is made while the game loads, so neither stalls play. For testing, K blows up your ship in Training (editor and development builds only).
- Cooldown display for boost.
- Goal celebration effects and sound.
- Rendering optimizations: culling far-away and off-screen objects, level of detail.

## Open questions

**Capture the flag:**
- What exactly makes you lose the flag?
- Do destroyed ships respawn?
- Where are flags positioned?

**Abilities:**
- Are the new short abilities the same thing as each craft's special and ult, or a separate system?
- Which ability goes with which character?

**Bigger picture:**
- Which Bebop crafts come next (Spike, Jet, Faye, Ein?), and their specials and ults.
- Netcode approach for online play.
- Whether a public or commercial release should use original characters "inspired by" Bebop.

## Keep in mind

- Don't retell the anime's story.
- For a public release, draw original emoticons and text in the show's spirit. Don't trace or copy show frames.
- Avoid generic AI-app styling.
