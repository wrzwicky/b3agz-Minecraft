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
    int vertexIndex = 0;
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

    // All the actual voxels for this chunk
    private VoxelState [,,] voxelMap = new VoxelState[VoxelData.ChunkWidth, VoxelData.ChunkHeight, VoxelData.ChunkWidth];
    World world;

    public bool isVoxelMapPopulated = false;  //explicit; not true til voxelMap is fully done
    bool _isActive;

    public Jobs<VoxelMod> modifications = new Jobs<VoxelMod>();

    // perform basic chunk init
    public Chunk(ChunkCoord _coord, World _world) {

        coord = _coord;
        origin = new Vector3(coord.x * VoxelData.ChunkWidth, 0f, coord.z * VoxelData.ChunkWidth);
        world = _world;
        _isActive = false;

    }

    // init Unity objects. must be called on game thread.
    public void InitUnity() {

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

            //Debug.Log("PopulateVoxelMap "+coord);
            for(int y=0; y<VoxelData.ChunkHeight; y++) {
                for(int x=0; x<VoxelData.ChunkWidth; x++) {
                    for(int z=0; z<VoxelData.ChunkWidth; z++) {

                        voxelMap[x,y,z] = new VoxelState( world.CreateVoxel(new Vector3(x,y,z) + origin, false));

                    }
                }
            }

            isVoxelMapPopulated = true;

            if(world.settings.enableAnimatedChunks && !world.settings.enableThreading)
                // only works if we're on the main thread
                chunkObject.AddComponent<ChunkLoadAnimation>();

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

        PopulateVoxelMap();

        bool modded = ApplyModifications() > 0;

        // need to (re)gen mesh?
        bool newMesh = true; // (modded || vertices.Count == 0);

        if(newMesh) {

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

    /// <summary>
    /// pos is relative to chunk origin;
    /// if pos is outside chunk, World is called to find appropriate chunk
    /// </summary>
    BlockType Voxel(Vector3 pos) {
        
        // mathematically more correct than just (int)
        Vector3Int v = Vector3Int.FloorToInt(pos);

        if (!IsVoxelInChunk(v))
            return world.Voxel(origin + pos);

        return world.blockTypes[voxelMap[v.x, v.y, v.z].id];

    }

    /// <summary>pos is relative to chunk origin</summary>
    VoxelState GetState(Vector3 pos) {
        
        // mathematically more correct than just (int)
        Vector3Int v = Vector3Int.FloorToInt(pos);

        if (!IsVoxelInChunk(v))
            return world.GetState(origin + pos);

        return voxelMap[v.x, v.y, v.z];

    }

    // pos is worldwide, but must be within this chunk
    public VoxelState GetVoxelFromGlobalPosition(Vector3 pos) {
        
        Vector3Int v = Vector3Int.FloorToInt(pos - origin);
        return voxelMap[v.x, v.y, v.z];

    }

    // pos is worldwide, but must be within this chunk
    public void SetVoxelFromGlobalPosition(Vector3 pos, byte id) {

        Vector3Int v = Vector3Int.FloorToInt(pos - origin);
        voxelMap[v.x, v.y, v.z].id = id;

    }

    // pos is worldwide
    public void EditVoxel(Vector3 pos, byte newID) {

        Vector3Int v = Vector3Int.FloorToInt(pos - origin);
        voxelMap[v.x, v.y, v.z].id = newID;

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

        vertexIndex = 0;
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
            CalculateLight_wrz();
            //CalculateLight_b3agz();

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
        VoxelState myState = voxelMap[ipos.x, ipos.y, ipos.z];
        BlockType myType = world.blockTypes[myState.id];

        if(!myType.isSolid)
            return;

        float lightLevel = myState.lightLevel;

        // Copy vertices in voxelTris order
        for (int p=0; p<6; p++) {
        
            VoxelState neighbor = GetState(pos + VoxelData.faceChecks[p]);

            // suppress faces covered by other voxels
            if(Voxel(pos+VoxelData.faceChecks[p]).seeThrough) {
        
                // 2-triangle strip
                vertices.Add(pos + VoxelData.voxelVerts[VoxelData.voxelTris[p,0]]);
                vertices.Add(pos + VoxelData.voxelVerts[VoxelData.voxelTris[p,1]]);
                vertices.Add(pos + VoxelData.voxelVerts[VoxelData.voxelTris[p,2]]);
                vertices.Add(pos + VoxelData.voxelVerts[VoxelData.voxelTris[p,3]]);

                for(int i=0; i<4; i++)
                    normals.Add(VoxelData.faceChecks[p]);

                AddTexture(myType.GetTextureID(p));

                colors.Add(new Color(0,0,0,lightLevel));
                colors.Add(new Color(0,0,0,lightLevel));
                colors.Add(new Color(0,0,0,lightLevel));
                colors.Add(new Color(0,0,0,lightLevel));

                if(!myType.seeThrough) {

                    triangles.Add(vertexIndex);
                    triangles.Add(vertexIndex+1);
                    triangles.Add(vertexIndex+2);
                    triangles.Add(vertexIndex+2);
                    triangles.Add(vertexIndex+1);
                    triangles.Add(vertexIndex+3);

                }
                else {

                    transparentTriangles.Add(vertexIndex);
                    transparentTriangles.Add(vertexIndex+1);
                    transparentTriangles.Add(vertexIndex+2);
                    transparentTriangles.Add(vertexIndex+2);
                    transparentTriangles.Add(vertexIndex+1);
                    transparentTriangles.Add(vertexIndex+3);

                }
                
                vertexIndex += 4;
            }
        }
    }

    void AddTexture(int textureID) {

        float y = textureID / VoxelData.TextureAtlasSizeInBlocks;
        float x = textureID - (y * VoxelData.TextureAtlasSizeInBlocks);

        x *= VoxelData.NormalizedBlockTextureSize;
        y *= VoxelData.NormalizedBlockTextureSize;

        y = 1f - y - VoxelData.NormalizedBlockTextureSize;

        uvs.Add(new Vector2(x, y));
        uvs.Add(new Vector2(x,y + VoxelData.NormalizedBlockTextureSize));
        uvs.Add(new Vector2(x + VoxelData.NormalizedBlockTextureSize, y));
        uvs.Add(new Vector2(x + VoxelData.NormalizedBlockTextureSize, y + VoxelData.NormalizedBlockTextureSize));

    }

    // create Unity mesh objects from stored verts/tris
    public void CreateMesh() {

        if(!freshMesh)
            return;

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

        mesh.normals = normalsA;
        //mesh.RecalculateNormals();

        meshFilter.mesh = mesh;

        if(!chunkObject.activeSelf && isActive) {

            //chunkObject.AddComponent<ChunkLoadAnimation>(); --doesn't work
            chunkObject.SetActive(isActive);

        }
    }

    // my simple version
    void CalculateLight_wrz() {

        for(int x=0; x<VoxelData.ChunkWidth; x++) {
            for(int z=0; z<VoxelData.ChunkWidth; z++) {

                float light = 1;

                for(int y=VoxelData.ChunkHeight-1; y >= 0; y--) {

                    VoxelState vox = voxelMap[x, y, z];
                    var blk = world.blockTypes[vox.id];

                    if(blk.isSolid) {
                        vox.lightLevel = light;
                        light *= world.blockTypes[vox.id].transparency;
                    }
                }
            }
        }
    }

    // b3agz' version
    void CalculateLight_b3agz() {

        Queue<Vector3Int> litVoxels = new Queue<Vector3Int>();

        for(int x=0; x<VoxelData.ChunkWidth; x++) {
            for(int z=0; z<VoxelData.ChunkWidth; z++) {

                float lightRay = 1f;

                for(int y=VoxelData.ChunkHeight - 1; y >= 0; y--) {

                    VoxelState thisVoxel = voxelMap[x,y,z];

                    if(thisVoxel.id > 0 && world.blockTypes[thisVoxel.id].transparency < lightRay)
                        lightRay = world.blockTypes[thisVoxel.id].transparency;

                    thisVoxel.lightLevel = lightRay;

                    // vox is bright enough to illuminate neighbors
                    if(lightRay > VoxelData.lightFalloff)
                        litVoxels.Enqueue(new Vector3Int(x,y,z));

                }
            }
        }

        while(litVoxels.Count > 0) {

            var v = litVoxels.Dequeue();

            for(int p=0; p<6; p++) {

                Vector3 currentVoxel = v + VoxelData.faceChecks[p];
                Vector3Int neighbor = Vector3Int.FloorToInt(currentVoxel);

                if(IsVoxelInChunk(neighbor)) {

                    if(voxelMap[neighbor.x, neighbor.y, neighbor.z].lightLevel
                        < voxelMap[v.x, v.y, v.z].lightLevel - VoxelData.lightFalloff) {

                        // vox is lit by neighbors
                        voxelMap[neighbor.x, neighbor.y, neighbor.z].lightLevel 
                            = voxelMap[v.x, v.y, v.z].lightLevel - VoxelData.lightFalloff;

                        // vox is bright enough to illuminate neighbors
                        if(voxelMap[neighbor.x, neighbor.y, neighbor.z].lightLevel > VoxelData.lightFalloff)
                            litVoxels.Enqueue(neighbor);

                    }
                }
            }
        }
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

public class VoxelState {
    
    /// <summary>block type id</summary>
    public byte id;
    /// <summary>How much light is falling on this block</summary>
    public float lightLevel;

    public VoxelState() {

        id = 0;
        lightLevel = 0;

    }

    public VoxelState(byte id) {

        this.id = id;
        this.lightLevel = 1f;

    }
}



/*
// old threading code

    /// <summary>generate/update/render as needed, on another thread</summary>
    public void UpdateChunkBackground() {

        Thread t = new Thread(new ThreadStart(UpdateChunk_thread));
        t.Start();

    }

    // thread worker
    void UpdateChunk_thread() {

        PopulateVoxelMap();  //only runs if new chunk
        //TODO re-queue self and neighbors if PopVM did something
        _updateChunk();
        world.chunksToDraw.Add(this);

    }



*/