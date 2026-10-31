using UnityEngine;

namespace SpaceCowboy
{
    /// <summary>The invisible box just behind a goal line. Tells the arena when the ball is in.</summary>
    [RequireComponent(typeof(BoxCollider))]
    public class GoalSensor : MonoBehaviour
    {
        public TrainingArena arena;
        /// <summary>0 = goal A, 1 = goal B.</summary>
        public int goal;

        void OnTriggerEnter(Collider other)
        {
            if (arena && arena.ball && other.attachedRigidbody == arena.ball.Body) arena.Scored(goal);
        }
    }
}
