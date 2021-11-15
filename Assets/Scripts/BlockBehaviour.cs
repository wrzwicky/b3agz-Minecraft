using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public static class BlockBehaviour {

    public const byte idNOTHING = 0;
    public const byte idGRASS = 3;
    public const byte idDIRT = 5;

    // return true if block is 'active'
    //   voxel has behavior and *can* do something
    public static bool Active(Vector3 pos) {

        VoxelState voxel = World.Instance.GetState(pos);

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
                    tv.chunk.RemoveActiveVoxel(tv.pos);
                    //tv.chunk.SetVoxelFromGlobalPosition(tv.pos, idDIRT);
                    World.Instance.modifications.Add(new ReplaceVoxelMod(tv.pos, idDIRT));
                    return;
                }

                // List(VoxelState)
                // if(tv.back != null && tv.back.id != idDIRT)



                break;

        }
    }
}
