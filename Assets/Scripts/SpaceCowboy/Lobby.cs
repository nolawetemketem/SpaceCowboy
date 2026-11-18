using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaceCowboy
{
    /// <summary>
    /// The lobby before a match: every pilot votes for one of a few maps, and you pick your fighter.
    /// The match starts when you're ready and everyone has voted, or when the countdown runs out.
    /// The most votes wins; a tie is settled by chance. Nobody voting at all picks one by chance too.
    ///
    /// For now the other pilots are the dummies, who vote on their own after a moment.
    /// </summary>
    public class Lobby
    {
        public const float Countdown = 30f;
        const int Choices = 3;

        public readonly MatchSetup setup;
        /// <summary>The maps up for the vote (scene names).</summary>
        public readonly List<string> maps;
        /// <summary>Each pilot's vote (an index into maps, or -1). Seat 0 is you.</summary>
        readonly int[] votes;
        readonly float[] votesAt;
        public float SecondsLeft { get; private set; } = Countdown;
        public bool Ready { get; private set; }
        public int YourVote => votes[0];
        public int Seats => votes.Length;
        public int Voted => votes.Count(v => v >= 0);
        public bool AllVoted => Voted == votes.Length;

        /// <summary>The maps that can host this mode: every map but the training area (capture the flag needs flags).</summary>
        public static List<string> MapsFor(GameMode mode, string trainingMap)
        {
            var all = Maps.All();
            var fit = all.Where(m => m != trainingMap && (mode != GameMode.CaptureTheFlag || Maps.HasFlags(m))).ToList();
            if (fit.Count == 0) fit = all.Where(m => mode != GameMode.CaptureTheFlag || Maps.HasFlags(m)).ToList();
            return fit;
        }

        public Lobby(MatchSetup setup, string trainingMap)
        {
            this.setup = setup;
            maps = MapsFor(setup.mode, trainingMap).OrderBy(_ => Random.value).Take(Choices).ToList();
            votes = Enumerable.Repeat(-1, setup.Seats).ToArray();
            votesAt = new float[votes.Length];
            for (int seat = 1; seat < votesAt.Length; seat++) votesAt[seat] = Random.Range(1.5f, 7f);
        }

        public int VotesFor(int map) => votes.Count(v => v == map);

        public void Vote(int map) => votes[0] = map;

        public void SetReady() => Ready = true;

        /// <summary>Count down and let the dummies vote. True once it's time to start.</summary>
        public bool Tick(float dt)
        {
            SecondsLeft = Mathf.Max(0f, SecondsLeft - dt);
            float waited = Countdown - SecondsLeft;
            for (int seat = 1; seat < votes.Length; seat++)
                if (votes[seat] < 0 && waited >= votesAt[seat]) votes[seat] = Random.Range(0, maps.Count);
            return maps.Count > 0 && (SecondsLeft <= 0f || (Ready && AllVoted));
        }

        /// <summary>The map with the most votes (ties settled by chance).</summary>
        public string Winner()
        {
            int best = Enumerable.Range(0, maps.Count).Max(VotesFor);
            var top = Enumerable.Range(0, maps.Count).Where(i => VotesFor(i) == best).ToList();
            return maps[top[Random.Range(0, top.Count)]];
        }
    }
}
