using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Collections;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using System.IO;
#endif

public class AnimationClipTextureBaker : MonoBehaviour
{
    public ComputeShader infoTexGen;
    public Shader playShader;
    public AnimationClip[] clipsToBake;
    [SerializeField] private bool ApplyRootMotion = false;
    public struct VertInfo
    {
        public Vector3 position;
        public Vector3 normal;
        public Vector3 tangent;
    }
    private void OnValidate()
    {
        if (clipsToBake == null || clipsToBake.Length == 0)
        {
            Reset();
        }
    }

    private void Reset()
    {
        var animation = GetComponent<Animation>();
        var animator = GetComponent<Animator>();

        if (animation != null)
        {
            clipsToBake = new AnimationClip[animation.GetClipCount()];
            var i = 0;
            foreach (AnimationState state in animation)
            {
                clipsToBake[i++] = state.clip;
            }
        }
        else if (animator != null)
        {
            clipsToBake = animator.runtimeAnimatorController.animationClips;
        }
    }

    [ContextMenu("bake texture")]
    void Bake()
    {

        Animator animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animatorRootMeshFix();
            return;
        }
        if (!Application.isPlaying)
        {
            Debug.LogWarning(" Works only in  Play Mode");
            return;
        }
        var skin = GetComponentInChildren<SkinnedMeshRenderer>();
        var vCount = skin.sharedMesh.vertexCount;
        var texWidth = Mathf.NextPowerOfTwo(vCount);
        var mesh = new Mesh();
        Vector3 originalPosition = gameObject.transform.position;

        Vector3 positionAnimator = Vector3.zero;
        foreach (var clip in clipsToBake)
        {
            var frames = Mathf.NextPowerOfTwo((int)(clip.length / 0.05f));
            var dt = clip.length / frames;
            var infoList = new List<VertInfo>();

            var pRt = new RenderTexture(texWidth, frames, 0, RenderTextureFormat.ARGBHalf);
            pRt.name = string.Format("{0}.{1}.posTex", name, clip.name);
            var nRt = new RenderTexture(texWidth, frames, 0, RenderTextureFormat.ARGBHalf);
            nRt.name = string.Format("{0}.{1}.normTex", name, clip.name);
            var tRt = new RenderTexture(texWidth, frames, 0, RenderTextureFormat.ARGBHalf);
            tRt.name = string.Format("{0}.{1}.tanTex", name, clip.name);
            foreach (var rt in new[] { pRt, nRt, tRt })
            {
                rt.enableRandomWrite = true;
                rt.Create();
                RenderTexture.active = rt;
                GL.Clear(true, true, Color.clear);
            }

            for (var i = 0; i < frames; i++)
            {
                clip.SampleAnimation(gameObject, dt * i);
                skin.BakeMesh(mesh);

                infoList.AddRange(
                    Enumerable.Range(0, vCount)
                    .Select(idx => new VertInfo()
                    {
                        position = mesh.vertices[idx],
                        normal = mesh.normals[idx],
                        tangent = mesh.tangents[idx],
                    })
                );
            }

            var buffer = new ComputeBuffer(infoList.Count, System.Runtime.InteropServices.Marshal.SizeOf(typeof(VertInfo)));
            buffer.SetData(infoList.ToArray());

            var kernel = infoTexGen.FindKernel("CSMain");
            uint x, y, z;
            infoTexGen.GetKernelThreadGroupSizes(kernel, out x, out y, out z);
            infoTexGen.SetInt("VertCount", vCount);
            infoTexGen.SetBuffer(kernel, "Info", buffer);
            infoTexGen.SetTexture(kernel, "OutPosition", pRt);
            infoTexGen.SetTexture(kernel, "OutNormal", nRt);
            infoTexGen.SetTexture(kernel, "OutTangent", tRt);
            infoTexGen.Dispatch(kernel, vCount / (int)x + 1, frames / (int)y + 1, 1);
            buffer.Release();

#if UNITY_EDITOR
            var folderName = "BakedAnimationTex";
            var folderPath = Path.Combine("Assets", folderName);
            if (!AssetDatabase.IsValidFolder(folderPath))
                AssetDatabase.CreateFolder("Assets", folderName);

            var subFolder = name;
            var subFolderPath = Path.Combine(folderPath, subFolder);
            if (!AssetDatabase.IsValidFolder(subFolderPath))
                AssetDatabase.CreateFolder(folderPath, subFolder);

            var posTex = RenderTextureToTexture2D.Convert(pRt);
            var normTex = RenderTextureToTexture2D.Convert(nRt);
            var tanTex = RenderTextureToTexture2D.Convert(tRt);
            Graphics.CopyTexture(pRt, posTex);
            Graphics.CopyTexture(nRt, normTex);
            Graphics.CopyTexture(tRt, tanTex);

            var mat = new Material(playShader);
            mat.SetTexture("_MainTex", skin.sharedMaterial.mainTexture);
            mat.SetTexture("_PosTex", posTex);
            mat.SetTexture("_NmlTex", normTex);
            mat.SetFloat("_Length", clip.length);
            if (clip.wrapMode == WrapMode.Loop)
            {
                mat.SetFloat("_Loop", 1f);
                mat.EnableKeyword("ANIM_LOOP");
            }

            var go = new GameObject(name + "." + clip.name);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.AddComponent<MeshFilter>().sharedMesh = skin.sharedMesh;

            AssetDatabase.CreateAsset(posTex, Path.Combine(subFolderPath, pRt.name + ".asset"));
            AssetDatabase.CreateAsset(normTex, Path.Combine(subFolderPath, nRt.name + ".asset"));
            AssetDatabase.CreateAsset(tanTex, Path.Combine(subFolderPath, tRt.name + ".asset"));
            AssetDatabase.CreateAsset(mat, Path.Combine(subFolderPath, string.Format("{0}.{1}.animTex.asset", name, clip.name)));
            PrefabUtility.SaveAsPrefabAsset(go, Path.Combine(folderPath, go.name + ".prefab").Replace("\\", "/"));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
#endif
        }
    }

    private void animatorRootMeshFix()
    {
#if UNITY_EDITOR

        var sampleGO = Instantiate(this.gameObject, Vector3.zero, Quaternion.identity) as GameObject;
        Animator animator = sampleGO.GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.applyRootMotion = ApplyRootMotion;
        }
        var skin = sampleGO.GetComponentInChildren<SkinnedMeshRenderer>();
        var vCount = skin.sharedMesh.vertexCount;
        var texWidth = Mathf.NextPowerOfTwo(vCount);

        var dictOnStateAndClips = FillDictionaryOfClipsAndStates(animator);

        foreach (var keyValue in dictOnStateAndClips)
        {
            var clip = keyValue.Value;
            var stateName = keyValue.Key;
            bool DoNeedToProcess = false;
            foreach (var item in clipsToBake)
            {
                DoNeedToProcess |= item.name == clip.name;
            }
            if (!DoNeedToProcess)
            {
                continue;
            }
            var frames = Mathf.NextPowerOfTwo((int)(clip.length / 0.05f));
            var infoList = new List<VertInfo>();

            var pRt = new RenderTexture(texWidth, frames, 0, RenderTextureFormat.ARGBHalf);
            pRt.name = string.Format("{0}.{1}.posTex", name, clip.name);
            var nRt = new RenderTexture(texWidth, frames, 0, RenderTextureFormat.ARGBHalf);
            nRt.name = string.Format("{0}.{1}.normTex", name, clip.name);
            var tRt = new RenderTexture(texWidth, frames, 0, RenderTextureFormat.ARGBHalf);
            tRt.name = string.Format("{0}.{1}.tanTex", name, clip.name);
            foreach (var rt in new[] { pRt, nRt, tRt })
            {
                rt.enableRandomWrite = true;
                rt.Create();
                RenderTexture.active = rt;
                GL.Clear(true, true, Color.clear);
            }

            int bakeFrames = Mathf.CeilToInt(frames);
            var dt = clip.length / frames;
            animator.Play(stateName, 0, 0);
            for (int i = 0; i < frames; i++)
            {
                float bakeDelta = Mathf.Clamp01(((float)i / frames));
                EditorUtility.DisplayProgressBar("Baking Animation", string.Format("Processing: {0} Frame: {1}", stateName, i), bakeDelta);
                float animationTime = bakeDelta * clip.length;
                animator.Update(dt);
                Mesh m = new Mesh();
                skin.BakeMesh(m);
                infoList.AddRange(
                    Enumerable.Range(0, vCount)
                        .Select(idx => new VertInfo()
                        {
                            position = m.vertices[idx],
                            normal = m.normals[idx],
                            tangent = m.tangents[idx],
                        })
                );
                DestroyImmediate(m);
                // debug only
                // Instantiate(sampleGO, i * Vector3.right, Quaternion.identity);
            }


            var buffer = new ComputeBuffer(infoList.Count, System.Runtime.InteropServices.Marshal.SizeOf(typeof(VertInfo)));
            buffer.SetData(infoList.ToArray());

            var kernel = infoTexGen.FindKernel("CSMain");
            uint x, y, z;
            infoTexGen.GetKernelThreadGroupSizes(kernel, out x, out y, out z);
            infoTexGen.SetInt("VertCount", vCount);
            infoTexGen.SetBuffer(kernel, "Info", buffer);
            infoTexGen.SetTexture(kernel, "OutPosition", pRt);
            infoTexGen.SetTexture(kernel, "OutNormal", nRt);
            infoTexGen.SetTexture(kernel, "OutTangent", tRt);
            infoTexGen.Dispatch(kernel, vCount / (int)x + 1, frames / (int)y + 1, 1);
            buffer.Release();

            var folderName = "BakedAnimationTex";
            var folderPath = Path.Combine("Assets", folderName);
            if (!AssetDatabase.IsValidFolder(folderPath))
                AssetDatabase.CreateFolder("Assets", folderName);

            var subFolder = name;
            var subFolderPath = Path.Combine(folderPath, subFolder);
            if (!AssetDatabase.IsValidFolder(subFolderPath))
                AssetDatabase.CreateFolder(folderPath, subFolder);

            var posTex = RenderTextureToTexture2D.Convert(pRt);
            var normTex = RenderTextureToTexture2D.Convert(nRt);
            var tanTex = RenderTextureToTexture2D.Convert(tRt);
            Graphics.CopyTexture(pRt, posTex);
            Graphics.CopyTexture(nRt, normTex);
            Graphics.CopyTexture(tRt, tanTex);

            var mat = new Material(playShader);
            mat.SetTexture("_MainTex", skin.sharedMaterial.mainTexture);
            mat.SetTexture("_PosTex", posTex);
            mat.SetTexture("_NmlTex", normTex);
            mat.SetFloat("_Length", clip.length);
            if (clip.wrapMode == WrapMode.Loop)
            {
                mat.SetFloat("_Loop", 1f);
                mat.EnableKeyword("ANIM_LOOP");
            }

            var go = new GameObject(name + "." + clip.name);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.AddComponent<MeshFilter>().sharedMesh = skin.sharedMesh;

            AssetDatabase.CreateAsset(posTex, Path.Combine(subFolderPath, pRt.name + ".asset"));
            AssetDatabase.CreateAsset(normTex, Path.Combine(subFolderPath, nRt.name + ".asset"));
            AssetDatabase.CreateAsset(tanTex, Path.Combine(subFolderPath, tRt.name + ".asset"));
            AssetDatabase.CreateAsset(mat, Path.Combine(subFolderPath, string.Format("{0}.{1}.animTex.asset", name, clip.name)));
            PrefabUtility.SaveAsPrefabAsset(go, Path.Combine(folderPath, go.name + ".prefab").Replace("\\", "/"));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.ClearProgressBar();
        }
        GameObject.DestroyImmediate(sampleGO);
#endif
    }
#if UNITY_EDITOR

    private static Dictionary<string, AnimationClip> FillDictionaryOfClipsAndStates(Animator animator)
    {
        var clips = animator.runtimeAnimatorController.animationClips;
        Dictionary<string, AnimationClip> AnimStateNames = new Dictionary<string, AnimationClip>(); // state to name of animation
        foreach (var item in clips)
        {
            AnimStateNames.Add(item.name, item);
        }

        var ac = animator.runtimeAnimatorController as AnimatorController;
        var acLayers = ac.layers;
        AnimatorStateMachine stateMachine;
        ChildAnimatorState[] ch_animStates;
        Dictionary<string, AnimationClip> AnimStateNamesAndCLip = new Dictionary<string, AnimationClip>(); // state to name of animation

        foreach (AnimatorControllerLayer i in acLayers) //for each layer
        {
            stateMachine = i.stateMachine;
            ch_animStates = null;
            ch_animStates = stateMachine.states;
            foreach (ChildAnimatorState j in ch_animStates) //for each state
            {
                AnimStateNamesAndCLip.Add(j.state.name, AnimStateNames[j.state.motion.name]);
            }
        }

        return AnimStateNames;
    }
#endif

}