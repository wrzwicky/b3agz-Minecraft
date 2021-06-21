using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using UnityEngine;

// 'this is the class that gets saveed'
[System.Serializable]
public class WorldData {

    public string worldName = "Prototype";
    public int seed;

    [System.NonSerialized]
    public Dictionary<Vector2Int, ChunkData> chunks = new Dictionary<Vector2Int, ChunkData>();

    [System.NonSerialized]
    public Jobs<ChunkData> modifiedChunks = new Jobs<ChunkData>();

    [System.NonSerialized]
    public string savePath;  // where world is being saved


    public WorldData() {

        seed = VoxelData.seed;

    }

    public WorldData(String name, int seed) {

        this.worldName = name;
        this.seed = seed;
        
    }

    // helper to properly re-construct from deserialzed instance
    // (deserialized does not init chunks for us)
    public WorldData(WorldData wd) {

        worldName = wd.worldName;
        seed = wd.seed;

    }

    // mark chunk as modified
    public void AddModified(ChunkData chunk) {

        modifiedChunks.Add(chunk);
        
    }

    /// <summary>
    /// index = 2D index of chunk in map
    /// </summary>
    public ChunkData RequestChunk(Vector2Int index, bool create) {

        ChunkData c = null;

        lock(World.Instance.chunkListThreadLock) {

            if(chunks.ContainsKey(index))
                c = chunks[index];
            else if (create) {

                LoadChunk(index);
                c = chunks[index];

            }

        }

        return c;

    }

    public void LoadChunk(Vector2Int coord) {

        if(chunks.ContainsKey(coord))
            return;

        // load chunk from file
        ChunkData chunkData = null;
        if(savePath != null)
            chunkData = SaveSystem.LoadChunk(savePath, coord);

        if(chunkData == null) {

            // return new chunk
            chunkData = new ChunkData(coord);
            chunkData.Populate();

        }

        chunks.Add(coord, chunkData);
        
    }

    // return true if pos is within range defined in VoxelData to have voxels
    bool IsVoxelInWorld(Vector3 pos) {

        return (pos.x >= 0 && pos.x < VoxelData.WorldSizeInVoxels
            && pos.y >= 0 && pos.y < VoxelData.ChunkHeight
            && pos.z >= 0 && pos.z < VoxelData.WorldSizeInVoxels);

    }

    // pos = pos in world
    public void SetVoxel(Vector3 pos, byte value) {

        if(!IsVoxelInWorld(pos))
            return;

        // get chunk index
        int x = Mathf.FloorToInt(pos.x / VoxelData.ChunkWidth);
        int z = Mathf.FloorToInt(pos.x / VoxelData.ChunkWidth);
        
        // get origin of chunk
        x *= VoxelData.ChunkWidth;
        z *= VoxelData.ChunkWidth;

        // fetch/create
        Vector2Int coord = new Vector2Int(x, z);
        ChunkData chunk = RequestChunk(coord, true);

        // get pos relative to chunk
        Vector3Int voxel = new Vector3Int((int)(pos.x - x), (int)(pos.y), (int)(pos.z - z));

        // finally update voxel
        chunk.map[voxel.x, voxel.y, voxel.z].id = value;

        modifiedChunks.Add(chunk);

    }

    public VoxelState GetVoxel(Vector3 pos) {

        if(!IsVoxelInWorld(pos))
            return null;

        // get chunk index
        int x = Mathf.FloorToInt(pos.x / VoxelData.ChunkWidth);
        int z = Mathf.FloorToInt(pos.x / VoxelData.ChunkWidth);
        
        // get origin of chunk
        x *= VoxelData.ChunkWidth;
        z *= VoxelData.ChunkWidth;

        // fetch/create
        Vector2Int coord = new Vector2Int(x, z);
        ChunkData chunk = RequestChunk(coord, true);

        // get pos relative to chunk
        Vector3Int voxel = new Vector3Int((int)(pos.x - x), (int)(pos.y), (int)(pos.z - z));

        // finally update voxel
        return chunk.map[voxel.x, voxel.y, voxel.z];

    }

    // finish construct when deserializing
    [OnDeserialized]    
    private void OnDeserialized(StreamingContext context) {

        chunks = new Dictionary<Vector2Int, ChunkData>();
        modifiedChunks = new Jobs<ChunkData>();

    }
}
