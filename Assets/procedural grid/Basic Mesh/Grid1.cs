using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Grid1 : MonoBehaviour
{
    public int xSize, ySize;
    private Vector3[] vertices;
    private Mesh mesh;
    void Awake()
    {
        StartCoroutine(Generate());
    }

    private IEnumerator Generate()
    {
        GetComponent<MeshFilter>().mesh = mesh = new Mesh();
        mesh.name = "Procedural Grid";
        //array of 3D vectors to store the points 
        vertices = new Vector3[(xSize + 1) * (ySize + 1)];

        /*
            coordinates from 0 to 1 
            tells the graphics pipeline how to "wrap" a 2D image (texture) onto a 3D surface
        */
        Vector2[] uv = new Vector2[vertices.Length];
        /* 
            change tiling:
            Tiling = (2,2)
            - Unity will feed the shader UV' = UV * (2,2)
            - vertext was at (0.5,0.5) becomes (1.0, 1.0)
            - Any UVs now lie in the [0 -> 2] range; the image will render twice in both U and V.
            - Mesh UVs stays the same, each vertext still have its original UV (0,0) -> (1,1)
        */


        Vector4[] tangents = new Vector4[vertices.Length];
        /* 
            Need when apply a normal Map 
            - A normal map is stored in UV space.
            The XYZ componenets define the tangent direction 
            The W component is a -+1 "handedness" flag used to reconstruct the bitangent
        
        */  
        for (int i = 0, y = 0; y <= ySize; y++)
        {
            for (int x = 0; x <= xSize; x++, i++)
            {
                vertices[i] = new Vector3(x, y);
                //allocates which coordinates of the UV map is vertices binds to 
                uv[i] = new Vector2((float)x / xSize, (float)y / ySize);
                tangents[i] = new Vector4(1f, 0f, 0f, -1f);
            }
        }
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.tangents = tangents;

        /*
        triangles are defined via an array of vertex indices (int)
        three concsecutive indices describe one triangle
        the orientation of triangle is clock wise
        */
        int[] triangles = new int[xSize * ySize * 2 * 3];

        for (int ti = 0, vi = 0, y = 0; y < ySize; y++, vi++)
        {
            for (int x = 0; x < xSize; x++, ti += 6, vi++)
            {
                triangles[ti] = vi;
                triangles[ti + 3] = triangles[ti + 2] = vi + 1;
                triangles[ti + 4] = triangles[ti + 1] = vi + xSize + 1;
                triangles[ti + 5] = vi + xSize + 2;
            }
        }
        mesh.triangles = triangles;

        /*
            Normals are defined per vertex. 
            - Each normal points directly "outward" from the mesh's surface
            - can have autogernerated or a vector array
            1. Used to tell shader which way the surface is facing 
            2. Used for all standard light models 
            Unity sampls mesh.normals to calculate diffuse/specular light
        */
        mesh.RecalculateNormals();

        yield return null;
    }

    private void OnDrawGizmos()
    {
        if (vertices == null)
        {
            return;
        }

        Gizmos.color = Color.black;
        for (int i = 0; i < vertices.Length; i++)
        {
            Gizmos.DrawSphere(vertices[i], 0.1f);
        }
    }
}
