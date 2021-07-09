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
    private VertData[] vertices;
    public int[] triangles;

    public int VertCount {
        get { return vertices.Length; }
    }

    public VertData GetVertData(int index) {
        return vertices[index];
    }

}


[System.Serializable]
public class VertData {

    public Vector3 position;
    public Vector2 uv;

    public VertData(Vector3 pos, Vector2 uv) {

        this.position = pos;
        this.uv = uv;
        
    }

    public Vector3 GetRotatedPos(Vector3 angles) {

        Vector3 centre = new Vector3(0.5f, 0.5f, 0.5f);  //center of rotation
        Vector3 direction = position - centre;
        direction = Quaternion.Euler(angles) * direction;
        return direction + centre;
        
    }
}
