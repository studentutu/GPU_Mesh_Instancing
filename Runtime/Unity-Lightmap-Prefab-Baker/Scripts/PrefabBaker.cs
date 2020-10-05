using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PrefabLightMapBaker
{
    public class PrefabBaker : MonoBehaviour
    {
        public enum LightMapType
        {
            LightColor,
            LightDir,
            LightShadow
        }
        [SerializeField] public LightInfo[] lights;
        [SerializeField] public Renderer[] renderers;
        [SerializeField] public int[] renderersLightmapIndex;
        [SerializeField] public Vector4[] renderersLightmapOffsetScale;
        [SerializeField] public Texture2D[] texturesColor;
        [SerializeField] public Texture2D[] texturesDir;
        [SerializeField] public Texture2D[] texturesShadow;

        private string lightMapId = null;

        public string GetLightMapHashCode()
        {
            if (lightMapId == null)
            {
                lightMapId = UniqueHashCodeFromLightMaps();
            }
            return lightMapId;
        }

        private string UniqueHashCodeFromLightMaps()
        {
            StringBuilder sb = new StringBuilder(50);
            foreach (var item in texturesColor)
            {
                sb.Append(item.GetHashCode());
            }
            foreach (var item in texturesDir)
            {
                sb.Append(item.GetHashCode());
            }
            foreach (var item in texturesShadow)
            {
                sb.Append(item.GetHashCode());
            }
            return sb.ToString();
        }

        public Texture2D[][] AllTextures() => new Texture2D[][]
        {
            texturesColor, texturesDir, texturesShadow
        };

        public bool HasBakeData => (renderers?.Length ?? 0) > 0 && (
                                                            (texturesColor?.Length ?? 0) > 0 ||
                                                            (texturesDir?.Length ?? 0) > 0 ||
                                                            (texturesShadow?.Length ?? 0) > 0
                                                            );

        public bool BakeApplied
        {
            get
            {
                bool hasColors = RuntimeBakedLightmapUtils.SceneHasAllLightmaps(texturesColor, LightMapType.LightColor);
                bool hasDirs = RuntimeBakedLightmapUtils.SceneHasAllLightmaps(texturesDir, LightMapType.LightDir);
                bool hasShadows = RuntimeBakedLightmapUtils.SceneHasAllLightmaps(texturesShadow, LightMapType.LightShadow);

                return hasColors && hasDirs && hasShadows;
            }
        }

        private bool BakeJustApplied = false;
        public bool RefAdded { get; private set; } = false;
        public void BakeApply()
        {
            if (!HasBakeData)
            {
                BakeJustApplied = false;
                return;
            }

            if (!BakeApplied)
            {
                BakeJustApplied = RuntimeBakedLightmapUtils.InitializeInstance(this);
#if UNITY_EDITOR
                // if (BakeJustApplied) Debug.LogWarning("[PrefabBaker] Addeded prefab lightmap data to current scene " + name);
                if (!Application.isPlaying)
                {
                    RuntimeBakedLightmapUtils.UpdateUnityLightMaps();
                }
#endif
            }

            for (var i = 0; i < renderers.Length; ++i)
            {
                renderers[i].lightmapScaleOffset = renderersLightmapOffsetScale[i];
            }
        }

        private void OnEnable()
        {
            // ActionOnEnable(); // uncomment to use textures right away
            RuntimeBakedLightmapUtils.AddInstanceRef(this);

            PrefabBakerManager.AddInstance(this);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            // ActionOnDisable(); // uncomment to use textures right away
            RuntimeBakedLightmapUtils.RemoveInstanceRef(this);
            // RefAdded = false;

            PrefabBakerManager.RemoveInstance(this);
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        public void ReleaseShaders()
        {
            foreach (var item in renderers)
            {
                if (item != null)
                {
                    ReleaseMaterialShader(item.sharedMaterials);
                }
            }
        }

        // required once for each lightmap
        private static void ReleaseMaterialShader(Material[] materials)
        {
            Shader shader = null;
            foreach (var mat in materials)
            {
                if (mat == null) continue;
                shader = Shader.Find(mat.shader.name);
                if (shader == null) continue;
                mat.shader = shader;
            }
        }

        public void ActionOnEnable()
        {
            BakeApply();
            RefAdded = true;
        }

        public void ActionOnDisable()
        {
            if (RuntimeBakedLightmapUtils.CheckInstance(this))
            {
                BakeJustApplied = false;
                // Debug.LogWarning(" Removing Prefab lightMap " + name);
            }
            RefAdded = false;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            BakeApply();
        }

#if UNITY_EDITOR
        [ContextMenu("Update textures from Prefab")]
        public void UpdateFromPrefab()
        {
            var mainObjPath = UnityEditor.PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(this);
            var getPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<PrefabBaker>(mainObjPath);
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(getPrefab), this);
            lightMapId = UniqueHashCodeFromLightMaps();
        }
#endif
    }
}