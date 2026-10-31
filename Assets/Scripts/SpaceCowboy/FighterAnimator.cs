using UnityEngine;

namespace SpaceCowboy
{
    /// <summary>
    /// The Fighter's engines, like FighterAnimations2 but driven by the shared controls, so a controller
    /// works too: braking/reversing opens the engines; analog input gives a partial opening. The wings
    /// stay still on turns, like a real jet's. Purely visual: physics is untouched.
    /// </summary>
    public class FighterAnimator : MonoBehaviour
    {
        public Transform topEngine;
        public Transform bottomEngine;
        public CraftInput input;
        public HoverCraft craft;

        [Tooltip("How fast the parts turn (degrees/s).")]
        public float rate = 120f;
        [Tooltip("How far they turn at full input (degrees).")]
        public float maxAngle = 15f;

        Quaternion topRest, bottomRest;
        float engineAngle;

        void Start()
        {
            // Each part's resting rotation, so the turn is added on top of it.
            if (topEngine) topRest = topEngine.localRotation;
            if (bottomEngine) bottomRest = bottomEngine.localRotation;
        }

        void Update()
        {
            bool live = input && (!craft || craft.controlsEnabled);
            float throttle = live ? input.Throttle : 0f;
            float step = rate * Time.deltaTime;

            engineAngle = Mathf.MoveTowards(engineAngle, Mathf.Max(0f, -throttle) * maxAngle, step);
            if (topEngine) topEngine.localRotation = topRest * Quaternion.Euler(0f, engineAngle, 0f);
            if (bottomEngine) bottomEngine.localRotation = bottomRest * Quaternion.Euler(0f, engineAngle, 0f);
        }
    }
}
