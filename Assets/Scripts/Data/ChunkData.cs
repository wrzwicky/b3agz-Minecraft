using UnityEngine;


[System.Serializable]
public class ChunkData {

    // Vector2Int not serializable :(
    int x;
    int y;

    // hidden; is too much data for inspector
    [HideInInspector]
    public VoxelState[,,] map = new VoxelState[VoxelData.ChunkWidth, VoxelData.ChunkHeight, VoxelData.ChunkWidth];

    public ChunkData(Vector2Int pos) {

        position = pos;

    }

    public ChunkData(int x, int y) {

        this.x = x;
        this.y = y;

    }

    public Vector2Int position {
        get { return new Vector2Int(x, y); }
        set { x = value.x; y = value.y; }
    }

    /// create new voxels for chunk
    public void Populate() {

        for(int y=0; y<VoxelData.ChunkHeight; y++) {
            for(int x=0; x<VoxelData.ChunkWidth; x++) {
                for(int z=0; z<VoxelData.ChunkWidth; z++) {

                    map[x,y,z] = new VoxelState( World.Instance.CreateVoxel(new Vector3(x + position.x, y, z + position.y), false));

                }
            }
        }

        World.Instance.worldData.AddModified(this);

    }
}