using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Structure {

    // 6 'trunk' -> tree
    // 12 'cactus' -> cactus
    public static void GenerateMajorFlora(int index, Vector3 position, Jobs<VoxelMod> queue, int minHeight, int maxHeight, float headSize) {
        //Debug.Log("Flora "+index+" at "+position);
        switch(index) {

            case 6:
                MakeTree(position, queue, minHeight, maxHeight, headSize);
                break;

            case 12:
                MakeCactus(position, queue, minHeight, maxHeight);
                break;

            default:
                break;

        }
    }

    public static void MakeTree(Vector3 position, Jobs<VoxelMod> queue, int minTrunkHeight, int maxTrunkHeight, float headSize) {

        int height = (int)(maxTrunkHeight * Noise.Get2DPerlin(new Vector3(position.x, position.z), 250f, 3f));

        if(height < minTrunkHeight)
            height = minTrunkHeight;

        // trunk (i=0 puts stump in ground)
        for(int i=0; i<height; i++)
            queue.Add(new ReplaceVoxelMod(new Vector3(position.x, position.y + i, position.z), 6));

        Vector3 leafCenter = new Vector3(position.x, position.y + height + 3, position.z);

        // FOILage
        for(float x = -headSize; x<=headSize; x++) {
            for(float y = 0; y <= 2*headSize; y++) {
                for(float z = -headSize; z<=headSize; z++) {

                    Vector3 p = new Vector3(position.x + x, position.y + height + y, position.z + z);
                    if((p-leafCenter).magnitude <= headSize)
                        queue.Add(new AddVoxelMod(p, 11));

                }
            }
        }
    }

    public static void MakeCactus(Vector3 position, Jobs<VoxelMod> queue, int minTrunkHeight, int maxTrunkHeight) {

        int height = (int)(maxTrunkHeight * Noise.Get2DPerlin(new Vector3(position.x, position.z), 23456f, 2f));

        if(height < minTrunkHeight)
            height = minTrunkHeight;

        // trunk
        for(int i=1; i<height; i++)
            queue.Add(new AddVoxelMod(new Vector3(position.x, position.y + i, position.z), 12));

    }
 }