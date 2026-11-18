using System.Collections.Generic;
using UnityEngine;

namespace SpaceCowboy
{
    /// <summary>
    /// Capture the flag, on any map whose ArenaMap has both flag spots. The game turns it on by itself.
    ///
    /// Each team's base is a big glowing ring floating in the air at its flag spot, with its flag in the middle
    /// and a beam of light above it so you can find it from far away. The ring's rim is solid: hit it and you bounce off.
    ///   Take:    fly cleanly through the enemy's ring. Their banner is towed behind your ship on a rope.
    ///   Score:   fly through your own ring while carrying it. Your own flag has to be home to score.
    ///   Dropped: if you're destroyed, or crash hard (faster than half your top speed), the flag falls where you were.
    ///            Its own team touching it sends it home;
    ///            the other team can pick it up; untouched, it goes home by itself after a while.
    /// For now you're the only ship, on the amber team, going after the teal flag.
    /// </summary>
    public class CaptureTheFlag : MonoBehaviour
    {
        const int Amber = 0, Teal = 1;
        static readonly string[] TeamNames = { "AMBER", "TEAL" };
        static readonly Color[] TeamColors = { Palette.Amber, Palette.Hex("#2fc4bd") };

        const float RingRadius = 40f, RingThickness = 4f, TouchDistance = 16f, BeamHeight = 400f;
        const float ReturnSeconds = 15f, FarthestFrameMove = 300f;
        const float PickUpDelay = 2f; // a dropped flag can't be grabbed straight back (it falls right where you are)

        enum Where { Home, Carried, Dropped }

        class Flag
        {
            public int team;
            public Where where;
            public Vector3 ringCentre, ringFacing;
            public GameObject atHome, dropped, carried;
            public Vector3 dropSpot;
            public float droppedFor;
        }

        readonly Flag[] flags = new Flag[2];
        readonly int[] score = new int[2];
        readonly List<Object> made = new();
        const int PlayerTeam = Amber;

        HoverCraft ship;
        Damageable health;
        Hud hud;
        Flag holding;
        Vector3 lastSpot;
        bool tracking;
        System.Action<GameObject> onDied;
        System.Action<Collision> onCrash;

        /// <summary>A team captured a flag (its team number: 0 amber, 1 teal).</summary>
        public event System.Action<int> Captured;
        public int ScoreOf(int team) => score[team];
        /// <summary>The amber team's name and color, then teal's.</summary>
        public static string TeamName(int team) => TeamNames[team];
        public static Color TeamColor(int team) => TeamColors[team];

        public static CaptureTheFlag Create(ArenaMap map, HoverCraft ship, Hud hud)
        {
            var game = new GameObject("Capture the Flag").AddComponent<CaptureTheFlag>();
            game.ship = ship;
            game.hud = hud;
            game.health = ship.GetComponent<Damageable>();
            game.onDied = _ => game.DropFlag();
            if (game.health) game.health.Died += game.onDied;
            game.onCrash = collision =>
            {
                if (collision.relativeVelocity.magnitude > ship.topSpeed * 0.5f) game.DropFlag();
            };
            ship.Crashed += game.onCrash;
            game.flags[Amber] = game.BuildBase(Amber, map.flagA);
            game.flags[Teal] = game.BuildBase(Teal, map.flagB);
            game.ShowScore();
            game.ShowObjective();
            return game;
        }

        // --- Playing ------------------------------------------------------------------------------

        void Update()
        {
            foreach (var flag in flags)
            {
                if (flag.where != Where.Dropped) continue;
                flag.droppedFor += Time.deltaTime;
                flag.dropped.transform.Rotate(Vector3.up, 40f * Time.deltaTime, Space.World);
                if (flag.droppedFor >= ReturnSeconds) SendHome(flag);
            }

            Vector3 now = ship.transform.position;
            // A jump this big is a reset or a respawn, not flying: don't count anything along it.
            if (!tracking || (now - lastSpot).sqrMagnitude > FarthestFrameMove * FarthestFrameMove)
            {
                lastSpot = now;
                tracking = true;
                return;
            }
            if (health && health.IsDead)
            {
                lastSpot = now;
                return;
            }

            foreach (var flag in flags)
            {
                // Only a clean pass through the middle of the ring counts.
                bool through = ThroughRing(lastSpot, now, flag);
                if (flag.team != PlayerTeam && flag.where == Where.Home && holding == null && through)
                    Take(flag);
                else if (flag.team == PlayerTeam && through && holding != null)
                    TryToScore();

                if (flag.where == Where.Dropped && flag.droppedFor >= PickUpDelay && Vector3.Distance(now, flag.dropSpot) < TouchDistance)
                {
                    if (flag.team == PlayerTeam) SendHome(flag);
                    else if (holding == null) Take(flag);
                }
            }
            lastSpot = now;
        }

        /// <summary>Did the ship pass through this flag's ring between two spots? Either direction counts.</summary>
        static bool ThroughRing(Vector3 from, Vector3 to, Flag flag)
        {
            float before = Vector3.Dot(from - flag.ringCentre, flag.ringFacing);
            float after = Vector3.Dot(to - flag.ringCentre, flag.ringFacing);
            if (before == after || Mathf.Sign(before) == Mathf.Sign(after)) return false;
            Vector3 crossing = Vector3.Lerp(from, to, before / (before - after));
            return Vector3.Distance(crossing, flag.ringCentre) < RingRadius;
        }

        void Take(Flag flag)
        {
            flag.where = Where.Carried;
            flag.atHome.SetActive(false);
            if (flag.dropped) Destroy(flag.dropped);
            flag.carried = AttachBanner(flag.team);
            holding = flag;
            ShowObjective();
        }

        void TryToScore()
        {
            if (flags[PlayerTeam].where != Where.Home) return; // your flag has to be home (the diamond shows where it is)
            score[PlayerTeam]++;
            SendHome(holding);
            ShowScore();
            Captured?.Invoke(PlayerTeam);
        }

        /// <summary>The carrier was destroyed or crashed: the flag falls where they were.</summary>
        void DropFlag()
        {
            if (holding == null) return;
            var flag = holding;
            holding = null;
            if (flag.carried) Destroy(flag.carried);
            flag.where = Where.Dropped;
            flag.droppedFor = 0f;
            flag.dropSpot = ship.transform.position;
            flag.dropped = BuildFlag(flag.team, null);
            flag.dropped.transform.position = flag.dropSpot;
            ShowObjective();
        }

        void SendHome(Flag flag)
        {
            if (flag.carried) Destroy(flag.carried);
            if (flag.dropped) Destroy(flag.dropped);
            if (holding == flag) holding = null;
            flag.where = Where.Home;
            flag.atHome.SetActive(true);
            ShowObjective();
        }

        /// <summary>The ship was reset to its start spot (R): a carried flag goes home rather than teleporting with it.</summary>
        public void PlayerReset()
        {
            if (holding != null) SendHome(holding);
            tracking = false;
        }

        void ShowScore() => hud.ShowScore(score[Amber], score[Teal]); // you fly for the amber team

        /// <summary>
        /// The diamond on where to go next, no words: the enemy's ring (or their flag, if it's lying out there),
        /// or, with their flag in tow, your own ring (or your own flag, if it isn't home: it has to be, to score).
        /// </summary>
        void ShowObjective()
        {
            int enemy = 1 - PlayerTeam;
            Flag target = holding != null ? flags[PlayerTeam] : flags[enemy];
            Vector3 spot = target.where == Where.Dropped ? target.dropSpot : target.ringCentre;
            hud.SetObjective(spot);
        }

        // --- Building ------------------------------------------------------------------------------

        /// <summary>A team's base: a ring at the flag spot (facing the way the spot faces), its flag in the middle, and a beam above.</summary>
        Flag BuildBase(int team, Transform spot)
        {
            var flag = new Flag { team = team, where = Where.Home, ringCentre = spot.position, ringFacing = spot.forward };
            var root = new GameObject($"{TeamNames[team]} Base").transform;
            root.SetParent(transform, false);
            root.SetPositionAndRotation(spot.position, spot.rotation);

            var glow = Keep(Palette.Unlit(TeamColors[team]));
            var ring = new GameObject("Ring");
            ring.transform.SetParent(root, false);
            ring.AddComponent<MeshFilter>().sharedMesh = Keep(Ring(RingRadius, RingThickness));
            var ringLook = ring.AddComponent<MeshRenderer>();
            ringLook.sharedMaterial = glow;
            ringLook.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // The rim is solid, so you have to fly through the middle, not clip the edge.
            ring.AddComponent<MeshCollider>().sharedMesh = ring.GetComponent<MeshFilter>().sharedMesh;

            flag.atHome = BuildFlag(team, root);
            return flag;
        }

        /// <summary>A flag on a short pole with a beam of light above it, in the team's color.</summary>
        GameObject BuildFlag(int team, Transform parent)
        {
            var root = new GameObject($"{TeamNames[team]} Flag").transform;
            root.SetParent(parent ? parent : transform, false);
            var pole = Keep(Palette.Toon(Palette.Hex("#d8d2c4")));
            Palette.Shape(PrimitiveType.Cylinder, "Pole", root, new Vector3(0f, 0f, 0f), new Vector3(0.6f, 9f, 0.6f), pole);
            BannerFlag.Create(root, new Vector3(0.3f, 2.5f, 0f), Quaternion.identity, TeamColors[team], 11f, 6f);

            var beamColor = TeamColors[team];
            beamColor.a = 0.18f;
            var beam = Palette.Shape(PrimitiveType.Cylinder, "Beam", root, new Vector3(0f, 9f + BeamHeight / 2f, 0f), new Vector3(3f, BeamHeight / 2f, 3f), Keep(Palette.Unlit(beamColor)));
            beam.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            foreach (var part in root.GetComponentsInChildren<Collider>()) Destroy(part);
            return root.gameObject;
        }

        /// <summary>The captured team's banner, towed behind the ship on a rope from its tail.</summary>
        GameObject AttachBanner(int team)
        {
            var box = ship.GetComponent<BoxCollider>();
            Vector3 centre = box ? box.center : Vector3.zero, size = box ? box.size : new Vector3(8f, 3f, 10f);
            Vector3 tail = centre + new Vector3(0f, 0f, -size.z * 0.5f);
            return TowedBanner.Create(transform, ship.transform, tail, TeamColors[team], Mathf.Max(6f, size.z)).gameObject;
        }

        /// <summary>A ring standing up, facing +Z.</summary>
        public static Mesh Ring(float radius, float thickness)
        {
            const int around = 64, across = 12;
            var points = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i <= around; i++)
            {
                float a = 2f * Mathf.PI * i / around;
                Vector3 outward = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                for (int j = 0; j <= across; j++)
                {
                    float b = 2f * Mathf.PI * j / across;
                    points.Add(outward * (radius + Mathf.Cos(b) * thickness) + Vector3.forward * (Mathf.Sin(b) * thickness));
                }
            }
            int row = across + 1;
            for (int i = 0; i < around; i++)
                for (int j = 0; j < across; j++)
                {
                    int p = i * row + j, q = p + 1, r = p + row, s = r + 1;
                    triangles.Add(p); triangles.Add(q); triangles.Add(r);
                    triangles.Add(q); triangles.Add(s); triangles.Add(r);
                }
            var mesh = new Mesh { name = "Goal Ring" };
            mesh.SetVertices(points);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            return mesh;
        }

        T Keep<T>(T thing) where T : Object
        {
            made.Add(thing);
            return thing;
        }

        void OnDestroy()
        {
            if (health) health.Died -= onDied;
            if (ship) ship.Crashed -= onCrash;
            foreach (var flag in flags)
                if (flag != null && flag.carried) Destroy(flag.carried);
            foreach (var thing in made) if (thing) Destroy(thing);
            if (hud) hud.SetObjective(null);
        }
    }
}
