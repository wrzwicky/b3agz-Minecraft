using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Nw Voxel Mesh Data", menuName = "Minecraft Tutorial/Voxel Mesh Data")]
public class VoxelMeshData : ScriptableObject {

    public string blockName;
    public FaceMeshData[] faces;  //only ever 6 faces (due to tricks); use normal winding order

}


[System.Serializable]
public class FaceMeshData {

    public string direction;  //name; for inspector
    public Vector3 normal;    //one normal for all 4 verts
    public VertData[] vertices;
    public int[] triangles;

}


[System.Serializable]
public class VertData {

    public Vector3 position;
    public Vector2 uv;

    public VertData(Vector3 pos, Vector2 uv) {

        this.position = pos;
        this.uv = uv;
        
    }
}
