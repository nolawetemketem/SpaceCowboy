using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceCowboy
{
    /// <summary>
    /// The Training field, built when the game starts, wherever this object sits in the scene: two goals,
    /// the ball, faded field lines, and the score. Move and turn this object to put the field somewhere
    /// else. Its blue arrow (+Z) points at goal A, straight ahead of where the craft starts.
    ///
    /// Sizes are the web game's × 3.2, the scale of this scene. Everything sits on the ground below
    /// it, so it works on uneven ground too, though the ball rolls best on flat ground.
    /// </summary>
    public class TrainingArena : MonoBehaviour
    {
        [Header("Field")]
        [Tooltip("Goal lines are this far in front of and behind the centre.")]
        public float halfLength = 128f;
        public float halfWidth = 80f;
        public bool drawFieldLines = true;
        public float goalBoxHalfWidth = 38f;
        public float goalBoxDepth = 26f;

        [Header("Goals")]
        [Tooltip("Half the gap between the posts.")]
        public float goalHalfWidth = 19f;
        [Tooltip("Height of the crossbar's underside.")]
        public float goalHeight = 16f;
        [Tooltip("How far behind the goal line the scoring zone reaches.")]
        public float goalDepth = 10f;
        public float postThickness = 2f;
        public Color goalAColor = Palette.Amber;
        public Color goalBColor = Palette.Teal;

        [Header("Start spots")]
        [Tooltip("The craft starts this far from the centre, toward goal B, facing goal A.")]
        public float craftStartDistance = 90f;
        [Tooltip("The craft starts this high above the ground (it floats down onto its hover).")]
        public float craftStartHeight = 6f;
        [Tooltip("The ball drops in from this high above the centre.")]
        public float ballDropHeight = 10f;

        /// <summary>After a goal, ignore the goals for this long, so one goal can never count twice.</summary>
        const float LockoutSeconds = 0.5f;

        [NonSerialized] public Ball ball;
        public int ScoreA { get; private set; }
        public int ScoreB { get; private set; }
        /// <summary>Called whenever the score changes (goal A's count, goal B's count).</summary>
        public event Action<int, int> ScoreChanged;

        float lockout;
        Vector3 ballSpawn;

        /// <summary>The middle of the field, on the ground.</summary>
        public Vector3 Centre => OnGround(0f, 0f);
        public Vector3 CraftStartPosition => OnGround(0f, -craftStartDistance) + Vector3.up * craftStartHeight;
        public Quaternion CraftStartRotation => Quaternion.LookRotation(Flat(transform.forward), Vector3.up);

        /// <summary>Build the goals, the field lines and the ball. Called once by the game.</summary>
        public void Build()
        {
            ball = Ball.Create(transform);
            BuildGoal(0, 1f, goalAColor);
            BuildGoal(1, -1f, goalBColor);
            if (drawFieldLines) BuildFieldLines();
            ballSpawn = Centre + Vector3.up * (ballDropHeight + ball.radius);
            RespawnBall();
        }

        void Update()
        {
            lockout = Mathf.Max(0f, lockout - Time.deltaTime);
        }

        public void RespawnBall() => ball.Respawn(ballSpawn);

        /// <summary>Back to 0–0 (each time Training starts).</summary>
        public void ResetScore()
        {
            ScoreA = ScoreB = 0;
            lockout = 0f;
            ScoreChanged?.Invoke(ScoreA, ScoreB);
        }

        /// <summary>The ball went into a goal: count it, and put the ball back in the middle.</summary>
        public void Scored(int goal)
        {
            if (lockout > 0f) return;
            if (goal == 0) ScoreA++;
            else ScoreB++;
            lockout = LockoutSeconds;
            RespawnBall();
            ScoreChanged?.Invoke(ScoreA, ScoreB);
        }

        /// <summary>Two posts and a crossbar, plus an invisible sensor just behind the goal line.</summary>
        void BuildGoal(int goal, float end, Color color)
        {
            var root = new GameObject(goal == 0 ? "Goal A" : "Goal B").transform;
            root.SetParent(transform, false);
            var material = Palette.Toon(color);
            float z = end * halfLength;
            float postX = goalHalfWidth + postThickness / 2f;

            // The crossbar is level, goalHeight above the highest of the two posts' feet.
            float leftY = LocalGroundY(-postX, z), rightY = LocalGroundY(postX, z), middleY = LocalGroundY(0f, z);
            float barY = Mathf.Max(leftY, rightY) + goalHeight;
            foreach (var (x, footY) in new[] { (-postX, leftY), (postX, rightY) })
            {
                float sunk = 3f; // reach a little into the ground, so there's no gap on a slope
                float height = barY - footY + sunk;
                Palette.Shape(PrimitiveType.Cube, "Post", root, new Vector3(x, footY - sunk + height / 2f, z),
                    new Vector3(postThickness, height, postThickness), material, keepCollider: true);
            }
            Palette.Shape(PrimitiveType.Cube, "Crossbar", root, new Vector3(0f, barY + postThickness / 2f, z),
                new Vector3((goalHalfWidth + postThickness) * 2f, postThickness, postThickness), material, keepCollider: true);

            // The sensor starts one ball-width past the line, so it only notices the ball once the whole
            // ball is over the line (its front edge touches the sensor as its back edge crosses).
            float start = halfLength + 2f * ball.radius;
            var sensor = new GameObject("Scoring Zone");
            sensor.transform.SetParent(root, false);
            sensor.transform.localPosition = new Vector3(0f, middleY + goalHeight / 2f, end * (start + goalDepth / 2f));
            var box = sensor.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(goalHalfWidth * 2f, goalHeight + 8f, goalDepth);
            var goalSensor = sensor.AddComponent<GoalSensor>();
            goalSensor.arena = this;
            goalSensor.goal = goal;
        }

        /// <summary>Old, faded paint: the boundary, the halfway line and the centre circle, following the ground.</summary>
        void BuildFieldLines()
        {
            var paint = Palette.Unlit(new Color(0.94f, 0.88f, 0.76f, 0.55f));
            float hw = halfWidth, hl = halfLength;
            Line(paint, new List<Vector2> { new(-hw, -hl), new(hw, -hl), new(hw, hl), new(-hw, hl), new(-hw, -hl) });
            Line(paint, new List<Vector2> { new(-hw, 0f), new(hw, 0f) });
            // The goal boxes in front of each goal.
            foreach (float end in new[] { -1f, 1f })
            {
                float z0 = end * hl, z1 = end * (hl - goalBoxDepth);
                Line(paint, new List<Vector2> { new(-goalBoxHalfWidth, z0), new(-goalBoxHalfWidth, z1), new(goalBoxHalfWidth, z1), new(goalBoxHalfWidth, z0) });
            }
            var circle = new List<Vector2>();
            for (int i = 0; i <= 48; i++)
            {
                float a = i / 48f * Mathf.PI * 2f;
                circle.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 29f);
            }
            Line(paint, circle);
        }

        void Line(Material material, List<Vector2> corners)
        {
            // Walk along the line in short steps, dropping each point onto the ground.
            var points = new List<Vector3>();
            for (int i = 0; i < corners.Count - 1; i++)
            {
                Vector2 a = corners[i], b = corners[i + 1];
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 4f));
                for (int s = 0; s < steps; s++)
                {
                    Vector2 p = Vector2.Lerp(a, b, s / (float)steps);
                    points.Add(OnGround(p.x, p.y) + Vector3.up * 0.4f);
                }
            }
            Vector2 last = corners[corners.Count - 1];
            points.Add(OnGround(last.x, last.y) + Vector3.up * 0.4f);

            var go = new GameObject("Field Line");
            go.transform.SetParent(transform, false);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // lie flat: the ribbon faces up
            var line = go.AddComponent<LineRenderer>();
            line.alignment = LineAlignment.TransformZ;
            line.useWorldSpace = true;
            line.widthMultiplier = 0.7f;
            line.sharedMaterial = material;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.positionCount = points.Count;
            line.SetPositions(points.ToArray());
        }

        /// <summary>The ground point under a spot on the field (x across, z along toward goal A).</summary>
        public Vector3 OnGround(float x, float z)
        {
            Vector3 above = transform.TransformPoint(new Vector3(x, 0f, z)) + Vector3.up * 400f;
            var hits = Physics.RaycastAll(above, Vector3.down, 1200f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Vector3 ground = transform.TransformPoint(new Vector3(x, 0f, z));
            foreach (var hit in hits)
            {
                if (hit.rigidbody != null || hit.transform.IsChildOf(transform)) continue; // not the craft, ball or goals
                if (hit.distance < best) { best = hit.distance; ground = hit.point; }
            }
            return ground;
        }

        float LocalGroundY(float x, float z) => transform.InverseTransformPoint(OnGround(x, z)).y;

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-4f ? v.normalized : Vector3.forward;
        }

        void OnDrawGizmos()
        {
            // Show the field's outline and the goals in the editor, so it's easy to place.
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.9f, 0.6f, 0.8f);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(halfWidth * 2f, 1f, halfLength * 2f));
            Gizmos.color = goalAColor;
            Gizmos.DrawWireCube(new Vector3(0f, goalHeight / 2f, halfLength), new Vector3(goalHalfWidth * 2f, goalHeight, 1f));
            Gizmos.color = goalBColor;
            Gizmos.DrawWireCube(new Vector3(0f, goalHeight / 2f, -halfLength), new Vector3(goalHalfWidth * 2f, goalHeight, 1f));
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(new Vector3(0f, craftStartHeight, -craftStartDistance), 4f);
        }
    }
}
