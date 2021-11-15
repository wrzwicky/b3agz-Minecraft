using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Lighting {

    // (re)propogate natural light for a whole chunk
    public static void RecalculateNaturalLight(ChunkData chunkData) {

        for(int x=0; x<GameData.ChunkWidth; x++) {
            for(int z=0; z<GameData.ChunkWidth; z++) {

                CastNaturalLight(chunkData, x, z, GameData.ChunkHeight-1);

            }
        }
    }

    // propogate natural light straight down from the given x,y starting from startY
    public static void CastNaturalLight(ChunkData chunkData, int x, int z, int startY) {

        if(startY > GameData.ChunkHeight - 1) {

            startY = GameData.ChunkHeight - 1;
            Debug.LogWarning("Attempted to cast natrual light form above world.");

        }

        // bool to keep track of whether the light has hit a block with opacity.
        bool obstructed = false;

        for (int y=startY; y > -1; y--) {

            VoxelState voxel = chunkData.map[x, y, z];

            if(obstructed) {
                // light was obstructed; all blocks below are 0
                voxel.light = 0;
            }
            else if(voxel.blockType.opacity > 0) {
                // block has opacity, set light then obstruct
                voxel.light = 15;
                obstructed = true;
            }
            else {
                // max light
                voxel.light = 15;
            }
        }
    }

    // change light level at a voxel, recalc neighbors
    public static void ChangeLight(ChunkData chunkData, Vector3Int pos, int light) {
        // set voxel light
        // optimized_recalc(pos)
    }

    public static void OptimizedRecalc(ChunkData chunkData, Vector3Int pos) {
        // do
        //   for x/y/z around pos +/- 1
        //     recalc light:
        //       if any voxel(x,y,z).neighbors light > voxel.light
        //         update voxel.light
        //   expand cube by 1
        // while something changed
    }
}