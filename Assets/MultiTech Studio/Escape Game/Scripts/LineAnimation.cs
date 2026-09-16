using UnityEngine;
using Random = UnityEngine.Random;

namespace MultiTechStudio.EscapeGame
{
    /// <summary>
    /// Controls arrow line animation state transitions based on gameplay events.
    /// </summary>
    public class LineAnimation : MonoBehaviour
    {
        [SerializeField] private ArrowLine arrowLine;
        [SerializeField] private Animator animator;
        [SerializeField] private string idleAnimation = "Idle";
        [SerializeField] private string hitAnimation = "Hit";
        [SerializeField] private string startAnimation = "Start";
        [SerializeField] private string exitAnimation = "Exit";
        void Start()
        {
            arrowLine.onLineExitedAction.AddListener(AnimateOnExit);
            arrowLine.onLineHitAction.AddListener(AnimateOnHit);
            AnimateIdle();
        }

        private void AnimateIdle()
        {
            animator.speed = Random.Range(0.5f, 1.5f);
            animator.Play(idleAnimation);
        }

        private void AnimateOnHit()
        {
            animator.speed = 1;
            animator.StopPlayback();
            if (!string.IsNullOrEmpty(hitAnimation))
                animator.Play(hitAnimation);
            else
                AnimateIdle();
        }
        private void AnimateOnStart()
        {
            animator.speed = 1;
            animator.StopPlayback();
            if (!string.IsNullOrEmpty(startAnimation))
                animator.Play(startAnimation);
            else
                AnimateIdle();
        }
        private void AnimateOnExit()
        {
            animator.speed = 1;
            animator.StopPlayback();
            if (!string.IsNullOrEmpty(exitAnimation))
                animator.Play(exitAnimation);
            else
                AnimateIdle();
        }
    }
}