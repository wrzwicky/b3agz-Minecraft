using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;

//TODO threading: Activate() calls ThreadController.Call(UpdateChunk), which pushes isActive to game thread

public class Chunk
{
    static readonly ProfilerMarker s_profUpdateChunk = new ProfilerMarker("Chunk.UpdateChunk");
    static readonly ProfilerMarker s_profRelight = new ProfilerMarker("Chunk.RecalculateLight");
    static readonly ProfilerMarker s_profResetPoly = new ProfilerMarker("Chunk.ResetPolys");
    static readonly ProfilerMarker s_profApplyMods = new ProfilerMarker("Chunk.ApplyModifications");

    // convert .orientation to y-rotation angle
    static readonly float[] ROT_ANGLE = {180f, 0f, 0f, 0f, 90f, 270f};
    
    // rotate faceChecks index by .orientation
    // new-p = ROT_FACECHECKS[orientation, old-p]
    int[,] ROT_FACECHECKS = {
        //N,S, T,B, W,E
        {1,0, 2,3, 5,4},   // orientation == 0-back
        {0,1, 2,3, 4,5},   // orientation == 1-front
        {0,1, 2,3, 4,5},   // orientation == 2-top (unused)
        {0,1, 2,3, 4,5},   // orientation == 3-bottom (unused)
        {4,5, 2,3, 1,0},   // orientation == 4-west
        {5,4, 2,3, 0,1},   // orientation == 5-east
    };

    public ChunkCoord coord;

    GameObject chunkObject;
    MeshRenderer meshRenderer;
    MeshFilter meshFilter;
    List<Vector3> vertices = new List<Vector3>();
    List<int> triangles = new List<int>();
    List<int> transparentTriangles = new List<int>();
    List<int> waterTriangles = new List<int>();
    Material[] materials = new Material[3];
    List<Vector2> uvs = new List<Vector2>();
    List<Color> colors = new List<Color>();
    List<Vector3> normals = new List<Vector3>();

    bool freshMesh = false;  //true if vertices,etc have changed

    // location in the world of this chunk's origin
    public Vector3 origin;

    World world;

    public bool isVoxelMapPopulated = false;  //explicit; not true til voxelMap is fully done
    bool _isActive;

    public Jobs<VoxelMod> modifications = new Jobs<VoxelMod>();

    // - - - - - New WorldData system - - - - - //

    ChunkData chunkData;

    // - - - - - - Block Behaviours  - - - - - //

    HashSet<TrackedVoxel> activeVoxels = new HashSet<TrackedVoxel>();

    // - - - - - - - - - - - - - - - - - - - - //


    // perform basic chunk init
    public Chunk(ChunkCoord _coord, World _world) {

        coord = _coord;
        origin = new Vector3(coord.x * GameData.ChunkWidth, 0f, coord.z * GameData.ChunkWidth);
        world = _world;
        _isActive = false;

        // Chunk is a kind of game object, so we can trust that World is available
        // Also apparently we can call this from another thread, but we need it now.
        chunkData = World.Instance.worldData.RequestChunk(new Vector2Int((int)origin.x, (int)origin.z), true);
        isVoxelMapPopulated = true;

        for(int y=0; y<GameData.ChunkHeight; y++) {
            for(int x=0; x<GameData.ChunkWidth; x++) {
                for(int z=0; z<GameData.ChunkWidth; z++) {
        
                    VoxelState voxel = chunkData.map[x,y,z];
                    if(voxel.blockType.isActive)
                        AddActiveVoxel(new TrackedVoxel(this, new Vector3(x,y,z)));

                }
            }
        }
    }

    // init Unity objects. must be called on game thread.
    // must be called early; we won't have chunkData until World is available
    public void InitForUnity() {

        if(chunkObject == null) {

            chunkObject = new GameObject(string.Format("Chunk ({0},{1})", coord.x, coord.z));
            chunkObject.SetActive(false);

            meshFilter = chunkObject.AddComponent<MeshFilter>();
            meshRenderer = chunkObject.AddComponent<MeshRenderer>();

            chunkObject.transform.SetParent(world.transform);
            chunkObject.transform.position = origin;

            materials[0] = world.material;
            materials[1] = world.transparentMaterial;
            materials[2] = world.waterMaterial;
            meshRenderer.materials = materials;

        }
    }

    public bool isActive {

        get { return _isActive; }
        set {
            
            _isActive = value;
            if(chunkObject != null)
                chunkObject.SetActive(value);

        }

    }

    public void AddActiveVoxel(TrackedVoxel vox) {
        activeVoxels.Add(vox);
    }

    public void RemoveActiveVoxel(Vector3 pos) {
        activeVoxels.RemoveWhere(tv => tv.pos == pos);
    }

    /// create new voxels for chunk
    void PopulateVoxelMap() {

        if(!isVoxelMapPopulated) {

            chunkData.Populate();

            isVoxelMapPopulated = true;

        }
    }

    /// <summary>Update voxelMap with registered mods</summary>
    public int ApplyModifications() {
        using(s_profApplyMods.Auto()) {

        int qty = 0;  // keep actual count cuz why not

        // Do the voxel mods, if any.
        while(true) {

            VoxelMod m = modifications.Any();
            if(m == null)
                break;

            if(m.Apply(this))
                qty++;
            
        }

        return qty;

    }}

    public void TickUpdate() {

        if(activeVoxels.Count == 0)
            return;

        Debug.Log(coord + " currently has " + activeVoxels.Count + " active blocks.");
        // clone list so we can modify while iterating
        foreach(TrackedVoxel tv in new List<TrackedVoxel>(activeVoxels)) {
            if(!BlockBehaviour.Active(tv.pos))
                activeVoxels.Remove(tv);
            else
                BlockBehaviour.Behave(tv);
        }

    }

    /// <summary>
    /// Update this chunks voxels as needed.
    /// </summary>
    public void UpdateChunk() {
        using(s_profUpdateChunk.Auto()) {

//        PopulateVoxelMap(); -- done by RequestChunk

        bool modded = ApplyModifications() > 0;

        // need to (re)gen mesh?
        bool newMesh = true; // (modded || vertices.Count == 0);

        if(newMesh) {

            RecalculateLight();

            ResetPolys();
            //meshFilter.mesh = CreateMesh();
            world.chunksToDraw.Add(this);

        }
    }}

    // return false if x,y,z is outside chuck local coords (0..ChunkWidth/Height)
    bool IsVoxelInChunk(Vector3Int v) {

        if(v.x<0 || v.x>GameData.ChunkWidth-1
            || v.y<0 || v.y>GameData.ChunkHeight-1
            || v.z<0 || v.z>GameData.ChunkWidth-1)
            // no voxels outside this chunk
            return false;
        else
            return true;

    }

    // return false if x,y,z is outside chuck local coords (0..ChunkWidth/Height)
    bool IsVoxelInChunk(int x, int y, int z) {

        if(x<0 || x>GameData.ChunkWidth-1
            || y<0 || y>GameData.ChunkHeight-1
            || z<0 || z>GameData.ChunkWidth-1)
            // no voxels outside this chunk
            return false;
        else
            return true;

    }

    /// <summary>
    /// pos is relative to chunk origin;
    /// if pos is outside chunk, World is called to find appropriate chunk
    /// </summary>
    BlockType Voxel(Vector3 pos) {
        
        // mathematically more correct than just (int)
        Vector3Int v = Vector3Int.FloorToInt(pos);

        if (!IsVoxelInChunk(v))
            return world.Voxel(origin + pos);

        return world.blockTypes[chunkData.map[v.x, v.y, v.z].id];

    }

    /// <summary>pos is relative to chunk origin</summary>
    VoxelState GetState(Vector3 pos) {
        
        // mathematically more correct than just (int)
        Vector3Int v = Vector3Int.FloorToInt(pos);

        if (!IsVoxelInChunk(v))
            return world.GetState(origin + pos);

        return chunkData.map[v.x, v.y, v.z];

    }

    // pos is worldwide, but must be within this chunk
    public VoxelState GetVoxelFromGlobalPosition(Vector3 pos) {
        
        Vector3Int v = Vector3Int.FloorToInt(pos - origin);
        return chunkData.map[v.x, v.y, v.z];

    }

    // pos is worldwide, but must be within this chunk
    public void SetVoxelFromGlobalPosition(Vector3 pos, byte id) {

        SetVoxelFromGlobalPosition(pos, id, 0);

    }

    // pos is worldwide, but must be within this chunk
    public void SetVoxelFromGlobalPosition(Vector3 pos, byte id, byte orientation) {

        Vector3Int v = Vector3Int.FloorToInt(pos - origin);
        chunkData.map[v.x, v.y, v.z].id = id;
        chunkData.map[v.x, v.y, v.z].orientation = orientation;
        World.Instance.worldData.AddModified(chunkData);

        if(world.blockTypes[id].isActive && BlockBehaviour.Active(pos)) {
            TrackedVoxel tv = new TrackedVoxel(pos);
            AddActiveVoxel(tv);
        }

    }

    // pos is worldwide
    public void EditVoxel(Vector3 pos, byte newID) {

        Vector3Int v = Vector3Int.FloorToInt(pos - origin);
        chunkData.map[v.x, v.y, v.z].id = newID;
        World.Instance.worldData.AddModified(chunkData);

        world.chunksToUpdate.Add(coord);
        UpdateSurroundingVoxels(v);

    }

    void UpdateSurroundingVoxels(Vector3Int v) {

        for (int p=0; p<6; p++) {
        
            Vector3 checkVoxel = v + GameData.faceChecks[p];

            // make neighbors active (not recursive)
            TrackedVoxel tn = new TrackedVoxel(origin + checkVoxel);
            if(tn.voxel != null && BlockBehaviour.Active(tn.pos))
                AddActiveVoxel(tn);

            // if at chunk border, update neighbor chunk
            if(!IsVoxelInChunk(Vector3Int.FloorToInt(checkVoxel))) {

                world.chunksToUpdate.Add(world.GetChunkCoordFromPosition(checkVoxel + origin));

            }
        }
    }

    void ClearMeshData() {

        vertices.Clear();
        triangles.Clear();
        transparentTriangles.Clear();
        waterTriangles.Clear();
        uvs.Clear();
        colors.Clear();
        normals.Clear();

    }

    /// <summary>Generate fresh vertices and tris from current voxels</summary>
    public void ResetPolys() {
        using(s_profResetPoly.Auto()) {

        lock(vertices) {

            ClearMeshData();
//            CalculateLight_wrz();
//            //CalculateLight_b3agz();

            for(int y=0; y<GameData.ChunkHeight; y++) {
                for(int x=0; x<GameData.ChunkWidth; x++) {
                    for(int z=0; z<GameData.ChunkWidth; z++) {

                        UpdateMeshData(new Vector3(x,y,z));

                    }
                }
            }

            freshMesh = true;

        }

    }}

    // generate vertices, triangles, and colors for one voxel
    void UpdateMeshData(Vector3 pos) {

        Vector3Int ipos = Vector3Int.FloorToInt(pos);
        VoxelState voxel = chunkData.map[ipos.x, ipos.y, ipos.z];

        if(!voxel.blockType.isSolid)
            return;

        // Copy vertices in voxelTris order
        for (int p=0; p<6; p++) {

            int rotp = ROT_FACECHECKS[voxel.orientation, p];

            VoxelState neighbor = GetState(pos + GameData.faceChecks[rotp]);

            // suppress faces covered by other voxels
            if(neighbor.blockType.seeThrough && !(voxel.blockType.isWater && chunkData.map[ipos.x, ipos.y+1, ipos.z].blockType.isWater)) {

                FaceMeshData face = voxel.blockType.meshData.faces[p];

                float rot = ROT_ANGLE[voxel.orientation];

                //float lightLevel = myState.lightAsFloat;
                float lightLevel = neighbor.lightAsFloat;
                int firstVert = vertices.Count;

                for(int i=0; i < face.VertCount; i++) {

                    VertData vertData = face.GetVertData(i);

                    vertices.Add(pos + vertData.GetRotatedPos(new Vector3(0, rot, 0)));
                    normals.Add(GameData.faceChecks[p]);
                    colors.Add(new Color(0,0,0, lightLevel));
                    if(voxel.blockType.isWater)
                        uvs.Add(voxel.blockType.meshData.faces[p].vertices[i].uv);
                    else
                        AddTextureVert(voxel.blockType.GetTextureID(p), vertData.uv);

                }

                for(int i=0; i < face.triangles.Length; i++) {

                    List<int> dest = triangles;
                    if(voxel.blockType.isWater)
                        dest = waterTriangles;
                    else if (voxel.blockType.seeThrough)
                        dest = transparentTriangles;

                    dest.Add(firstVert + face.triangles[i]);

                }
            }
        }
    }

    void AddTextureVert(int textureID, Vector2 uv) {

        float y = textureID / GameData.TextureAtlasSizeInBlocks;
        float x = textureID - (y * GameData.TextureAtlasSizeInBlocks);

        x *= GameData.NormalizedBlockTextureSize;
        y *= GameData.NormalizedBlockTextureSize;

        y = 1f - y - GameData.NormalizedBlockTextureSize;

        x += GameData.NormalizedBlockTextureSize * uv.x;
        y += GameData.NormalizedBlockTextureSize * uv.y;

        uvs.Add(new Vector2(x,y));

    }

    // create Unity mesh objects from stored verts/tris
    public void CreateMesh() {

        if(freshMesh) {

            Vector3[] vertsA = null;
            int[] trisA = null;
            int[] transA = null;
            int[] waterA = null;
            Vector2[] uvsA = null;
            Color[] colorsA = null;
            Vector3[] normalsA = null;

            lock (vertices) {

                vertsA = vertices.ToArray();
                trisA = triangles.ToArray();
                transA = transparentTriangles.ToArray();
                waterA = waterTriangles.ToArray();

                uvsA = uvs.ToArray();
                colorsA = colors.ToArray();
                normalsA = normals.ToArray();

                freshMesh = false;

            }

            Mesh mesh = new Mesh();
            mesh.vertices = vertsA;
            
            mesh.subMeshCount = 3;
            mesh.SetTriangles(trisA, 0);  //opaque material
            mesh.SetTriangles(transA, 1);  //transparent material
            mesh.SetTriangles(waterA, 2);  //water material

            mesh.uv = uvsA;

            mesh.colors = colorsA;

            //mesh.RecalculateNormals();
            mesh.normals = normalsA;

            meshFilter.mesh = mesh;

        }

        if(chunkObject.activeSelf != isActive) {

            chunkObject.SetActive(isActive);

            if(isActive && world.settings.enableAnimatedChunks)
                // only works if we're on the main thread
                chunkObject.AddComponent<ChunkLoadAnimation>();

        }
    }




    public void PurgeLight() {

        for(int x=0; x<GameData.ChunkWidth; x++) {
            for(int y=0; y<GameData.ChunkHeight; y++) {
                for(int z=0; z<GameData.ChunkWidth; z++) {

                    chunkData.map[x,y,z].light = 0;

                }
            }
        }
    }

    public void RecalculateLight() {
        using(s_profRelight.Auto()) {

        PurgeLight();
        Lighting.RecalculateNaturalLight(chunkData);
        //TODO ReplaceLights() -- add light sources

        bool changed = false;
        int safety = 18;

        do {

            changed = false;
            
            for(int x=0; x<GameData.ChunkWidth; x++) {
                for(int y=0; y<GameData.ChunkHeight; y++) {
                    for(int z=0; z<GameData.ChunkWidth; z++) {

                        VoxelState voxel = chunkData.map[x, y, z];

                        // if hasn't been lit
                        if (voxel.light == 0) {

                            // get current light
                            int light = voxel.light;

                            // find brightest neighbor
                            for(int p = 0; p < GameData.faceChecks.Length; p++) {

                                Vector3Int index = new Vector3Int(x,y,z) + GameData.faceChecks[p];
                                VoxelState probe;

                                if(!IsVoxelInChunk(index))
                                    probe = World.Instance.GetState(origin + index);
                                else
                                    probe = chunkData.map[index.x, index.y, index.z];

                                if(probe.blockType.isSolid)
                                    // dim with distance
                                    light = Math.Max(light, probe.light-1);
                                else
                                    light = Math.Max(light, probe.light);

                            }

                            // if light changed
                            if(light != voxel.light) {

                                // // dim with distance
                                // if(light > 0)
                                //     light -= 1;

                                voxel.light = (byte)light;
                                changed = true;

                            }
                        }
                    }
                }
            }

            safety--;

        } while(changed && safety > 0);
    }}



    public override string ToString()
    {
        return base.ToString()+"("+coord+")";
    }
}



// index of chunk in chunk map
public class ChunkCoord {

    public int x;
    public int z;

    public ChunkCoord() {

        x = 0; z = 0;

    }

    public ChunkCoord(int _x, int _z) {
        x = _x;
        z = _z;
    }

    // pos = coords of voxel; we become coord of enclosing chunk
    public ChunkCoord(Vector3 pos) {

        x = Mathf.FloorToInt(pos.x) / GameData.ChunkWidth;
        z = Mathf.FloorToInt(pos.z) / GameData.ChunkWidth;

    }

    public Vector2Int V2 { get { return new Vector2Int(x,z); } }

    // override object.Equals
    public override bool Equals(object obj)
    {
        if (obj == null || GetType() != obj.GetType())
        {
            return false;
        }

        ChunkCoord other = (ChunkCoord)obj;
        if(other.x == x && other.z == z)
            return true;
        else
            return false;
    }
    
    // override object.GetHashCode
    public override int GetHashCode()
    {

        return string.Format("{0} {1}", x, z).GetHashCode();

    }

    public override string ToString() {

        return "(" + x + ", " + z + ")";

    }
}


/// replacement for beagz' complex VoxelState class
public class TrackedVoxel {
    public Chunk chunk;
    public VoxelState voxel;
    public Vector3 pos;


    public TrackedVoxel(Vector3 pos) { 
        this.pos = pos;
        this.chunk = World.Instance.GetChunkFromPosition(pos);
        this.voxel = this.chunk.GetVoxelFromGlobalPosition(pos);
    }

    public TrackedVoxel(Chunk chunk, Vector3 relPos) {
        this.pos = chunk.origin + relPos;
        this.chunk = chunk;
        this.voxel = this.chunk.GetVoxelFromGlobalPosition(this.pos);
    }

    // public TrackedVoxel back { get {
    //     var p = pos + GameData.faceChecks[0];
    //     return World.Instance.IsVoxelInWorld(p) ? new TrackedVoxel(p) : null;
    // }}

    public VoxelState back  { get { return chunk.GetVoxelFromGlobalPosition(pos + GameData.faceChecks[0]); }}
    public VoxelState front { get { return chunk.GetVoxelFromGlobalPosition(pos + GameData.faceChecks[1]); }}
    public VoxelState above { get { return chunk.GetVoxelFromGlobalPosition(pos + GameData.faceChecks[2]); }}
    public VoxelState below { get { return chunk.GetVoxelFromGlobalPosition(pos + GameData.faceChecks[3]); }}
    public VoxelState left  { get { return chunk.GetVoxelFromGlobalPosition(pos + GameData.faceChecks[4]); }}
    public VoxelState right { get { return chunk.GetVoxelFromGlobalPosition(pos + GameData.faceChecks[5]); }}

    public VoxelState[] neighbors { get {
        return new VoxelState[6] {
            back, front, above, below, left, right
        };
    }}

    // public TrackedVoxel[] neighbors { get {
    //     TrackedVoxel[] n = new TrackedVoxel[6];
    //     for(int p=0; p<6; p++) {
    //         var np = pos + GameData.faceChecks[p];
    //         if(World.Instance.IsVoxelInWorld(np))
    //             n[p] = new TrackedVoxel(np);
    //     }
    //     return n;
    // }
}
