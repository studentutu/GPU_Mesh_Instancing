//----------------------------------------------
// Mesh Animator
// Flick Shot Games
// http://www.flickshotgames.com
//----------------------------------------------

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace FSG.MeshAnimator
{
    public class MeshAnimatorManager : MonoBehaviour
    {
        public static int AnimatorCount { get { if (Instance) return mAnimators.Count; return 0; } }
        public static MeshAnimatorManager Instance
        {
            get
            {
                if (mInstance == null)
                {
                    mInstance = FindObjectOfType<MeshAnimatorManager>();
                    if (mInstance == null)
                    {
                        mInstance = new GameObject("MeshAnimatorManager").AddComponent<MeshAnimatorManager>();
                    }
                }
                return mInstance;
            }
        }

        private static AnimatorUpdateMode mode = AnimatorUpdateMode.Normal;
        private static MeshAnimatorManager mInstance = null;
        private static List<MeshAnimator> mAnimators = new List<MeshAnimator>(100);

        public static void AddAnimator(MeshAnimator animator)
        {
            if (Instance)
                mAnimators.Add(animator);
        }
        public static void RemoveAnimator(MeshAnimator animator)
        {
            mAnimators.Remove(animator);
        }
        public static void SetUpdateMode(AnimatorUpdateMode updateMode)
        {
            mode = updateMode;
            if (mode == AnimatorUpdateMode.UnscaledTime && mInstance != null)
            {
                mInstance.StartCoroutine(mInstance.UnscaledUpdate());
            }
        }

        private void Awake()
        {
            if (mode == AnimatorUpdateMode.UnscaledTime)
                StartCoroutine(UnscaledUpdate());
        }
        private IEnumerator UnscaledUpdate()
        {
            while (enabled && mode == AnimatorUpdateMode.UnscaledTime)
            {
                UpdateTick(Time.realtimeSinceStartup);
                yield return null;
            }
        }
        private void Update()
        {
            if (mode == AnimatorUpdateMode.Normal)
                UpdateTick(Time.time);
        }
        private void FixedUpdate()
        {
            if (mode == AnimatorUpdateMode.AnimatePhysics)
                UpdateTick(Time.time);
        }
        private void UpdateTick(float time)
        {
            int c = mAnimators.Count;
            for (int i = 0; i < c; i++)
            {
                MeshAnimator animator = mAnimators[i];
                if (time >= animator.nextTick)
                {
                    // try
                    // {
                    animator.UpdateTick(time);
                    // }
                    // catch (System.Exception ex)
                    // {
                    // Debug.LogException(ex);
                    // }
                }
            }
        }
    }
}