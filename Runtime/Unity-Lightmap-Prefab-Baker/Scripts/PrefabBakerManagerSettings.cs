using UnityEngine;

namespace PrefabLightMapBaker
{
    [CreateAssetMenu(menuName = "Morpeh/Baker/" + nameof(PrefabBakerManagerSettings))]
    public class PrefabBakerManagerSettings : ScriptableObject
    {
        [Tooltip("Remember to put this asset into Resources/" + PrefabBakerManager.PATH_TO_RESOURCE)]
        public int NumberOfLightMapSetPassesForSingleFrame = 5;
    }
}