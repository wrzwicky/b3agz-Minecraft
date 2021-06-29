using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


//TODO threading: Activate() calls ThreadController.Call(UpdateChunk), which pushes isActive to game thread

public class Chunk
{
    public ChunkCoord coord;

    GameObject chunkObject;
    MeshRenderer meshRenderer;
    MeshFilter meshFilter;
    List<Vector3> vertices = new List<Vector3>();
    List<int> triangles = new List<int>();
    List<int> transparentTriangles = new List<int>();
    Material[] materials = new Material[2];
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

    // - - - - - - - - - - - - - - - - - - - - //


    // perform basic chunk init
    public Chunk(ChunkCoord _coord, World _world) {

        coord = _coord;
        origin = new Vector3(coord.x * VoxelData.ChunkWidth, 0f, coord.z * VoxelData.ChunkWidth);
        world = _world;
        _isActive = false;

        // Chunk is a kind of game object, so we can trust that World is available
        // Also apparently we can call this from another thread, but we need it now.
        chunkData = World.Instance.worldData.RequestChunk(new Vector2Int((int)origin.x, (int)origin.z), true);
        isVoxelMapPopulated = true;

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

    /// create new voxels for chunk
    void PopulateVoxelMap() {

        if(!isVoxelMapPopulated) {

            chunkData.Populate();

            isVoxelMapPopulated = true;

        }
    }

    /// <summary>Update voxelMap with registered mods</summary>
    public int ApplyModifications() {

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

    }

    /// <summary>
    /// Update this chunks voxels as needed.
    /// </summary>
    public void UpdateChunk() {

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
    }

    // return false if x,y,z is outside chuck local coords (0..ChunkWidth/Height)
    bool IsVoxelInChunk(Vector3Int v) {

        if(v.x<0 || v.x>VoxelData.ChunkWidth-1
            || v.y<0 || v.y>VoxelData.ChunkHeight-1
            || v.z<0 || v.z>VoxelData.ChunkWidth-1)
            // no voxels outside this chunk
            return false;
        else
            return true;

    }

    // return false if x,y,z is outside chuck local coords (0..ChunkWidth/Height)
    bool IsVoxelInChunk(int x, int y, int z) {

        if(x<0 || x>VoxelData.ChunkWidth-1
            || y<0 || y>VoxelData.ChunkHeight-1
            || z<0 || z>VoxelData.ChunkWidth-1)
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

        Vector3Int v = Vector3Int.FloorToInt(pos - origin);
        chunkData.map[v.x, v.y, v.z].id = id;
        World.Instance.worldData.AddModified(chunkData);

    }

    // pos is worldwide
    public void EditVoxel(Vector3 pos, byte newID) {

        Vector3Int v = Vector3Int.FloorToInt(pos - origin);
        chunkData.map[v.x, v.y, v.z].id = newID;
        World.Instance.worldData.AddModified(chunkData);

        //UpdateChunkBackground();
        //UpdateChunk_thread();
        world.chunksToUpdate.Add(coord);
        UpdateSurroundingVoxels(v);

    }

    void UpdateSurroundingVoxels(Vector3Int v) {

        for (int p=0; p<6; p++) {
        
            Vector3 checkVoxel = v + VoxelData.faceChecks[p];

            if(!IsVoxelInChunk(Vector3Int.FloorToInt(checkVoxel))) {

                world.chunksToUpdate.Add(world.GetChunkCoordFromPosition(checkVoxel + origin));

            }
        }
    }

    void ClearMeshData() {

        vertices.Clear();
        triangles.Clear();
        transparentTriangles.Clear();
        uvs.Clear();
        colors.Clear();
        normals.Clear();

    }

    /// <summary>Generate fresh vertices and tris from current voxels</summary>
    public void ResetPolys() {

        lock(vertices) {

            ClearMeshData();
//            CalculateLight_wrz();
//            //CalculateLight_b3agz();

            for(int y=0; y<VoxelData.ChunkHeight; y++) {
                for(int x=0; x<VoxelData.ChunkWidth; x++) {
                    for(int z=0; z<VoxelData.ChunkWidth; z++) {

                        UpdateMeshData(new Vector3(x,y,z));

                    }
                }
            }

            freshMesh = true;

        }

    }

    // generate vertices, triangles, and colors for one voxel
    void UpdateMeshData(Vector3 pos) {

        Vector3Int ipos = Vector3Int.FloorToInt(pos);
        VoxelState myState = chunkData.map[ipos.x, ipos.y, ipos.z];

        if(!myState.blockType.isSolid)
            return;

        // Copy vertices in voxelTris order
        for (int p=0; p<6; p++) {

            VoxelState neighbor = GetState(pos + VoxelData.faceChecks[p]);

            // suppress faces covered by other voxels
            if(neighbor.blockType.seeThrough) {

                FaceMeshData face = myState.blockType.meshData.faces[p];

                //float lightLevel = myState.lightAsFloat;
                float lightLevel = neighbor.lightAsFloat;
                int firstVert = vertices.Count;

                for(int i=0; i < face.vertices.Length; i++) {

                    vertices.Add(pos + face.vertices[i].position);
                    normals.Add(face.normal);
                    colors.Add(new Color(0,0,0, lightLevel));
                    AddTextureVert(myState.blockType.GetTextureID(p), face.vertices[i].uv);

                }

                for(int i=0; i < face.triangles.Length; i++) {

                    (!myState.blockType.seeThrough ? triangles : transparentTriangles).Add(
                        firstVert + face.triangles[i]
                    );

                }
            }
        }
    }

    void AddTextureVert(int textureID, Vector2 uv) {

        float y = textureID / VoxelData.TextureAtlasSizeInBlocks;
        float x = textureID - (y * VoxelData.TextureAtlasSizeInBlocks);

        x *= VoxelData.NormalizedBlockTextureSize;
        y *= VoxelData.NormalizedBlockTextureSize;

        y = 1f - y - VoxelData.NormalizedBlockTextureSize;

        x += VoxelData.NormalizedBlockTextureSize * uv.x;
        y += VoxelData.NormalizedBlockTextureSize * uv.y;

        uvs.Add(new Vector2(x,y));

    }

    // create Unity mesh objects from stored verts/tris
    public void CreateMesh() {

        if(freshMesh) {

            Vector3[] vertsA = null;
            int[] trisA = null;
            int[] transA = null;
            Vector2[] uvsA = null;
            Color[] colorsA = null;
            Vector3[] normalsA = null;

            lock (vertices) {

                vertsA = vertices.ToArray();
                trisA = triangles.ToArray();
                transA = transparentTriangles.ToArray();
                uvsA = uvs.ToArray();
                colorsA = colors.ToArray();
                normalsA = normals.ToArray();

                freshMesh = false;

            }

            Mesh mesh = new Mesh();
            mesh.vertices = vertsA;
            
            mesh.subMeshCount = 2;
            mesh.SetTriangles(trisA, 0);  //opaque material
            mesh.SetTriangles(transA, 1);  //transparent material

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

        for(int x=0; x<VoxelData.ChunkWidth; x++) {
            for(int y=0; y<VoxelData.ChunkHeight; y++) {
                for(int z=0; z<VoxelData.ChunkWidth; z++) {

                    chunkData.map[x,y,z].light = 0;

                }
            }
        }
    }

    public void RecalculateLight() {

        PurgeLight();
        Lighting.RecalculateNaturalLight(chunkData);
        //TODO ReplaceLights() -- add light sources

        bool changed = false;
        int safety = 18;

        do {

            changed = false;
            
            for(int x=0; x<VoxelData.ChunkWidth; x++) {
                for(int y=0; y<VoxelData.ChunkHeight; y++) {
                    for(int z=0; z<VoxelData.ChunkWidth; z++) {

                        VoxelState voxel = chunkData.map[x, y, z];

                        // if hasn't been lit
                        if (voxel.light == 0) {

                            // get current light
                            int light = voxel.light;

                            // find brightest neighbor
                            for(int p = 0; p < VoxelData.faceChecks.Length; p++) {

                                Vector3Int index = new Vector3Int(x,y,z) + VoxelData.faceChecks[p];
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
    }



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

        x = Mathf.FloorToInt(pos.x) / VoxelData.ChunkWidth;
        z = Mathf.FloorToInt(pos.z) / VoxelData.ChunkWidth;

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
