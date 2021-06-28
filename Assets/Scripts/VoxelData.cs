using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class VoxelData
{

    public static readonly int ChunkWidth = 16;
    public static readonly int ChunkHeight = 128;
    public static readonly int WorldSizeInChunks = 100;

    // Lighting values
    public static float minLightLevel = 0.05f;
    public static float maxLightLevel = 0.8f;
//    public static float lightFalloff = 0.08f; now just 1 unitOfLight
    
    // world seed
    public static int seed = Mathf.Abs(Utils.SuperRandom()) / VoxelData.WorldSizeInChunks;

    public static int WorldCentre {
        get { return (WorldSizeInChunks * ChunkWidth) / 2; }
    }

    public static int WorldSizeInVoxels {
        get { return WorldSizeInChunks * ChunkWidth; }
    }

    // atlas holds 4x4 textures
    public static readonly int TextureAtlasSizeInBlocks = 16;
    // float size of one texture in atlas
    public static float NormalizedBlockTextureSize {
        get { return 1f / (float)TextureAtlasSizeInBlocks; }
    }


    // Just a cube, for now
    public static readonly Vector3[] voxelVerts = new Vector3[8] {
        new Vector3(0f,0f,0f),
        new Vector3(1f,0f,0f),
        new Vector3(1f,1f,0f),
        new Vector3(0f,1f,0f),
        new Vector3(0f,0f,1f),
        new Vector3(1f,0f,1f),
        new Vector3(1f,1f,1f),
        new Vector3(0f,1f,1f),
    };

    // offset to neighbor voxel per face
    public static readonly Vector3Int[] faceChecks = new Vector3Int[6] {
        new Vector3Int(0,0,-1), //back
        new Vector3Int(0,0, 1), //front
        new Vector3Int(0, 1,0), //top
        new Vector3Int(0,-1,0), //bottom
        new Vector3Int(-1,0,0), //left
        new Vector3Int( 1,0,0), //right
    };

    // 6 faces, 2 tris per face, clockwise
    public static readonly int[,] voxelTris = new int[6,4] {
        
        {0,3,1, 2}, //back
        {5,6,4, 7}, //front
        {3,7,2, 6}, //top face
        {1,5,0, 4}, //bottom
        {4,7,0, 3}, //left
        {1,2,5, 6}, //right
    };

    public static readonly Vector2[] voxelUvs = new Vector2[4] {
        new Vector2(0,0),
        new Vector2(0,1),
        new Vector2(1,0),
        new Vector2(1,1),
    };

    // Light is handle as float (0-1) but Minecraft stores it as a byte (0-15),
    // thus conversion.
    public static float unitOfLight {
        get { return 1f / 16f; }
    }
}
