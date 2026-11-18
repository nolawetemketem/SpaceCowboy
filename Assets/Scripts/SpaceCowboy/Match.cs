using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaceCowboy
{
    /// <summary>
    /// One match of Death Match or Capture the Flag: the pilots and their teams, the clock, the score and who wins.
    ///   Death Match (free for all, 1v1, 3v3): most kills when time runs out wins (in 3v3, the team's total). No flags.
    ///   Capture the Flag (1v1, 3v3): first team to 3 captures wins; if time runs out first, most captures wins,
    ///     and a tie goes to sudden death (the next capture wins).
    /// Destroyed pilots come back at their team's spawn points. Every pilot's kills, deaths and K/D are shown at the end.
    ///
    /// For now you're the only real pilot. The other seats are computer pilots (BotPilot): they chase the nearest enemy
    /// and shoot. They don't use specials or go for the flags yet. Real pilots take their seats later.
    /// </summary>
    public class Match : MonoBehaviour
    {
        public const int CapturesToWin = 3;

        public class Pilot
        {
            public string name;
            public int team;
            public bool you;
            public Damageable health;
            public Transform ship;
            public int kills, deaths, captures;
        }

        public MatchSetup Setup { get; private set; }
        public bool Over { get; private set; }
        /// <summary>The match has ended: the heading (like "YOU WIN!") and everyone's results.</summary>
        public event Action<string, List<ResultsPanel.Line>> Ended;

        readonly List<Pilot> pilots = new();
        Pilot you;
        ArenaMap map;
        Hud hud;
        CaptureTheFlag flags;
        float secondsLeft;
        int shownSeconds = -1; // the clock's text is only rebuilt when the whole second changes
        bool suddenDeath;
        int spawnTurn;

        public IEnumerable<Transform> EnemiesOfYou => pilots.Where(p => p.team != you.team && p.ship).Select(p => p.ship);

        public static Match Create(MatchSetup setup, ArenaMap map, HoverCraft player, Hud hud, CaptureTheFlag flags)
        {
            var match = new GameObject("Match").AddComponent<Match>();
            match.Setup = setup;
            match.map = map;
            match.hud = hud;
            match.flags = flags;
            match.secondsLeft = MatchSetup.Minutes * 60f;
            match.Seat(player);
            if (flags) flags.Captured += match.OnCapture;
            Damageable.AnyDied += match.OnDeath;
            match.ShowScore();
            return match;
        }

        // --- Seats ---------------------------------------------------------------------------------

        void Seat(HoverCraft player)
        {
            int seats = Setup.Seats;
            var health = player.GetComponent<Damageable>();
            you = new Pilot { name = Setup.fighter.name, team = 0, you = true, health = health, ship = player.transform };
            health.team = 0;
            pilots.Add(you);

            var model = player.transform.Find("BankPivot");
            var shape = player.GetComponent<BoxCollider>();
            var names = Fighters.All.Select(f => f.name).Where(n => n != Setup.fighter.name).ToList();
            for (int seat = 1; seat < seats; seat++)
            {
                int team = !Setup.Teams ? seat : Setup.size == TeamSize.OneVsOne ? 1 : seat < 3 ? 0 : 1;
                var pilot = new Pilot { name = names[(seat - 1) % names.Count] + " (BOT)", team = team };
                var bot = BotShip.Build(transform, pilot.name, model, shape, ShipColor(team, seat), team, map, () => EnemiesOf(pilot), ship => Respawn(pilot, ship));
                pilot.health = bot.GetComponent<Damageable>();
                pilot.ship = bot.transform;
                pilots.Add(pilot);
                Respawn(pilot, bot);
            }
        }

        /// <summary>Everyone a pilot fights: the other team, or everybody else in free for all.</summary>
        IEnumerable<Damageable> EnemiesOf(Pilot pilot) =>
            pilots.Where(p => p != pilot && p.health && (!Setup.Teams || p.team != pilot.team)).Select(p => p.health);

        /// <summary>A bot's ship color: its team's, or its own in free for all.</summary>
        Color ShipColor(int team, int seat) =>
            Setup.Teams ? CaptureTheFlag.TeamColor(team) : BotShip.Colors[seat % BotShip.Colors.Length];

        /// <summary>Where a pilot (re)appears: one of its team's spawn points, or spread around the map in free for all.</summary>
        public Pose SpawnFor(int team)
        {
            Transform[] spots = !Setup.Teams ? map.teamASpawns.Concat(map.teamBSpawns).ToArray()
                              : team == 0 ? map.teamASpawns : map.teamBSpawns;
            spots = spots.Where(s => s).ToArray();
            spawnTurn++;
            if (spots.Length > 0)
            {
                var spot = spots[spawnTurn % spots.Length];
                return new Pose(spot.position, ArenaMap.Level(spot.forward));
            }
            // A map without spawn points: around the player start, each team on its own side.
            float side = team % 2 == 0 ? -1f : 1f;
            Vector3 start = map.PlayerStartPosition + map.PlayerStartRotation * new Vector3((spawnTurn % 3 - 1) * 40f, 0f, side < 0 ? 0f : 600f);
            return new Pose(start, team % 2 == 0 ? map.PlayerStartRotation : map.PlayerStartRotation * Quaternion.Euler(0f, 180f, 0f));
        }

        public Pose YourSpawn => SpawnFor(you.team);

        void Respawn(Pilot pilot, BotShip bot)
        {
            if (!this || !map) return;
            var pose = SpawnFor(pilot.team);
            // A little ahead of the spawn point and spread out, so bots don't sit on top of each other.
            Vector3 spot = pose.position + pose.rotation * new Vector3(UnityEngine.Random.Range(-70f, 70f), UnityEngine.Random.Range(-20f, 40f), UnityEngine.Random.Range(60f, 160f));
            if (!map.Contains(spot)) spot = pose.position; // a narrow map (the ring tube): the spawn point itself
            bot.PlaceAt(spot, spot + pose.rotation * Vector3.forward * 100f);
        }

        // --- Scoring -------------------------------------------------------------------------------

        void OnDeath(Damageable victim, GameObject source)
        {
            if (Over) return;
            var dead = pilots.FirstOrDefault(p => p.health == victim);
            if (dead == null) return;
            dead.deaths++;
            var killer = source ? pilots.FirstOrDefault(p => p.health && source.transform.IsChildOf(p.health.transform)) : null;
            if (killer != null && killer != dead && (killer.team != dead.team || !Setup.Teams)) killer.kills++;
            ShowScore();
        }

        void OnCapture(int team)
        {
            if (Over) return;
            if (team == you.team) you.captures++;
            if (suddenDeath || flags.ScoreOf(team) >= CapturesToWin) Finish();
            else ShowScore();
        }

        int TeamKills(int team) => pilots.Where(p => p.team == team).Sum(p => p.kills);

        void ShowScore()
        {
            if (Setup.mode == GameMode.CaptureTheFlag) return; // the flag game shows its own score
            if (Setup.Teams)
                hud.ShowScore(TeamKills(you.team), TeamKills(1 - you.team));
            else
            {
                var leader = pilots.Where(p => !p.you).OrderByDescending(p => p.kills).First();
                // Your kills, then the best of the rest.
                hud.ShowScore(you.kills, leader.kills);
            }
        }


        // --- The clock and the end -----------------------------------------------------------------

        void Update()
        {
            if (Over || suddenDeath) return;
            secondsLeft = Mathf.Max(0f, secondsLeft - Time.deltaTime);
            int whole = Mathf.CeilToInt(secondsLeft);
            if (whole != shownSeconds)
            {
                shownSeconds = whole;
                hud.SetTimer($"{whole / 60}:{whole % 60:00}", secondsLeft < 30f ? Palette.Blush : Palette.Amber);
            }
            if (secondsLeft <= 0f) TimeUp();
        }

        void TimeUp()
        {
            if (Setup.mode == GameMode.CaptureTheFlag && flags && flags.ScoreOf(0) == flags.ScoreOf(1))
            {
                suddenDeath = true;
                hud.SetTimer("0:00", Palette.Blush, blink: true); // sudden death: the next capture wins
                return;
            }
            Finish();
        }

        /// <summary>End now: work out who won and hand everyone's results to whoever's listening.</summary>
        public void Finish()
        {
            if (Over) return;
            Over = true;
            string heading;
            if (Setup.Teams)
            {
                // Capture the flag counts captures; team death match counts kills.
                bool flagGame = Setup.mode == GameMode.CaptureTheFlag;
                int a = flagGame ? flags.ScoreOf(0) : TeamKills(0), b = flagGame ? flags.ScoreOf(1) : TeamKills(1);
                int winner = a > b ? 0 : b > a ? 1 : -1;
                heading = winner < 0 ? "DRAW" : winner == you.team ? "YOUR TEAM WINS!" : $"{CaptureTheFlag.TeamName(winner)} WINS";
            }
            else
            {
                int best = pilots.Max(p => p.kills);
                var top = pilots.Where(p => p.kills == best).ToList();
                heading = top.Count > 1 ? "DRAW" : top[0].you ? "YOU WIN!" : $"{top[0].name} WINS";
            }

            // Teams are listed together (free for all is one list), best first.
            var lines = pilots.OrderBy(p => Setup.Teams ? p.team : 0).ThenByDescending(p => p.kills).ThenBy(p => p.deaths)
                .Select(p => new ResultsPanel.Line { pilot = p.name, team = Setup.Teams ? p.team : 0, kills = p.kills, deaths = p.deaths, captures = p.captures, you = p.you })
                .ToList();
            hud.SetTimer("", Palette.Amber);
            Ended?.Invoke(heading, lines);
        }

        void OnDestroy()
        {
            Damageable.AnyDied -= OnDeath;
            if (flags) flags.Captured -= OnCapture;
            if (you != null && you.health) you.health.team = -1;
        }
    }
}
