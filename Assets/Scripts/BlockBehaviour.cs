using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/**
 * <summary>Very simple block simulation. Kills smothered grass, spreads planted grass.</summary>
 */
public static class BlockBehaviour {

    // gotta hardcode thses cuz we got a lotta hardcoded behavior in the code
    public const byte idNOTHING = 0;
    public const byte idBEDROCK = 1;
    public const byte idSTONE = 2;
    public const byte idGRASS = 3;
    public const byte idDIRT = 5;
    public const byte idWOOD = 6;
    public const byte idPLANK = 7;
    public const byte idBRICK = 8;
    public const byte idCOBBLE = 9;
    public const byte idGLASS = 10;
    public const byte idLEAF = 11;
    public const byte idCACTUS = 12;
    public const byte idCACTOP = 13;
    public const byte idFURNACE = 14;
    public const byte idWATER = 15;

    // return true if block is 'active'
    // == block type is marked active, and block might need an update
    // (i.e. Behave() needs to be called)
    public static bool IsActive(Vector3 pos) {

        VoxelState voxel = World.Instance.GetState(pos);

        if(!voxel.blockType.isActive)
            return false;

        switch(voxel.id) {

            case idGRASS: {
                // planar neighbors
                VoxelState v0 = World.Instance.GetState(pos + GameData.faceChecks[0]);
                VoxelState v1 = World.Instance.GetState(pos + GameData.faceChecks[1]);
                VoxelState v4 = World.Instance.GetState(pos + GameData.faceChecks[4]);
                VoxelState v5 = World.Instance.GetState(pos + GameData.faceChecks[5]);
                // on top
                VoxelState v2 = World.Instance.GetState(pos + GameData.faceChecks[2]);

                // check planar neighbors for dirt, top for air
                if((v2 != null && v2.id == idNOTHING) &&
                   ((v0 != null && v0.id == idDIRT) ||
                    (v1 != null && v1.id == idDIRT) ||
                    (v4 != null && v4.id == idDIRT) ||
                    (v5 != null && v5.id == idDIRT))) {
                    //Debug.Log("Grass block with potential to spread has been found.");
                    return true;
                }
                else if(v2 != null && v2.id != idNOTHING)
                    // block on top -> grass is smothered
                    return true;

                break;
            }

        }

        return false;
    }

    /// make voxel at world coord do whatever it does
    public static void Behave(TrackedVoxel tv) {

        switch(tv.voxel.id) {

            case idGRASS:
                if(tv.above != null && tv.above.id != idNOTHING) {
                    World.Instance.modifications.Add(new ReplaceVoxelMod(tv.pos, idDIRT));
                    return;
                }

                if((tv.back != null && tv.back.id == idDIRT) ||
                    (tv.front != null && tv.front.id == idDIRT) ||
                    (tv.left != null && tv.left.id == idDIRT) ||
                    (tv.right != null && tv.right.id == idDIRT)) {

                        Debug.Log(" -doing grass");
                        //World.Instance.modifications.Add(new ReplaceVoxelMod(tv.pos, idNOTHING));
                        if(tv.back.id == idDIRT)
                            World.Instance.modifications.Add(new ReplaceVoxelAndSim(
                                tv.pos + GameData.faceChecks[0], idGRASS));
                        if(tv.front.id == idDIRT)
                            World.Instance.modifications.Add(new ReplaceVoxelAndSim(
                                tv.pos + GameData.faceChecks[1], idGRASS));
                        if(tv.left.id == idDIRT)
                            World.Instance.modifications.Add(new ReplaceVoxelAndSim(
                                tv.pos + GameData.faceChecks[4], idGRASS));
                        if(tv.right.id == idDIRT)
                            World.Instance.modifications.Add(new ReplaceVoxelAndSim(
                                tv.pos + GameData.faceChecks[5], idGRASS));

                }

                break;

        }
    }
}
