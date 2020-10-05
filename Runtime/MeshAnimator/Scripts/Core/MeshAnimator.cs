//----------------------------------------------
// Mesh Animator
// Flick Shot Games
// http://www.flickshotgames.com
//----------------------------------------------

#if !UNITY_WEBGL
#define THREADS_ENABLED
#endif

#if UNITY_SWITCH
#define USE_TRIANGLE_DATA
#endif

using UnityEngine;
using System.Collections.Generic;
using System;
using System.Runtime.CompilerServices;

namespace FSG.MeshAnimator
{
    /// <summary>
    /// Handles animation playback and swapping of mesh frames on the target MeshFilter
    /// </summary>
    [AddComponentMenu("Miscellaneous/Mesh Animator")]
    [RequireComponent(typeof(MeshFilter))]
    public class MeshAnimator : MonoBehaviour
    {
        private static Dictionary<Mesh, int> s_meshCount = new Dictionary<Mesh, int>();
#if USE_TRIANGLE_DATA
        private static Dictionary<Mesh, Mesh> s_modifiedMeshCache = new Dictionary<Mesh, Mesh>();
#endif
#if THREADS_ENABLED
        // static crossfade threading
        private static List<System.Threading.Thread> s_crossfadeThreads = new List<System.Threading.Thread>();
        private static bool s_shutDownThreads = false;
        private static Queue<MeshAnimator> s_crossfadeAnimators = new Queue<MeshAnimator>(1000);
#endif
        // static crossfade pooling
        private static List<Stack<Mesh>> s_crossFadePool = new List<Stack<Mesh>>(10);
        private static Dictionary<Mesh, int> s_crossFadeIndex = new Dictionary<Mesh, int>();

        [Serializable]
        public class MeshAnimatorLODLevel
        {
            public int fps;
            public float distance;
            public float distanceSquared;
        }
        private struct CurrentCrossFade
        {
            public MeshFrameData fromFrame;
            public MeshFrameData toFrame;
            public int framesNeeded;
            public int currentFrame;
            public int generatedFrame;
            public bool isFading;
            public float endTime;
            public CrossFadeFrameData frame;

            public void Reset()
            {
                fromFrame = null;
                toFrame = null;
                isFading = false;
                endTime = 0;
                currentFrame = 0;
                generatedFrame = -1;
                framesNeeded = 0;
                ReturnFrame();
            }
            public void PopulateFrame(int length)
            {
                if (frame == null)
                {
                    frame = new CrossFadeFrameData();
                }
                if (frame.positions == null)
                {
                    frame.positions = AllocatedArray<Vector3>.Get(length);
                }
                if (frame.normals == null)
                {
                    frame.normals = AllocatedArray<Vector3>.Get(length);
                }
            }
            public void ReturnFrame()
            {
                if (frame != null)
                {
                    if (frame.positions != null)
                        AllocatedArray<Vector3>.Return(frame.positions, false);
                    if (frame.normals != null)
                        AllocatedArray<Vector3>.Return(frame.normals, false);
                    frame.positions = null;
                    frame.normals = null;
                }
            }
        }
        private class CrossFadeFrameData
        {
            public Vector3[] positions;
            public Vector3[] normals;
            public Bounds bounds;
        }
        public Mesh baseMesh;
        public MeshAnimation defaultAnimation;
        public MeshAnimation[] animations;
        public float speed = 1;
        public bool updateWhenOffscreen = false;
        public bool playAutomatically = true;
        public bool resetOnEnable = true;
        public GameObject eventReciever;
        public int FPS = 30;
        public bool skipLastLoopFrame = false;
        public bool recalculateCrossfadeNormals = false;
        public Action<string> OnAnimationFinished = delegate { };
        public Action OnFrameUpdated = delegate { };
        public Action<bool> OnVisibilityChanged = delegate { };
        public int currentFrame;
        public Transform LODCamera;
        public MeshAnimatorLODLevel[] LODLevels = new MeshAnimatorLODLevel[0];

        [HideInInspector]
        public float nextTick = 0;
        [HideInInspector]
        public MeshFilter meshFilter;
        [HideInInspector]
        public MeshAnimation currentAnimation;
        [HideInInspector]
        public MeshTriangleData[] meshTriangleData;
        [HideInInspector]
        public Vector2[] uv1Data;
        [HideInInspector]
        public Vector2[] uv2Data;
        [HideInInspector]
        public Vector2[] uv3Data;
        [HideInInspector]
        public Vector2[] uv4Data;

        private int currentAnimIndex = -1;
        private bool isVisible = true;
        private float lastFrameTime;
        private bool pingPong = false;
        private bool isPaused = false;
        private float currentAnimTime;
        private Mesh crossFadeMesh;
        private Queue<string> queuedAnims;
        private CurrentCrossFade currentCrossFade;
        private int currentLodLevel = 0;
        private Transform mTransform;
        private Dictionary<string, Transform> childMap;
        private bool initialized = false;
        private int previousEventFrame = -1;
        private bool hasExposedTransforms;
        private int crossFadePoolIndex = -1;

        #region Private Methods
#if USE_TRIANGLE_DATA
        // Nintendo Switch currently changes triangle ordering when built
        // so override the mesh triangles when a new instance is created
        private void Awake()
        {
            if (meshTriangleData != null)
            {
                Mesh sourceMesh = baseMesh;
                if (sourceMesh != null)
                {
                    Mesh modifiedMesh = null;
                    if (!s_modifiedMeshCache.TryGetValue(sourceMesh, out modifiedMesh))
                    {
                        modifiedMesh = Instantiate(baseMesh);
                        for (int i = 0; i < meshTriangleData.Length; i++)
                        {
                            modifiedMesh.SetTriangles(meshTriangleData[i].triangles, meshTriangleData[i].submesh);   
                        }
                        if (uv1Data != null)
                            modifiedMesh.uv = uv1Data;
                        if (uv2Data != null)
                            modifiedMesh.uv2 = uv2Data;
                        if (uv3Data != null)
                            modifiedMesh.uv3 = uv3Data;
                        if (uv4Data != null)
                            modifiedMesh.uv4 = uv4Data;
                        baseMesh = modifiedMesh;
                        s_modifiedMeshCache.Add(sourceMesh, baseMesh);
                    }
                    else
                    {
                        baseMesh = modifiedMesh;
                    }
                }
            }
        }
#endif
        private void Start()
        {
            if (animations.Length == 0)
            {
                Debug.LogWarning("No animations for MeshAnimator on object: " + name + ". Disabling.", this);
                this.enabled = false;
                return;
            }

            for (int i = 0; i < animations.Length; i++)
            {
                MeshAnimation animation = animations[i];
                if (animation == null)
                    continue;
                animation.GenerateFrames(baseMesh);
                if (animation.exposedTransforms != null)
                {
                    for (int j = 0; j < animation.exposedTransforms.Length; j++)
                    {
                        string childName = animation.exposedTransforms[j];
                        if (string.IsNullOrEmpty(childName))
                            continue;
                        Transform childTransform = transform.Find(childName);
                        if (childTransform != null)
                        {
                            if (childMap == null)
                            {
                                childMap = new Dictionary<string, Transform>();
                            }
                            if (childMap.ContainsKey(childName) == false)
                            {
                                childMap.Add(childName, childTransform);
                                hasExposedTransforms = true;
                            }
                        }
                    }
                }
            }

            if (meshFilter == null)
                meshFilter = GetComponent<MeshFilter>();

            if (!s_meshCount.ContainsKey(baseMesh))
            {
                s_meshCount.Add(baseMesh, 1);
            }
            else
            {
                s_meshCount[baseMesh]++;
            }

            if (playAutomatically)
                Play(defaultAnimation.name);
            else
                isPaused = true;

#if THREADS_ENABLED
            if (s_crossfadeThreads.Count < MeshAnimatorManager.AnimatorCount / 15f && s_crossfadeThreads.Count < 20)
            {
                s_shutDownThreads = false;
                var t = new System.Threading.Thread(new System.Threading.ThreadStart(GenerateThreadedCrossfade));
                t.Start();
                s_crossfadeThreads.Add(t);
            }
#endif
            for (int i = 0; i < LODLevels.Length; i++)
            {
                float d = LODLevels[i].distance;
                LODLevels[i].distanceSquared = d * d;
            }
            initialized = true;
        }
        private void OnBecameVisible()
        {
            isVisible = true;
            OnVisibilityChanged(isVisible);
        }
        private void OnBecameInvisible()
        {
            isVisible = false;
            OnVisibilityChanged(isVisible);
        }
        private void OnEnable()
        {
            mTransform = transform;
            if (resetOnEnable && meshFilter)
            {
                if (playAutomatically)
                {
                    Play(defaultAnimation.name);
                }
                else
                {
                    isPaused = true;
                }
                if (currentAnimation != null)
                {
                    currentAnimation.GenerateFrameIfNeeded(baseMesh, currentFrame);
                    currentAnimation.DisplayFrame(meshFilter, currentFrame, -1);
                }
            }
            MeshAnimatorManager.AddAnimator(this);
            lastFrameTime = Time.time;
        }
        private void OnDisable()
        {
            MeshAnimatorManager.RemoveAnimator(this);
            currentCrossFade.Reset();
            currentAnimIndex = -1;
            pingPong = false;
            if (queuedAnims != null)
                queuedAnims.Clear();
        }
        private void OnDestroy()
        {
            if (s_meshCount.ContainsKey(baseMesh) == false)
                return;
            s_meshCount[baseMesh]--;
            ReturnCrossfadeToPool();
            if (s_meshCount[baseMesh] <= 0)
            {
                s_meshCount.Remove(baseMesh);
                foreach (var v in MeshAnimation.generatedFrames[baseMesh])
                {
                    for (int i = 0; i < v.Value.Length; i++)
                    {
                        DestroyImmediate(v.Value[i]);
                    }
                }
                MeshAnimation.generatedFrames.Remove(baseMesh);
                for (int i = 0; i < animations.Length; i++)
                {
                    animations[i].Reset();
                }
                if (crossFadePoolIndex > -1)
                {
                    Stack<Mesh> meshStack = null;
                    lock (s_crossFadePool)
                    {
                        meshStack = s_crossFadePool[crossFadePoolIndex];
                        s_crossFadePool.RemoveAt(crossFadePoolIndex);
                        s_crossFadeIndex.Remove(baseMesh);
                        crossFadePoolIndex = -1;
                    }
                    while (meshStack.Count > 0)
                    {
                        Destroy(meshStack.Pop());
                    }
                }
            }
#if THREADS_ENABLED
            if (s_meshCount.Count == 0)
            {
                s_crossfadeThreads.Clear();
                s_shutDownThreads = true;
                lock (s_crossfadeAnimators)
                    s_crossfadeAnimators.Clear();
            }
#endif
        }
        private void FireAnimationEvents(MeshAnimation cAnim, float totalSpeed, bool finished)
        {
            if (cAnim.events.Length > 0 && eventReciever != null && previousEventFrame != currentFrame)
            {
                if (finished)
                {
                    if (totalSpeed < 0)
                    {
                        // fire off animation events, including skipped frames
                        for (int i = previousEventFrame; i >= 0; i++)
                            cAnim.FireEvents(eventReciever, i);
                        previousEventFrame = 0;
                    }
                    else
                    {
                        // fire off animation events, including skipped frames
                        for (int i = previousEventFrame; i <= cAnim.totalFrames; i++)
                            cAnim.FireEvents(eventReciever, i);
                        previousEventFrame = -1;
                    }
                    return;
                }
                else
                {
                    if (totalSpeed < 0)
                    {
                        // fire off animation events, including skipped frames
                        for (int i = currentFrame; i > previousEventFrame; i--)
                            cAnim.FireEvents(eventReciever, i);
                    }
                    else
                    {
                        // fire off animation events, including skipped frames
                        for (int i = previousEventFrame + 1; i <= currentFrame; i++)
                            cAnim.FireEvents(eventReciever, i);
                    }
                    previousEventFrame = currentFrame;
                }
            }
        }
        private Mesh GetCrossfadeFromPool()
        {
            if (crossFadePoolIndex > -1)
            {
                lock (s_crossFadePool)
                {
                    Stack<Mesh> meshStack = s_crossFadePool[crossFadePoolIndex];
                    if (meshStack.Count > 0)
                        return meshStack.Pop();
                }
            }
            return (Mesh)Instantiate(baseMesh);
        }
        private void ReturnCrossfadeToPool()
        {
            if (crossFadeMesh != null)
            {
                Stack<Mesh> meshStack = null;
                lock (s_crossFadePool)
                {
                    if (crossFadePoolIndex < 0)
                    {
                        if (!s_crossFadeIndex.TryGetValue(baseMesh, out crossFadePoolIndex))
                        {
                            crossFadePoolIndex = s_crossFadePool.Count;
                            s_crossFadeIndex.Add(baseMesh, crossFadePoolIndex);
                            meshStack = new Stack<Mesh>();
                            s_crossFadePool.Add(meshStack);
                        }
                        else
                        {
                            meshStack = s_crossFadePool[crossFadePoolIndex];
                        }
                    }
                    else
                    {
                        meshStack = s_crossFadePool[crossFadePoolIndex];
                    }
                    meshStack.Push(crossFadeMesh);
                }
                crossFadeMesh = null;
            }
            currentCrossFade.Reset();
        }
        #endregion

        #region Static Crossfading
        /// <summary>
        /// Generates the queued crossfade frame
        /// </summary>
        private void GenerateCrossfadeFrame()
        {
            if (currentCrossFade.generatedFrame == currentCrossFade.currentFrame)
            {
                return;
            }
            int vertexCount = currentCrossFade.toFrame.verts.Length;
            currentCrossFade.PopulateFrame(vertexCount);
            Vector3[] from = currentCrossFade.fromFrame.verts;
            Vector3[] to = currentCrossFade.toFrame.verts;
            // generate the frames for the crossfade
            CrossFadeFrameData frame = currentCrossFade.frame;
            Vector3 center = Vector3.zero;
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            float delta = currentCrossFade.currentFrame / (float)currentCrossFade.framesNeeded;
            for (int j = 0; j < frame.positions.Length; j++)
            {
                Vector3 pos = Vector3.Lerp(from[j], to[j], delta);
                if (pos.x < min.x) min.x = pos.x;
                if (pos.y < min.y) min.y = pos.y;
                if (pos.z < min.z) min.z = pos.z;
                if (pos.x > max.x) max.x = pos.x;
                if (pos.y > max.y) max.y = pos.y;
                if (pos.z > max.z) max.z = pos.z;
                center += pos;
                frame.positions[j] = pos;
            }
            center /= frame.positions.Length;
            currentCrossFade.frame = frame;
            currentCrossFade.frame.bounds = new Bounds(center, max - min);
            currentCrossFade.generatedFrame = currentCrossFade.currentFrame;
        }

        /// <summary>
        /// Enqueue a MeshAnimator for crossfading
        /// </summary>
        private static void EnqueueAnimatorForCrossfade(MeshAnimator animator)
        {
#if THREADS_ENABLED
            lock (s_crossfadeAnimators)
            {
                s_crossfadeAnimators.Enqueue(animator);
            }
#endif
        }

        /// <summary>
        /// Dequeue the next MeshAnimator waiting for a crossfade
        /// </summary>
        private static MeshAnimator DequeueAnimatorForCrossfade()
        {
#if THREADS_ENABLED
            lock (s_crossfadeAnimators)
            {
                if (s_crossfadeAnimators.Count == 0)
                    return null;
                return s_crossfadeAnimators.Dequeue();
            }
#else
			return null;
#endif
        }

        /// <summary>
        /// Lerps all values in the Matrix4x4
        /// </summary>
        private static Matrix4x4 MatrixLerp(Matrix4x4 from, Matrix4x4 to, float time)
        {
            Matrix4x4 ret = new Matrix4x4();
            for (int i = 0; i < 16; i++)
                ret[i] = Mathf.Lerp(from[i], to[i], time);
            return ret;
        }
#if THREADS_ENABLED
        private static void GenerateThreadedCrossfade()
        {
            // generates crossfade frames in queued multi-threaded order
            // lightens the load on the update function of the animator
            while (!s_shutDownThreads)
            {
                try
                {
                    MeshAnimator ma = null;
                    while ((ma = DequeueAnimatorForCrossfade()) != null)
                    {
                        if (ma.currentCrossFade.isFading)
                        {
                            ma.GenerateCrossfadeFrame();
                            System.Threading.Thread.Sleep(0);
                        }
                    }
                }
                catch { }
                int count = 0;
                do
                {
                    lock (s_crossfadeAnimators)
                    {
                        count = s_crossfadeAnimators.Count;
                    }
                    if (count == 0)
                    {
                        System.Threading.Thread.Sleep(1);
                    }
                }
                while (count == 0);
            }
        }
#endif
        #endregion

        #region Public Methods
        /// <summary>
        /// The main update loop called from the MeshAnimatorManager
        /// </summary>
        /// <param name="time">Current time</param>
        public void UpdateTick(float time)
        {
            if (initialized == false)
                return;
            if (animations.Length == 0)
                return;
            if (currentAnimIndex < 0 || currentAnimIndex > animations.Length)
            {
                if (defaultAnimation != null)
                    Play(defaultAnimation.animationName);
                else
                    Play(0);
            }
            if ((isVisible == false && updateWhenOffscreen == false) || isPaused || speed == 0 || currentAnimation.playbackSpeed == 0) // return if offscreen or crossfading
            {
                return;
            }
            // update the lod level if needed
            if (LODLevels.Length > 0)
            {
                bool hasLODCamera = LODCamera != null;
                if (!hasLODCamera)
                {
                    int cameraCount = Camera.allCamerasCount;
                    if (cameraCount > 0)
                    {
                        Camera[] cameras = AllocatedArray<Camera>.Get(cameraCount);
                        cameraCount = Camera.GetAllCameras(cameras);
                        LODCamera = cameras[0].transform;
                        AllocatedArray<Camera>.Return(cameras);
                        hasLODCamera = true;
                    }
                }
                if (hasLODCamera)
                {
                    float dis = (LODCamera.position - mTransform.position).sqrMagnitude;
                    int lodLevel = 0;
                    for (int i = 0; i < LODLevels.Length; i++)
                    {
                        if (dis > LODLevels[i].distanceSquared)
                        {
                            lodLevel = i;
                        }
                    }
                    if (currentLodLevel != lodLevel)
                    {
                        currentLodLevel = lodLevel;
                    }
                }
            }
            // if the speed is below the normal playback speed, wait until the next frame can display
            float lodFPS = LODLevels.Length > currentLodLevel ? LODLevels[currentLodLevel].fps : FPS;
            if (lodFPS == 0.0f)
            {
                return;
            }
            float totalSpeed = Math.Abs(currentAnimation.playbackSpeed * speed);
            float calculatedTick = 1f / lodFPS / totalSpeed;
            float tickRate = 0.0001f;
            if (calculatedTick > 0.0001f)
                tickRate = calculatedTick;
            float actualDelta = time - lastFrameTime;
            bool finished = false;

            float pingPongMult = pingPong ? -1 : 1;
            if (speed * currentAnimation.playbackSpeed < 0)
                currentAnimTime -= actualDelta * pingPongMult * totalSpeed;
            else
                currentAnimTime += actualDelta * pingPongMult * totalSpeed;

            if (currentAnimTime < 0)
            {
                currentAnimTime = currentAnimation.length;
                finished = true;
            }
            else if (currentAnimTime > currentAnimation.length)
            {
                if (currentAnimation.wrapMode == WrapMode.Loop)
                    currentAnimTime = currentAnimTime - currentAnimation.length;
                finished = true;
            }

            nextTick = time + tickRate;
            lastFrameTime = time;

            float normalizedTime = currentAnimTime / currentAnimation.length;
            int previousFrame = currentFrame;
            currentFrame = Math.Min((int)Math.Round(normalizedTime * currentAnimation.totalFrames), currentAnimation.totalFrames - 1);

            // do WrapMode.PingPong
            if (currentAnimation.wrapMode == WrapMode.PingPong)
            {
                if (finished)
                {
                    pingPong = !pingPong;
                }
            }

            if (finished)
            {
                bool stopUpdate = false;
                if (queuedAnims != null && queuedAnims.Count > 0)
                {
                    Play(queuedAnims.Dequeue());
                    stopUpdate = true;
                }
                else if (currentAnimation.wrapMode != WrapMode.Loop && currentAnimation.wrapMode != WrapMode.PingPong)
                {
                    nextTick = float.MaxValue;
                    stopUpdate = true;
                }
                OnAnimationFinished(currentAnimation.animationName);
                if (stopUpdate)
                {
                    FireAnimationEvents(currentAnimation, totalSpeed, finished);
                    return;
                }
            }

            // generate frames if needed and show the current animation frame
            currentAnimation.GenerateFrameIfNeeded(baseMesh, currentFrame);

            // if crossfading, lerp the vertices to the next frame
            if (currentCrossFade.isFading)
            {
                if (currentCrossFade.currentFrame >= currentCrossFade.framesNeeded)
                {
                    currentFrame = 0;
                    previousFrame = -1;
                    currentAnimTime = 0;
                    ReturnCrossfadeToPool();
                }
                else
                {
#if !THREADS_ENABLED
					GenerateCrossfadeFrame();
#endif
                    if (currentCrossFade.generatedFrame >= currentCrossFade.currentFrame)
                    {
                        if (crossFadeMesh == null)
                            crossFadeMesh = GetCrossfadeFromPool();
                        crossFadeMesh.vertices = currentCrossFade.frame.positions;
                        crossFadeMesh.bounds = currentCrossFade.frame.bounds;
                        if (recalculateCrossfadeNormals)
                            crossFadeMesh.RecalculateNormals();
                        meshFilter.sharedMesh = crossFadeMesh;
                        currentCrossFade.ReturnFrame();
                        currentCrossFade.currentFrame++;
                        if (currentCrossFade.currentFrame < currentCrossFade.framesNeeded)
                        {
                            EnqueueAnimatorForCrossfade(this);
                        }
                        // move exposed transforms
                        bool applyRootMotion = currentAnimation.rootMotionMode == MeshAnimation.RootMotionMode.AppliedToTransform;
                        if (hasExposedTransforms || applyRootMotion)
                        {
                            float delta = currentCrossFade.currentFrame / (float)currentCrossFade.framesNeeded;
                            MeshFrameData fromFrame = currentCrossFade.fromFrame;
                            MeshFrameData toFrame = currentCrossFade.toFrame;
                            // move exposed transforms
                            if (hasExposedTransforms)
                            {
                                for (int i = 0; i < currentAnimation.exposedTransforms.Length; i++)
                                {
                                    Transform child = null;
                                    if (fromFrame.exposedTransforms.Length <= i || toFrame.exposedTransforms.Length <= i)
                                        continue;
                                    if (childMap.TryGetValue(currentAnimation.exposedTransforms[i], out child))
                                    {
                                        Matrix4x4 f = fromFrame.exposedTransforms[i];
                                        Matrix4x4 t = toFrame.exposedTransforms[i];
                                        Matrix4x4 c = MatrixLerp(f, t, delta);
                                        MatrixUtils.FromMatrix4x4(child, c);
                                    }
                                }
                            }
                            // apply root motion
                            if (applyRootMotion)
                            {
                                Vector3 pos = Vector3.Lerp(fromFrame.rootMotionPosition, toFrame.rootMotionPosition, delta);
                                Quaternion rot = Quaternion.Lerp(fromFrame.rootMotionRotation, toFrame.rootMotionRotation, delta);
                                transform.Translate(pos, Space.Self);
                                transform.Rotate(rot.eulerAngles * Time.deltaTime, Space.Self);
                            }
                        }
                    }
                }
            }
            if (currentCrossFade.isFading == false)
            {
                currentAnimation.DisplayFrame(meshFilter, currentFrame, previousFrame);
                if (currentFrame != previousFrame)
                {
                    bool applyRootMotion = currentAnimation.rootMotionMode == MeshAnimation.RootMotionMode.AppliedToTransform;
                    if (hasExposedTransforms || applyRootMotion)
                    {
                        MeshFrameData fromFrame = currentAnimation.GetNearestFrame(currentFrame);
                        MeshFrameData targetFrame = null;
                        int frameGap = currentFrame % currentAnimation.frameSkip;
                        bool needsInterp = actualDelta > 0 && frameGap != 0;
                        float blendDelta = 0;
                        if (needsInterp)
                        {
                            blendDelta = currentAnimation.GetInterpolatingFrames(currentFrame, out fromFrame, out targetFrame);
                        }
                        // move exposed transforms
                        if (hasExposedTransforms)
                        {
                            for (int i = 0; i < currentAnimation.exposedTransforms.Length; i++)
                            {
                                Transform child = null;
                                if (fromFrame.exposedTransforms.Length > i && childMap.TryGetValue(currentAnimation.exposedTransforms[i], out child))
                                {
                                    if (needsInterp)
                                    {
                                        Matrix4x4 c = MatrixLerp(fromFrame.exposedTransforms[i], targetFrame.exposedTransforms[i], blendDelta);
                                        MatrixUtils.FromMatrix4x4(child, c);
                                    }
                                    else
                                    {
                                        MatrixUtils.FromMatrix4x4(child, fromFrame.exposedTransforms[i]);
                                    }
                                }
                            }
                        }
                        // apply root motion
                        if (applyRootMotion)
                        {
                            if (previousFrame > currentFrame)
                            {
                                // animation looped around, apply motion for skipped frames at the end of the animation
                                for (int i = previousFrame + 1; i < currentAnimation.frames.Length; i++)
                                {
                                    MeshFrameData rootFrame = currentAnimation.GetNearestFrame(i);
                                    transform.Translate(rootFrame.rootMotionPosition, Space.Self);
                                    transform.Rotate(rootFrame.rootMotionRotation.eulerAngles * Time.deltaTime, Space.Self);
                                }
                                // now apply motion from first frame to current frame
                                for (int i = 0; i <= currentFrame; i++)
                                {
                                    MeshFrameData rootFrame = currentAnimation.GetNearestFrame(i);
                                    transform.Translate(rootFrame.rootMotionPosition, Space.Self);
                                    transform.Rotate(rootFrame.rootMotionRotation.eulerAngles * Time.deltaTime, Space.Self);
                                }
                            }
                            else
                            {
                                for (int i = previousFrame + 1; i <= currentFrame; i++)
                                {
                                    MeshFrameData rootFrame = currentAnimation.GetNearestFrame(i);
                                    transform.Translate(rootFrame.rootMotionPosition, Space.Self);
                                    transform.Rotate(rootFrame.rootMotionRotation.eulerAngles * Time.deltaTime, Space.Self);
                                }
                            }

                        }
                    }
                }
            }
            OnFrameUpdated();

            FireAnimationEvents(currentAnimation, totalSpeed, finished);
        }

        /// <summary>
        /// Crossfade an animation by index
        /// </summary>
        /// <param name="index">Index of the animation</param>
        public void Crossfade(int index)
        {
            Crossfade(index, 0.1f);
        }

        /// <summary>
        /// Crossfade an animation by name
        /// </summary>
        /// <param name="animationName">Name of the animation</param>
        public void Crossfade(string animationName)
        {
            Crossfade(animationName, 0.1f);
        }

        /// <summary>
        /// Crossfade an animation by index
        /// </summary>
        /// <param name="index">Index of the animation</param>
        /// <param name="speed">Duration the crossfade will take</param>
        public void Crossfade(int index, float speed)
        {
            if (index < 0 || animations.Length <= index || currentAnimIndex == index)
                return;
            currentCrossFade.Reset();
            currentCrossFade.framesNeeded = (int)(speed * FPS);
            currentCrossFade.isFading = true;
            currentCrossFade.endTime = Time.time + speed;
            if (currentAnimation == null)
            {
                currentCrossFade.fromFrame = defaultAnimation.GetNearestFrame(0);
            }
            else
            {
                currentCrossFade.fromFrame = currentAnimation.GetNearestFrame(currentFrame);
            }
            Play(index);
            currentCrossFade.toFrame = currentAnimation.GetNearestFrame(0);
            EnqueueAnimatorForCrossfade(this);
        }

        /// <summary>
        /// Crossfade an animation by name
        /// </summary>
        /// <param name="animationName">Name of the animation</param>
        /// <param name="speed">Duration the crossfade will take</param>
        public void Crossfade(string animationName, float speed)
        {
            for (int i = 0; i < animations.Length; i++)
            {
                if (animations[i].IsName(animationName))
                {
                    Crossfade(i, speed);
                    break;
                }
            }
        }

        /// <summary>
        /// Play the default animation, or resume playing a paused animator
        /// </summary>
        public void Play()
        {
            isPaused = false;
        }

        /// <summary>
        /// Play an animation by name
        /// </summary>
        /// <param name="animationName">Name of the animation</param>
        public void Play(string animationName)
        {
            for (int i = 0; i < animations.Length; i++)
            {
                if (animations[i].IsName(animationName))
                {
                    Play(i);
                    break;
                }
            }
        }

        /// <summary>
        /// Play an animation by index
        /// </summary>
        /// <param name="index">Index of the animation</param>
        public void Play(int index)
        {
            if (index < 0 || animations.Length <= index || currentAnimIndex == index)
                return;
            if (queuedAnims != null)
                queuedAnims.Clear();
            currentAnimIndex = index;
            currentAnimation = animations[currentAnimIndex];
            currentFrame = 0;
            currentAnimTime = 0;
            previousEventFrame = -1;
            pingPong = false;
            isPaused = false;
            nextTick = Time.time;
        }

        /// <summary>
        /// Play a random animation
        /// </summary>
        /// <param name="animationNames">Animation names</param>
        public void PlayRandom(params string[] animationNames)
        {
            int rand = UnityEngine.Random.Range(0, animationNames.Length);
            string randomAnim = animationNames[rand];
            for (int i = 0; i < animations.Length; i++)
            {
                if (animations[i].IsName(randomAnim))
                {
                    Play(i);
                    break;
                }
            }
        }

        /// <summary>
        /// Play an animation after the previous one has finished
        /// </summary>
        /// <param name="animationName">Animation name</param>
        public void PlayQueued(string animationName)
        {
            if (queuedAnims == null)
                queuedAnims = new Queue<string>();
            queuedAnims.Enqueue(animationName);
        }

        /// <summary>
        /// Pause an animator, disabling the component also has the same effect
        /// </summary>
        public void Pause()
        {
            isPaused = true;
        }

        /// <summary>
        /// Restart the current animation from the beginning
        /// </summary>
        public void RestartAnim()
        {
            currentFrame = 0;
        }

        /// <summary>
        /// Sets the current time of the playing animation
        /// </summary>
        /// <param name="time">Time of the animation to play. Min: 0, Max: Length of animation</param>
        public void SetTime(float time, bool instantUpdate = false)
        {
            var cAnim = currentAnimation;
            if (cAnim == null)
                return;
            time = Mathf.Clamp(time, 0, cAnim.length);
            currentAnimTime = time;
            if (instantUpdate)
                UpdateTick(Time.time);
        }

        /// <summary>
        /// Set the current time of the animation, normalized
        /// </summary>
        /// <param name="time">Time of the animation to start playback (0-1)</param>
        public void SetTimeNormalized(float time, bool instantUpdate = false)
        {
            var cAnim = currentAnimation;
            if (cAnim == null)
                return;
            time = Mathf.Clamp01(time);
            currentAnimTime = time * cAnim.length;
            if (instantUpdate)
                UpdateTick(Time.time);
        }

        /// <summary>
        /// Get the MeshAnimation by name
        /// </summary>
        /// <param name="animationName">Name of the animation</param>
        /// <returns>MeshAnimation class</returns>
        public MeshAnimation GetClip(string animationName)
        {
            for (int i = 0; i < animations.Length; i++)
            {
                if (animations[i].IsName(animationName))
                {
                    return animations[i];
                }
            }
            return null;
        }

        /// <summary>
        /// Populates the crossfade pool with the set amount of meshes
        /// </summary>
        /// <param name="count">Amount to fill the pool with</param>
        public void PrepopulateCrossfadePool(int count)
        {
            Stack<Mesh> pool = null;
            lock (s_crossFadePool)
            {
                if (crossFadePoolIndex > -1)
                {
                    pool = s_crossFadePool[crossFadePoolIndex];
                    count = pool.Count - count;
                    if (count <= 0)
                        return;
                }
            }
            Mesh[] meshes = AllocatedArray<Mesh>.Get(count);
            for (int i = 0; i < count; i++)
            {
                meshes[i] = GetCrossfadeFromPool();
            }
            for (int i = 0; i < count; i++)
            {
                crossFadeMesh = meshes[i];
                ReturnCrossfadeToPool();
                meshes[i] = null;
            }
            AllocatedArray<Mesh>.Return(meshes);
        }
        #endregion
    }
}