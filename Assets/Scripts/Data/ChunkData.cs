using UnityEngine;


/// <summary>
/// Just the data for a chunk; no Unity code.
/// 'this is the class that gets saved'
/// </summary>
[System.Serializable]
public class ChunkData {

    // Location of this chunk in world
    // Vector2Int is not serializable :(
    int x;
    int y;

    // hidden; is too much data for inspector
    [HideInInspector]
    public VoxelState[,,] map = new VoxelState[GameData.ChunkWidth, GameData.ChunkHeight, GameData.ChunkWidth];


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

        for(int y=0; y<GameData.ChunkHeight; y++) {
            for(int x=0; x<GameData.ChunkWidth; x++) {
                for(int z=0; z<GameData.ChunkWidth; z++) {

                    map[x,y,z] = new VoxelState( World.Instance.CreateVoxel(new Vector3(x + position.x, y, z + position.y), false));

                }
            }
        }

        Lighting.RecalculateNaturalLight(this);

        World.Instance.worldData.AddModified(this);

    }
}