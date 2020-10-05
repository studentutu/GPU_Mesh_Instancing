using System.Collections.Generic;
using UnityEngine;

namespace PrefabLightMapBaker
{
    public static class RuntimeBakedLightmapUtils
    {
        private class LightMapPrefabStorage
        {
            public int referenceCount = 0;
            public List<LightmapData> lightData = null;
            public List<int> HashCodes = new List<int>(10);
        }

        private class LightmapWithIndex
        {
            public LightmapData lightData = null;
            public int index = -1;
        }
        private struct ActionStruct
        {
            public PrefabBaker prefab;
            public bool AddOrRemove;
        }
        private const int INITIAL_RESERVE_NUMBER_LIGHTMAPS = 10;
        private const int INITIAL_RESERVE_NUMBER_INSTANCES = 10;

        public static bool IsLightMapsChanged { get; set; } = false;
        private static readonly List<LightmapData> added_lightmaps = new List<LightmapData>(INITIAL_RESERVE_NUMBER_LIGHTMAPS);
        private static readonly List<LightmapWithIndex> changed_lightmaps = new List<LightmapWithIndex>(INITIAL_RESERVE_NUMBER_LIGHTMAPS);
        private static readonly List<LightmapData> sceneLightData = new List<LightmapData>(INITIAL_RESERVE_NUMBER_LIGHTMAPS);
        private static readonly Dictionary<int, bool> emptySlots = new Dictionary<int, bool>(INITIAL_RESERVE_NUMBER_LIGHTMAPS);
        private static readonly Dictionary<string, LightMapPrefabStorage> prefabToLightmap = new Dictionary<string, LightMapPrefabStorage>();
        private static readonly List<ActionStruct> actionsToPerform = new List<ActionStruct>(INITIAL_RESERVE_NUMBER_INSTANCES);
        private static Dictionary<PrefabBaker.LightMapType, System.Func<Texture2D[], bool>> switchCase = null;

        private static Dictionary<PrefabBaker.LightMapType, System.Func<Texture2D[], bool>> SwitchCase
        {
            get
            {
                if (switchCase == null)
                {
                    switchCase = new Dictionary<PrefabBaker.LightMapType, System.Func<Texture2D[], bool>>()
                    {
                        {PrefabBaker.LightMapType.LightColor, IsInLightColor},
                        {PrefabBaker.LightMapType.LightDir, IsInLightDir},
                        {PrefabBaker.LightMapType.LightShadow, IsInLightShadows},
                    };
                }
                return switchCase;
            }
        }

        private static List<LightmapData> CurrentFrameLightData
        {
            get
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    ClearAndAddUnityLightMaps();
                    return sceneLightData;
                }
#endif
                return sceneLightData;
            }
        }

        public static void UpdateUnityLightMaps()
        {
            IsLightMapsChanged = false;
            LightmapSettings.lightmaps = CurrentFrameLightData.ToArray();
            foreach (var item in actionsToPerform)
            {
                if (item.prefab != null)
                {
                    item.prefab.ReleaseShaders();
                }
            }
            actionsToPerform.Clear();
        }

        public static void ClearAndAddUnityLightMaps(bool copyLightMaps = true)
        {
            IsLightMapsChanged = false;
            sceneLightData.Clear();
            actionsToPerform.Clear();
            if (copyLightMaps)
            {
                sceneLightData.AddRange(LightmapSettings.lightmaps);
            }
        }

        public static void AddInstanceRef(PrefabBaker prefab)
        {
            var hashCode = prefab.GetLightMapHashCode();
            if (!prefabToLightmap.TryGetValue(hashCode, out _))
            {
                if (!prefab.BakeApplied)
                {
                    int max = Mathf.Max(prefab.texturesColor.Length, prefab.texturesDir.Length);
                    max = Mathf.Max(max, prefab.texturesShadow.Length);
                    for (int i = 0; i < max; i++)
                    {
                        var newLightmapData = new LightmapData
                        {
                            lightmapColor = GetElement(prefab.texturesColor, i),
                            lightmapDir = GetElement(prefab.texturesDir, i),
                            shadowMask = GetElement(prefab.texturesShadow, i)
                        };
                        JoinOn(prefab, newLightmapData);
                    }
                }
            }
            prefabToLightmap[hashCode].referenceCount++;
        }

        public static void RemoveInstanceRef(PrefabBaker prefab)
        {
            var hashCode = prefab.GetLightMapHashCode();
            if (prefabToLightmap.TryGetValue(hashCode, out LightMapPrefabStorage storage))
            {
                storage.referenceCount--;
                if (storage.referenceCount <= 0)
                {
                    storage.referenceCount = 0;
                }
            }
        }

        public static bool CheckInstance(PrefabBaker prefab)
        {
            bool fullyCleaned = false;
            var hashCode = prefab.GetLightMapHashCode();
            if (prefabToLightmap.TryGetValue(hashCode, out LightMapPrefabStorage storage))
            {
                if (storage.referenceCount <= 0)
                {
                    storage.referenceCount = 0;
                    fullyCleaned = true;
                    RemoveEmpty(prefab, storage);
                    prefabToLightmap.Remove(hashCode);
                }
            }
            return fullyCleaned;
        }
        public static bool CheckInstance(string hashCode)
        {
            bool fullyCleaned = false;
            if (prefabToLightmap.TryGetValue(hashCode, out LightMapPrefabStorage storage))
            {
                if (storage.referenceCount <= 0)
                {
                    storage.referenceCount = 0;
                    fullyCleaned = true;
                    RemoveEmpty(null, storage);
                    prefabToLightmap.Remove(hashCode);
                }
            }
            return fullyCleaned;
        }

        private static int GetHashCodeCustom(LightmapData objectToGetCode)
        {
            int result = 0;
            result += objectToGetCode.lightmapColor == null ? 0 : objectToGetCode.lightmapColor.GetInstanceID();
            result += objectToGetCode.lightmapDir == null ? 0 : objectToGetCode.lightmapDir.GetInstanceID();
            result += objectToGetCode.shadowMask == null ? 0 : objectToGetCode.shadowMask.GetInstanceID();
            return result;
        }

        private static void RemoveEmpty(PrefabBaker prefab, LightMapPrefabStorage toRemoveData)
        {
            var sceneLightmaps = CurrentFrameLightData;
            int count = sceneLightmaps.Count;
            for (int j = 0; j < count; j++)
            {
                int hash = GetHashCodeCustom(sceneLightmaps[j]);
                foreach (var item in toRemoveData.lightData)
                {
                    if (hash == GetHashCodeCustom(item))
                    {
                        sceneLightmaps[j] = new LightmapData();
                    }
                }
            }
            if (prefab != null)
            {
                actionsToPerform.Add(new ActionStruct { prefab = prefab, AddOrRemove = false });
            }
            IsLightMapsChanged = true;
        }

        public static bool InitializeInstance(PrefabBaker prefab)
        {
            if (prefab.renderers == null || prefab.renderers.Length == 0) return false;

            var sceneLightmapsRef = CurrentFrameLightData;
            added_lightmaps.Clear();
            changed_lightmaps.Clear();

            int max = Mathf.Max(prefab.texturesColor.Length, prefab.texturesDir.Length);
            max = Mathf.Max(max, prefab.texturesShadow.Length);

            int[] lightmapArrayOffsetIndex = new int[max];
            emptySlots.Clear();
            int count = CurrentFrameLightData.Count;
            for (int i = 0; i < max; i++)
            {
                bool found = false;
                for (int j = 0; j < count && !found; j++)
                {
                    if (sceneLightmapsRef[j].lightmapColor == null &&
                        sceneLightmapsRef[j].lightmapDir == null &&
                        sceneLightmapsRef[j].shadowMask == null)
                    {
                        if (!emptySlots.ContainsKey(j))
                        {
                            emptySlots.Add(j, true);
                        }
                        continue;
                    }
                    found |= prefab.texturesColor.Length > i && prefab.texturesColor[i] != null && prefab.texturesColor[i] == sceneLightmapsRef[j].lightmapColor;
                    found |= prefab.texturesDir.Length > i && prefab.texturesDir[i] != null && prefab.texturesDir[i] == sceneLightmapsRef[j].lightmapDir;
                    found |= prefab.texturesShadow.Length > i && prefab.texturesShadow[i] != null && prefab.texturesShadow[i] == sceneLightmapsRef[j].shadowMask;

                    if (found)
                    {
                        lightmapArrayOffsetIndex[i] = j;
                        break;
                    }
                }

                if (!found)
                {
                    var newLightmapData = new LightmapData
                    {
                        lightmapColor = GetElement(prefab.texturesColor, i),
                        lightmapDir = GetElement(prefab.texturesDir, i),
                        shadowMask = GetElement(prefab.texturesShadow, i)
                    };

                    if (emptySlots.Keys.Count > 0)
                    {
                        int indexToGet = -1;
                        foreach (var item in emptySlots.Keys)
                        {
                            indexToGet = item;
                            break;
                        }
                        lightmapArrayOffsetIndex[i] = indexToGet;
                        changed_lightmaps.Add(new LightmapWithIndex
                        {
                            lightData = newLightmapData,
                            index = lightmapArrayOffsetIndex[i]
                        });
                    }
                    else
                    {
                        lightmapArrayOffsetIndex[i] = added_lightmaps.Count + count;
                        added_lightmaps.Add(newLightmapData);
                    }
                    JoinOn(prefab, newLightmapData);
                }
            }

            bool combined = false;
            if (added_lightmaps.Count > 0 || changed_lightmaps.Count > 0)
            {
                IsLightMapsChanged = true;
                CombineLightmaps(added_lightmaps, changed_lightmaps);
                combined = true;
            }

            // Required for each instance once!
            UpdateLightmaps(prefab, lightmapArrayOffsetIndex);
            return combined;
        }

        private static void JoinOn(PrefabBaker prefab, LightmapData newData)
        {
            var hashCode = prefab.GetLightMapHashCode();
            if (string.IsNullOrEmpty(hashCode))
            {
                return;
            }
            if (!prefabToLightmap.TryGetValue(hashCode, out LightMapPrefabStorage storage))
            {
                storage = new LightMapPrefabStorage
                {
                    lightData = new List<LightmapData>(4)
                };
            }
            bool addedNew = true;
            var hashCodeOfLightData = GetHashCodeCustom(newData);
            foreach (var item in storage.HashCodes)
            {
                if (item == hashCodeOfLightData)
                {
                    addedNew = false;
                    break;
                }
            }
            if (addedNew)
            {
                storage.lightData.Add(newData);
                storage.HashCodes.Add(hashCodeOfLightData);
                prefabToLightmap[hashCode] = storage;
            }
        }

        private static T GetElement<T>(T[] array, int index)
        {
            if (array == null) return default;
            if (array.Length < index + 1) return default;
            return array[index];
        }

        private static void CombineLightmaps(List<LightmapData> lightmaps, List<LightmapWithIndex> changed)
        {
            var original = CurrentFrameLightData;
            foreach (var item in changed)
            {
                original[item.index] = item.lightData;
            }

            for (int i = 0; i < lightmaps.Count; i++)
            {
                original.Add(lightmaps[i]);
            }

            // LightmapSettings.lightmaps = combined; // manager should add at the end of frame
        }

        // required on each instance
        private static void UpdateLightmaps(PrefabBaker prefab, int[] lightmapOffsetIndex)
        {
            for (var i = 0; i < prefab.renderers.Length; ++i)
            {
                var renderer = prefab.renderers[i];
                var lightIndex = prefab.renderersLightmapIndex[i];
                var lightScale = prefab.renderersLightmapOffsetScale[i];

                renderer.lightmapIndex = lightmapOffsetIndex[lightIndex];
                renderer.lightmapScaleOffset = lightScale;
            }

            actionsToPerform.Add(new ActionStruct { prefab = prefab, AddOrRemove = true });


            ChangeLightBaking(prefab.lights);
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UpdateUnityLightMaps();
            }
#endif
        }

        private static void ChangeLightBaking(LightInfo[] lightsInfo)
        {
            foreach (var info in lightsInfo)
            {
                info.light.bakingOutput = new LightBakingOutput
                {
                    isBaked = true,
                    mixedLightingMode = (MixedLightingMode)info.mixedLightingMode,
                    lightmapBakeType = (LightmapBakeType)info.lightmapBaketype
                };
            }
        }

        public static bool SceneHasAllLightmaps(Texture2D[] textures, PrefabBaker.LightMapType typeLight)
        {
            if ((textures?.Length ?? 0) < 1) return true;

            else if (CurrentFrameLightData.Count < 1) return false;

            return SwitchCase[typeLight](textures);
        }

        private static bool IsInLightColor(Texture2D[] textures)
        {
            bool found;
            foreach (var lmd in CurrentFrameLightData)
            {
                found = false;
                for (int i = 0; i < textures.Length && !found; i++)
                {
                    found |= textures[i] == lmd.lightmapColor;
                }
                if (!found) return false;
            }
            return true;
        }

        private static bool IsInLightDir(Texture2D[] textures)
        {
            bool found;
            foreach (var lmd in CurrentFrameLightData)
            {
                found = false;
                for (int i = 0; i < textures.Length && !found; i++)
                {
                    found |= textures[i] == lmd.lightmapDir;
                }
                if (!found) return false;
            }
            return true;
        }

        private static bool IsInLightShadows(Texture2D[] textures)
        {
            bool found;
            foreach (var lmd in CurrentFrameLightData)
            {
                found = false;
                for (int i = 0; i < textures.Length && !found; i++)
                {
                    found |= textures[i] == lmd.shadowMask;
                }
                if (!found) return false;
            }
            return true;
        }
    }
}