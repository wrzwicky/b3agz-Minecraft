using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Structure {

    public static VoxelState[,,] CreateChunkArray(World world, Vector2Int origin) {

        Vector3 origin3d = new Vector3(origin.x, 0, origin.y);
        VoxelState[,,] map = new VoxelState[GameData.ChunkWidth, GameData.ChunkHeight, GameData.ChunkWidth];
        Jobs<VoxelMod> modifications = new Jobs<VoxelMod>();

        for(int y=0; y<GameData.ChunkHeight; y++) {
            for(int x=0; x<GameData.ChunkWidth; x++) {
                for(int z=0; z<GameData.ChunkWidth; z++) {

                    Vector3 pos3d = new Vector3(x + origin.x, y, z + origin.y);
                    byte newId = CreateVoxel(world, pos3d, modifications);
                    map[x,y,z] = new VoxelState(newId);

                }
            }
        }

        // apply voxel mods immediately to avoid trigger block behavior
        foreach (var mod in modifications) {

            Vector3 pos3d = mod.position - origin3d;
            Vector3Int v = Vector3Int.FloorToInt(pos3d);
    
            switch(mod) {
                case AddVoxelMod t1:
                    map[v.x, v.y, v.z].id = t1.id;
                    map[v.x, v.y, v.z].orientation = t1.orientation;
                    break;
                case ReplaceVoxelMod t2:
                    map[v.x, v.y, v.z].id = t2.id;
                    map[v.x, v.y, v.z].orientation = t2.orientation;
                    break;
            }
        }

        return map;
    }

    /// generate new voxel for pos in world
    /// returns block type for given pos
    /// modifications will receive extra changes to apply to this chunk
    /// after block gen is complete, if needed.
    public static byte CreateVoxel(World world, Vector3 pos, Jobs<VoxelMod> modifications) {

        int yPos = Mathf.FloorToInt(pos.y);

        // -- IMMUTABLE PASS -- //

        // outside world -> air
        if(!world.IsVoxelInWorld(pos))
            return BlockBehaviour.idNOTHING;

        // bottom of chunk -> bedrock
        if(pos.y <= 0)
            return BlockBehaviour.idBEDROCK;

        // -- BIOME SELECTION PASS -- //

        int solidGroundHeight = 42;
        float sumOfHeight = 0;
        int count = 0;
        float strongestWeight = 0f;
        int strongestIndex = 0;

        for(int i=0; i < world.biomes.Length; i++) {

            // choose 'strongest' biome for this voxel
            float weight = Noise.Get2DPerlin(new Vector2(pos.x, pos.z), world.biomes[i].offset, world.biomes[i].scale);
            if(weight > strongestWeight) {

                strongestWeight = weight;
                strongestIndex = i;

            }

            // height is average of all biomes, for smoothness
            float height = world.biomes[i].terrainHeight * Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 0, world.biomes[i].terrainScale) * weight;
            if(height > 0) {
                sumOfHeight += height;
                count++;
            }

        }

        BiomeAttributes biome = world.biomes[strongestIndex];

        int terrainHeight = Mathf.FloorToInt(sumOfHeight/count + solidGroundHeight);


        // -- BASIC TERRAIN PASS -- //

        byte voxelValue = BlockBehaviour.idNOTHING;

        if(yPos == terrainHeight)
            voxelValue = biome.surfaceBlock;
        else if(yPos < terrainHeight && yPos > terrainHeight - 4)
            voxelValue = biome.subSurfaceBlock;
        else if(yPos > terrainHeight) {

            if(yPos < 51)  //TODO don't hardcode sea level!
                return BlockBehaviour.idWATER;
            else
                return BlockBehaviour.idNOTHING;

        }
        else
            voxelValue = BlockBehaviour.idSTONE;


        // -- SECOND PASS -- //

        if(voxelValue == BlockBehaviour.idSTONE) {

            voxelValue = CheckLodes(voxelValue, biome, pos);

        }


        // -- MAJOR FLORA PASS -- //

        // trees can only sprout on surface
        if(yPos == terrainHeight && biome.placeMajorFlora) {
            if( CheckTrees(voxelValue, biome, pos) ) {

                voxelValue = biome.zoneSurfaceBlock;

                Structure.GenerateMajorFlora(
                    biome.majorFloraIndex, pos, modifications, 
                    biome.minHeight, biome.maxHeight, biome.headSize);

            }
        }

        return voxelValue;

    }

    /// Modify voxel value based on biome lodes
    static byte CheckLodes(byte voxelValue, BiomeAttributes biome, Vector3 pos) {

        int yPos = Mathf.FloorToInt(pos.y);

        foreach(Lode lode in biome.lodes) {
            if(yPos > lode.minHeight && yPos < lode.maxHeight) {
                if(Noise.Get3DPerlin(pos, lode.noiseOffset, lode.scale, lode.threshold))
                
                    //TODO more intelligently pick which lode we keep
                    voxelValue = lode.blockID;

            }
        }

        return voxelValue;

    }

    /// return 'true' if we should plant a tree here
    static bool CheckTrees(byte voxelValue, BiomeAttributes biome, Vector3 pos) {

        int yPos = Mathf.FloorToInt(pos.y);
        ISet<Vector3> treePos = new HashSet<Vector3>();

        // make patches that can be forest-y
        if(Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 123, biome.majorFloraZoneScale) > biome.majorFloraZoneTheshold) {

            // within patches, make trees
            return Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 123, biome.majorFloraPlacementScale) > biome.majorFloraPlacementThreshold;

        }

        return false;

    }

    // 6 'trunk' -> tree
    // 12 'cactus' -> cactus
    public static void GenerateMajorFlora(int index, Vector3 position, Jobs<VoxelMod> queue, int minHeight, int maxHeight, float headSize) {
        //Debug.Log("Flora "+index+" at "+position);
        switch(index) {

            case BlockBehaviour.idWOOD:
                MakeTree(position, queue, minHeight, maxHeight, headSize);
                break;

            case BlockBehaviour.idCACTUS:
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

        for(int i=1; i<height; i++)
            queue.Add(new AddVoxelMod(new Vector3(position.x, position.y + i, position.z), BlockBehaviour.idCACTUS));

        queue.Add(new AddVoxelMod(new Vector3(position.x, position.y + height, position.z), BlockBehaviour.idCACTOP));
    }
 }