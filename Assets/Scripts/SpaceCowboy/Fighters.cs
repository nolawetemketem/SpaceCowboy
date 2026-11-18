using UnityEngine;

namespace SpaceCowboy
{
    /// <summary>The six specials. Each fighter has one; a 3-kill streak charges it.</summary>
    public enum SpecialKind { Spy, Phantom, Hopper, Sniper, Tank, Racer }

    /// <summary>
    /// A fighter you can pick before a match. For now they all fly the same ship model; what differs is the name
    /// and the special. Later each gets its own model and personality.
    /// </summary>
    public class Fighter
    {
        public readonly string name, specialName, blurb;
        public readonly SpecialKind special;

        public Fighter(string name, SpecialKind special, string specialName, string blurb)
        {
            this.name = name;
            this.special = special;
            this.specialName = specialName;
            this.blurb = blurb;
        }
    }

    public static class Fighters
    {
        public static readonly Fighter[] All =
        {
            new("SPY", SpecialKind.Spy, "X-RAY", "SEE ENEMIES THROUGH WALLS FOR 30 SECONDS"),
            new("PHANTOM", SpecialKind.Phantom, "GHOST", "INVISIBLE, AND FLY THROUGH WALLS FOR 30 SECONDS"),
            new("HOPPER", SpecialKind.Hopper, "PORTALS", "PLACE TWO LINKED PORTALS FOR YOUR TEAM, 30 SECONDS"),
            new("SNIPER", SpecialKind.Sniper, "BEAM CANNON", "ONE LASER SHOT THAT DESTROYS ANYTHING IT HITS"),
            new("TANK", SpecialKind.Tank, "BULLDOZER", "RAM SHIPS TO DESTROY THEM FOR 30 S, PLUS A SHIELD"),
            new("RACER", SpecialKind.Racer, "NITRO", "A 30-SECOND TANK OF NITRO: HOLD TO BOOST"),
        };

        const string SaveKey = "SpaceCowboy.Fighter";

        /// <summary>The fighter you picked last (remembered between sessions).</summary>
        public static Fighter Chosen
        {
            get => All[Mathf.Clamp(PlayerPrefs.GetInt(SaveKey, 0), 0, All.Length - 1)];
            set
            {
                PlayerPrefs.SetInt(SaveKey, System.Array.IndexOf(All, value));
                PlayerPrefs.Save();
            }
        }
    }

    public enum GameMode { Training, DeathMatch, CaptureTheFlag }
    public enum TeamSize { FreeForAll, OneVsOne, ThreeVsThree }

    /// <summary>Everything chosen on the menu before a match starts.</summary>
    public class MatchSetup
    {
        public GameMode mode = GameMode.Training;
        public TeamSize size = TeamSize.OneVsOne;
        /// <summary>Every match is the same length (no choosing on the menu).</summary>
        public const int Minutes = 5;
        public string map;
        public Fighter fighter = Fighters.Chosen;

        public bool Teams => size != TeamSize.FreeForAll;
        /// <summary>How many pilots: 2 in a 1v1, 6 otherwise.</summary>
        public int Seats => size == TeamSize.OneVsOne ? 2 : 6;

        /// <summary>Like "DEATH MATCH · 3V3".</summary>
        public string Title
        {
            get
            {
                string mode = this.mode switch { GameMode.DeathMatch => "DEATH MATCH", GameMode.CaptureTheFlag => "CAPTURE THE FLAG", _ => "TRAINING" };
                if (this.mode == GameMode.Training) return mode;
                string size = this.size switch { TeamSize.FreeForAll => "FREE FOR ALL", TeamSize.OneVsOne => "1V1", _ => "3V3" };
                return $"{mode} · {size}";
            }
        }
    }
}
