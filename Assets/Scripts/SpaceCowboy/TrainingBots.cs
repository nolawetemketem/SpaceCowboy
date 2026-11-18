using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaceCowboy
{
    /// <summary>
    /// The bots in Training: the same computer pilots as a free-for-all match (same colors, names, health and flying),
    /// all against everyone, you included. Nothing is scored. A destroyed bot comes back somewhere in front of
    /// the start spot.
    /// </summary>
    public class TrainingBots : MonoBehaviour
    {
        /// <summary>As many as the other seats in a free for all.</summary>
        public const int Count = 5;
        const float NearestDistance = 150f, FarthestDistance = 450f, WidestAngle = 80f;
        const float LowestHeight = 30f, HighestHeight = 120f, GroundClearance = 25f;

        ArenaMap map;
        Damageable you;
        readonly List<Damageable> everyone = new();

        public IEnumerable<Transform> Ships => everyone.Where(d => d && d != you).Select(d => d.transform);

        public static TrainingBots Create(ArenaMap map, HoverCraft craft, Fighter yours)
        {
            var bots = new GameObject("Training Bots").AddComponent<TrainingBots>();
            bots.map = map;
            bots.you = craft.GetComponent<Damageable>();
            bots.everyone.Add(bots.you);
            var model = craft.transform.Find("BankPivot");
            var shape = craft.GetComponent<BoxCollider>();
            var names = Fighters.All.Where(f => f != yours).Select(f => f.name).ToList();
            for (int seat = 1; seat <= Count; seat++)
            {
                var ship = BotShip.Build(bots.transform, names[(seat - 1) % names.Count] + " (BOT)", model, shape,
                    BotShip.Colors[seat % BotShip.Colors.Length], seat, map, bots.EnemiesOf, bots.Respawn);
                bots.everyone.Add(ship.GetComponent<Damageable>());
                bots.Respawn(ship);
            }
            return bots;
        }

        /// <summary>Everyone fights everyone (each bot skips itself).</summary>
        IEnumerable<Damageable> EnemiesOf() => everyone.Where(d => d);

        /// <summary>Put a bot at a fresh spot ahead of the start position.</summary>
        void Respawn(BotShip ship)
        {
            if (!this || !map) return; // the map was switched; these bots are on their way out
            Vector3 start = map.PlayerStartPosition;
            Vector3 ahead = Quaternion.AngleAxis(Random.Range(-WidestAngle, WidestAngle), Vector3.up) * (map.PlayerStartRotation * Vector3.forward);
            Vector3 spot = start + ahead * Random.Range(NearestDistance, FarthestDistance) + Vector3.up * Random.Range(LowestHeight, HighestHeight);
            spot.y = Mathf.Max(spot.y, ArenaMap.GroundBelow(spot).y + GroundClearance); // never inside a dune or mesa
            ship.PlaceAt(spot, spot + ahead * 100f);
        }
    }
}
