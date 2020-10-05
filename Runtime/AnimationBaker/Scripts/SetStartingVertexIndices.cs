using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// [ExecuteAlways]
public class SetStartingVertexIndices : MonoBehaviour
{
    // public static HashSet<int> sharedMeshOnce = new HashSet<int>();
    [SerializeField] private MeshFilter m_renderer = null;

    private void OnValidate()
    {
        if (m_renderer == null)
        {
            m_renderer = GetComponent<MeshFilter>();
            SetUV2();
        }
    }

    // Start is called before the first frame update
    private void Start()
    {
        SetUV2();
    }

    private void SetUV2()
    {
        if (m_renderer != null)
        {
            var mesh = m_renderer.sharedMesh;
            // if (sharedMeshOnce.Contains(mesh.GetHashCode()))
            // {
            //     return;
            // }
            if (mesh.uv2.Length == 0)
            {
                // Debug.LogWarning(" need to set for mesh " + mesh.name);
                // mesh.uv2 = m_renderer.s.ToArray();
                var listOfIndices = new Vector2[mesh.vertexCount];
                for (int i = 0; i < mesh.vertexCount; i++)
                {
                    listOfIndices[i] = new Vector2(i, 0);
                }
                mesh.uv2 = listOfIndices;
            }
        }
    }

}
