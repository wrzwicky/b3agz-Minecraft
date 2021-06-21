using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using UnityEngine;


public class World : MonoBehaviour {

    public Settings settings;

    public BiomeAttributes[] biomes;

    public Transform player;
    public Vector3 spawnPosition;

    // texture for chunks
    public Material material;
    public Material transparentMaterial;
    public BlockType[] blockTypes;

    public GameObject debugScreen;

    //List<ChunkCoord> activeChunks = new List<ChunkCoord>();
    ChunkCoord playerLastChunk = null; //ensure first update

    //HashSet<ChunkCoord> chunksToCreate = new HashSet<ChunkCoord>();
    public Jobs<ChunkCoord> chunksToUpdate = new Jobs<ChunkCoord>();
    bool isUpdatingChunks;
    public Jobs<Chunk> chunksToDraw = new Jobs<Chunk>();

    // all voxel changes that have been requested
    public Jobs<VoxelMod> modifications = new Jobs<VoxelMod>();

    public GameObject creativeInventoryWindow;
    public GameObject cursorSlot;
    private bool _inUI = false;

    public Clouds clouds;

    Chunk[,] chunks = new Chunk[VoxelData.WorldSizeInChunks, VoxelData.WorldSizeInChunks];

    // - - - - - New WorldData system - - - - - //

    private static World _instance;
    public static World Instance { get { return _instance; } }

    public object chunkListThreadLock = new object();

    public string appPath;
    public WorldData worldData;

    // - - - - - - - - - - - - - - - - - - - - //


    [Header("Lighting")]
    [Range(0, 1f)]
    public float GlobalLightLevel;
    public Color day;
    public Color night;

    Thread chunkUpdateThread;
    public object chunkUpdateThreadLock = new object();


    private void Awake() {

        // capture singleton
        if(_instance != null && _instance != this)
            // destroy imposters
            Destroy(this.gameObject);
        else
            _instance = this;

        try {
            settings = Settings.LoadFile(Application.dataPath + "/settings.cfg");
        }
        catch(System.IO.IOException) {
            // just use the defaults from Unity editor
        }

        // copy app path so it's availble off thread
        appPath = Application.persistentDataPath;

        clouds.style = settings.clouds;

    }

    private void Start() {

        Shader.SetGlobalFloat("minGlobalLightLevel", VoxelData.minLightLevel);
        Shader.SetGlobalFloat("maxGlobalLightLevel", VoxelData.maxLightLevel);
        SetGlobalLightValue();

        spawnPosition = new Vector3(
            VoxelData.WorldCentre,
            VoxelData.ChunkHeight - 2,  //TODO crashes if y>chunkheight
            VoxelData.WorldCentre);
        // player.position = spawnPosition;

        // worldData is magically not null here, dunno what's going on
        string path = Path.Combine(appPath, "saves", worldData.worldName);
        worldData = SaveSystem.LoadWorld(path, VoxelData.seed);
        if(worldData == null) {
            Debug.Log("Generating new world using seed " + VoxelData.seed);
            worldData = new WorldData();
        }
        else
            Debug.Log("Loaded world from " + worldData.savePath);

        Random.InitState(worldData.seed);

        if(settings.enableThreading) {
            chunkUpdateThread = new Thread(new ThreadStart(ThreadedUpdate));
            chunkUpdateThread.Start();
        }

    }

    void Update() {

        ChunkCoord curchunk = GetChunkCoordFromPosition(player.position);

        // only update view if player changes chunks
        if(! curchunk.Equals(playerLastChunk)) {

            playerLastChunk = curchunk;
            CheckViewDistance_circle();
            clouds.UpdateClouds(player.position);

        }

        // apply any outstanding mods
        //ApplyModifications();

        if(settings.enableThreading) {
            // thread is already running!
        }
        else {

            //UpdateChunks_now();
            //UpdateChunks_one();
            UpdateChunks_later();

        }

        DrawChunks_now();

        if(Input.GetKeyDown(KeyCode.F3))
            debugScreen.SetActive(!debugScreen.activeSelf);

        if(Input.GetKeyDown(KeyCode.F5)) {

            string path = appPath + "/saves/" + worldData.worldName + "/";
            SaveSystem.SaveWorld(worldData, path);

        }
    }

    public void SetGlobalLightValue() {

        Shader.SetGlobalFloat("GlobalLightLevel", GlobalLightLevel);
        Camera.main.backgroundColor = Color.Lerp(night, day, GlobalLightLevel);

    }

    private void OnDisable() {

        if(chunkUpdateThread != null) {
            chunkUpdateThread.Abort();
            chunkUpdateThread = null;
        }
        
    }

    public bool inUI {

        get { return _inUI; }
        set {

            _inUI = value;

            if(_inUI) {
                Cursor.lockState = CursorLockMode.None;
            }
            else {
                Cursor.lockState = CursorLockMode.Locked;
            }

            Cursor.visible = _inUI;
            creativeInventoryWindow.SetActive(_inUI);
            cursorSlot.SetActive(_inUI);

        }
    }

    /// <summary>
    /// generate new chunks around player as needed, starting nearest player.
    /// deactivate chunks outside view distance
    /// this also covers b3agz's GenerateWorld
    /// </summary>
    void CheckViewDistance_circle() {

        ChunkCoord center = GetChunkCoordFromPosition(player.position);

        List<ChunkCoord> showChunks = new List<ChunkCoord>();
        List<ChunkCoord> hideChunks = new List<ChunkCoord>();

        // extra +/- 2 is to hide chunks as they go out of range
        for(int x = center.x - settings.viewDistance-2; x < center.x + settings.viewDistance+2; x++) {
            for(int z = center.z - settings.viewDistance-2; z < center.z + settings.viewDistance+2; z++) {

                ChunkCoord coord = new ChunkCoord(x,z);

                if(IsChunkInWorld(coord)) {

                    Vector2 playerRef = new Vector2(center.x * VoxelData.ChunkWidth, center.z * VoxelData.ChunkWidth); //player.position.x, player.position.z);
                    Vector2 chunkRef = new Vector2(x * VoxelData.ChunkWidth, z * VoxelData.ChunkWidth);
                    float dist = (playerRef - chunkRef).magnitude;

                    if(dist <= settings.viewDistance * VoxelData.ChunkWidth)
                        showChunks.Add(coord);
                    else if(chunks[x,z] != null)
                        hideChunks.Add(coord);
                }
            }
        }

        // Chunk too far -- make invisible, abort update
        foreach(ChunkCoord cc in hideChunks) {

            chunks[cc.x, cc.z].isActive = false;
            chunksToUpdate.Remove(cc);

        }

        // sort new chunks by distance from player
        showChunks.Sort((a,b) => {
            var dx = (a.x - center.x); dx *= dx;
            var dz = (a.z - center.z); dz *= dz;
            var da = dx + dz;

            dx = (b.x - center.x); dx *= dx;
            dz = (b.z - center.z); dz *= dz;
            var db = dx + dz;
            
            return da.CompareTo(db);
        });

        foreach(ChunkCoord cc in showChunks) {

            // build/update as needed
            if(chunks[cc.x, cc.z] == null) {
                chunks[cc.x, cc.z] = new Chunk(cc, this);
            }

            if(!chunks[cc.x, cc.z].isActive) {
                chunks[cc.x, cc.z].isActive = true;  //make visible
                chunksToUpdate.Add(cc);    //queue up a gen/update
            }
        }
    }

    /// <summary>
    /// generate new chunks around player as needed, left-right/forward-rear.
    /// deactivate chunks outside view distance
    /// this also covers b3agz's GenerateWorld
    /// </summary>
    void CheckViewDistance_scan() {

        ChunkCoord center = GetChunkCoordFromPosition(player.position);

        for(int x = center.x - settings.viewDistance-2; x < center.x + settings.viewDistance+2; x++) {
            for(int z = center.z - settings.viewDistance-2; z < center.z + settings.viewDistance+2; z++) {

                ChunkCoord coord = new ChunkCoord(x,z);

                if(IsChunkInWorld(coord)) {

                    Vector2 playerRef = new Vector2(player.position.x, player.position.z);
                    Vector2 chunkRef = new Vector2(x * VoxelData.ChunkWidth, z * VoxelData.ChunkWidth);
                    float dist = (playerRef - chunkRef).magnitude;

                    if(dist <= settings.viewDistance * VoxelData.ChunkWidth) {

                        // build/update as needed
                        if(chunks[x,z] == null) {
                            chunks[x,z] = new Chunk(coord, this);
                        }

                        if(!chunks[x,z].isActive) {
                            chunks[x,z].isActive = true;  //make visible
                            chunksToUpdate.Add(coord);    //queue up a gen/update
                        }
                    }

                    // Chunk too far -- make invisible, abort update
                    else if(chunks[x,z] != null) {

                        chunks[x,z].isActive = false;
                        chunksToUpdate.Remove(coord);

                    }
                }
            }
        }

    }

    // move global voxel mods into appropriate chunks
    // flush invalid mods
    // return set of Chunk objects that have received mods
    bool ApplyModifications() {

        bool hadMods = false;

        // process all registered worldwide voxel mods
        while(true) {

            VoxelMod m = modifications.Any();
            if(m == null)
                break;
            ChunkCoord c = GetChunkCoordFromPosition(m.position);

            // mod might be outside world if it blindly generates voxel coords
            if(IsChunkInWorld(c)) {

                // make chunk stub if needed
                if(chunks[c.x, c.z] == null)
                    chunks[c.x, c.z] = new Chunk(c, this);

                // pass the mod to the chunk
                chunks[c.x, c.z].modifications.Add(m);

                // notate that chunk is modified
                chunksToUpdate.Add(c);
                hadMods = true;

            }
        }

        return hadMods;

    }

    void ThreadedUpdate() {

        while(true) {

            if(!UpdateChunks_one())
                Thread.Sleep(16);

        }
    }

    void UpdateChunks_later() {

        if((modifications.Count > 0 || chunksToUpdate.Count > 0) && !isUpdatingChunks) {

            StartCoroutine(UpdateChunks_coro());

        }

    }

    void UpdateChunks_now() {

        var i = UpdateChunks_coro();
        while(i.MoveNext()) ;

    }

    /// <summary>
    /// coroutine to update chunksToUpdate, one at a time.
    /// be sure to set
    ///   isCreatingChunks = true
    /// after starting one of these; it might be a while before we can run and set it ourselves!
    /// </summary>
    // b3agz calls it CreateChunks
    IEnumerator UpdateChunks_coro() {

        if(isUpdatingChunks)
            yield break;
        isUpdatingChunks = true;

        // return now; let coro do chunks later
        // --needed with old threading mods, don't know why
        yield return null;

        while (UpdateChunks_one()) {

            yield return null;

        }

        isUpdatingChunks = false;

    }

    bool UpdateChunks_one() {

        // apply any outstanding mods
        ApplyModifications();

        ChunkCoord coord = chunksToUpdate.Any();
        if(coord == null)
            return false;

        Debug.Log("Update "+coord);
        Chunk c = chunks[coord.x, coord.z];

        // generate this chunk
        c.UpdateChunk();

        return true;

    }

    void DrawChunks_now() {

        // cancel draw if still needs update
        foreach(ChunkCoord cc in chunksToUpdate)
            chunksToDraw.Remove(chunks[cc.x, cc.z]);

        while(true) {

            // update gameobject for one chunk
            Chunk c = chunksToDraw.Any();
            if(c == null)
                break;

            c.InitForUnity();
            c.CreateMesh();

        }
    }

    bool IsChunkInWorld(ChunkCoord coord) {

        return (coord.x >= 0 && coord.x < VoxelData.WorldSizeInChunks
            && coord.z >= 0 && coord.z < VoxelData.WorldSizeInChunks);

    }

    // return true if pos is within range defined in VoxelData to have voxels
    bool IsVoxelInWorld(Vector3 pos) {

        return (pos.x >= 0 && pos.x < VoxelData.WorldSizeInVoxels
            && pos.y >= 0 && pos.y < VoxelData.ChunkHeight
            && pos.z >= 0 && pos.z < VoxelData.WorldSizeInVoxels);

    }

    // generate new voxel for pos in world
    // fake=true means do min work to find if pos is solid or not. do not queue mods!
    // was GetVoxel
    public byte CreateVoxel(Vector3 pos, bool fake) {

        int yPos = Mathf.FloorToInt(pos.y);

        // -- IMMUTABLE PASS -- //

        // outside world -> air
        if(!IsVoxelInWorld(pos))
            return 0;

        // bottom of chunk -> bedrock
        if(pos.y <= 0)
            return 1; //bedrock

        // -- BIOME SELECTION PASS -- //

        int solidGroundHeight = 42;
        float sumOfHeight = 0;
        int count = 0;
        float strongestWeight = 0f;
        int strongestIndex = 0;

        for(int i=0; i < biomes.Length; i++) {

            // choose 'strongest' biome for this voxel
            float weight = Noise.Get2DPerlin(new Vector2(pos.x, pos.z), biomes[i].offset, biomes[i].scale);
            if(weight > strongestWeight) {

                strongestWeight = weight;
                strongestIndex = i;

            }

            // height is average of all biomes, for smoothness
            float height = biomes[i].terrainHeight * Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 0, biomes[i].terrainScale) * weight;
            if(height > 0) {
                sumOfHeight += height;
                count++;
            }

        }

        BiomeAttributes biome = biomes[strongestIndex];

        int terrainHeight = Mathf.FloorToInt(sumOfHeight/count + solidGroundHeight);


        // -- BASIC TERRAIN PASS -- //

        byte voxelValue = 0;

        if(yPos == terrainHeight)
            voxelValue = biome.surfaceBlock;
        else if(yPos < terrainHeight && yPos > terrainHeight - 4)
            voxelValue = biome.subSurfaceBlock;
        else if(yPos > terrainHeight)
            return 0; //air
        else
            voxelValue = 2; //stone

        if(fake)
            return voxelValue;

        // -- SECOND PASS -- //

        if(voxelValue == 2) {
            foreach(Lode lode in biome.lodes) {
                if(yPos > lode.minHeight && yPos < lode.maxHeight) {
                    if(Noise.Get3DPerlin(pos, lode.noiseOffset, lode.scale, lode.threshold))

                        voxelValue = lode.blockID;

                }
            }
        }

        // -- MAJOR FLORA PASS -- //

        // trees can only sprout on surface
        if(yPos == terrainHeight && biome.placeMajorFlora) {

            // make patches that can be forest-y
            if(Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 123, biome.majorFloraZoneScale) > biome.majorFloraZoneTheshold) {

                // within patches, make trees
                voxelValue = biome.zoneSurfaceBlock;
                if(Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 123, biome.majorFloraPlacementScale) > biome.majorFloraPlacementThreshold) {

                    Structure.GenerateMajorFlora(biome.majorFloraIndex, pos, modifications, biome.minHeight, biome.maxHeight, biome.headSize);

                }
            }
        }

        return voxelValue;

    }

    // return world location of origin of chunk which contains position 'pos'
    public ChunkCoord GetChunkCoordFromPosition(Vector3 pos) {

        int x = Mathf.FloorToInt(pos.x / VoxelData.ChunkWidth);
        int z = Mathf.FloorToInt(pos.z / VoxelData.ChunkWidth);
        return new ChunkCoord(x,z);

    }

    public Chunk GetChunkFromPosition(Vector3 pos) {

        int x = Mathf.FloorToInt(pos.x / VoxelData.ChunkWidth);
        int z = Mathf.FloorToInt(pos.z / VoxelData.ChunkWidth);
        return chunks[x,z];

    }

    // pos is worldwide
    // returns blockType, or BlockType.NOTHING if pos is outside world
    // or generates voxel if not generated yet
    public BlockType Voxel(Vector3 pos) {

        ChunkCoord thisChunk = new ChunkCoord(pos);

        // air outside world
        if(!IsVoxelInWorld(pos))
            return BlockType.NOTHING;

        // return voxel if generated
        if(chunks[thisChunk.x, thisChunk.z] != null && chunks[thisChunk.x,thisChunk.z].isVoxelMapPopulated)
            return blockTypes[chunks[thisChunk.x, thisChunk.z].GetVoxelFromGlobalPosition(pos).id];

        // generate (but dont save) if not
        return blockTypes[CreateVoxel(pos, true)];

    }

    public VoxelState GetState(Vector3 pos) {

        ChunkCoord thisChunk = new ChunkCoord(pos);

        // air outside world
        if(!IsVoxelInWorld(pos))
            return new VoxelState(0);

        // return voxel if generated
        if(chunks[thisChunk.x, thisChunk.z] != null && chunks[thisChunk.x,thisChunk.z].isVoxelMapPopulated)
            return chunks[thisChunk.x, thisChunk.z].GetVoxelFromGlobalPosition(pos);

        // generate (but dont save) if not
        return new VoxelState(1);

    }

}

[System.Serializable]
public class BlockType {

    public static BlockType NOTHING;

    static BlockType() {
        NOTHING = new BlockType();
        NOTHING.blockName = "__empty__";
        NOTHING.isSolid = false;
        NOTHING.seeThrough = true;
    }

    /// <summary>descriptive name for contents of block</summary>
    public string blockName;
    /// <summary>true if there is anything at all in this block; false if fully empty</summary>
    public bool isSolid;
    /// <summary>true if block is transparent, translucent, or otherwise does not fully block view of neighbor blocks</summary>
    public bool seeThrough;
    public float transparency;
    public Sprite icon;
    
    [Header ("Texture Values")]
    public int backFaceTexture;
    public int frontFaceTexture;
    public int topFaceTexture;
    public int bottomFaceTexture;
    public int leftFaceTexture;
    public int rightFaceTexture;

    // Back, Front, Top, Bottom, Left, Right

    public int GetTextureID(int faceIndex) {

        switch(faceIndex) {
            
            case 0:
                return backFaceTexture;
            case 1:
                return frontFaceTexture;
            case 2:
                return topFaceTexture;
            case 3:
                return bottomFaceTexture;
            case 4:
                return leftFaceTexture;
            case 5:
                return rightFaceTexture;
            default:
                Debug.Log("Error in GetTextureID; invalid face index "+faceIndex);
                return 0;

        }
    }
}


public abstract class VoxelMod {

    public Vector3 position;

    // true if something actually changed
    public abstract bool Apply(Chunk chunk);

}

/// <summary>
/// is a request to place a specific voxel at a position in world,
/// after appropriate chunk is generated.
/// only air is replaced; other voxels are unchanged.
/// </summary>
public class AddVoxelMod : VoxelMod {

    public byte id;

    public AddVoxelMod() {

        position = new Vector3();
        id = 0;

    }

    /// <summary>
    /// pos = 3D world position of block
    /// id = block ID
    /// </summary>
    public AddVoxelMod(Vector3 _pos, byte _id) {

        position = _pos;
        id = _id;

    }

    public override bool Apply(Chunk chunk) {

        if(chunk.GetVoxelFromGlobalPosition(position).id == 0) {
            chunk.SetVoxelFromGlobalPosition(position, id);
            return true;
        }
        else
            return false;

    }
}


/// <summary>
/// is a request to replace a specific voxel at a position in world,
/// after appropriate chunk is generated
/// </summary>
public class ReplaceVoxelMod : VoxelMod {

    public byte id;

    public ReplaceVoxelMod() {

        position = new Vector3();
        id = 0;

    }

    /// <summary>
    /// pos = 3D world position of block
    /// id = block ID
    /// </summary>
    public ReplaceVoxelMod(Vector3 pos, byte id) {

        position = pos;
        this.id = id;

    }

    public override bool Apply(Chunk chunk) {

        var old = chunk.GetVoxelFromGlobalPosition(position).id;
        
        if(old != this.id) {
            chunk.SetVoxelFromGlobalPosition(position, id);
            return true;
        }
        else
            return false;

    }
}



/*
// Old threading code

in Update():
            // StartCoroutine(UpdateChunks_coro());
            // isCreatingChunks = true;

    /// <summary>coroutine to update chunksToUpdate, one at a time</summary>
    // be sure to set
    //   isCreatingChunks = true
    // after starting one of these; it might be a while before we can run and set it ourselves!
    // b3agz calls it CreateChunks
    IEnumerator UpdateChunks_coro() {

        if(isCreatingChunks)
            yield break;
        isCreatingChunks = true;

        // apply any outstanding mods
yield return null;  //--needed with the threading mods, don't know why
        ApplyModifications();

        while(true) {

            ChunkCoord coord = chunksToUpdate.Any();
            if(coord == null)
                break;

            Chunk newc = chunks[coord.x, coord.z];

            //Chunk newc = chunks[chunksToCreate[0].x, chunksToCreate[0].z];
            // chunksToCreate.RemoveAt(0);

            // generate this chunk
            newc.UpdateChunk();

            // spread new mods
            //HashSet<Chunk> ch = ApplyModifications(); -- not work with threaded Update
            // re-queue this chunk if changed
            // if(ch.Contains(newc))
            //     chunksToUpdate.Add(newc.coord);

            // string s = "HashSet[";
            // foreach(Chunk c in ch) {
            //     s += c.ToString()+", ";
            // }
            // s += "]";
            // Debug.Log("New mods for: "+s);

            //TODO still not right; we want to queue all changed chunks that are in view distance

            yield return null;   //--not needed with threading mods

        }

        //This actually works fine, and just updates the view area.
        //It *should* pull in the whole world due to tree on the edges, but doesn't.
        //I don't know why. Maybe just lucky that not many trees are on edges?
        // HashSet<Chunk> ch = SpreadMods();
        // foreach(Chunk c in ch)
        //     chunksToCreate.Add(c.coord);

        isCreatingChunks = false;

    }



*/



[System.Serializable]
public class Settings {

    [Header("Game Data")]
    public string version = "20x1.0.0r3";

    [Header("Performance")]
    public int loadDistance = 16;
    public int viewDistance = 8;
    public CloudStyle clouds = CloudStyle.Fast;
    public bool enableThreading = true;
    public bool enableAnimatedChunks = false;

    [Header("Controls")]
    [Range(0.1f, 10f)]
    public float mouseSensitivity = 1;

    public void SaveFile(string path) {

        string jsonExport = JsonUtility.ToJson(this);
        File.WriteAllText(path, jsonExport);

    }

    public static Settings LoadFile(string path) {

        string jsonImport = File.ReadAllText(path);
        return JsonUtility.FromJson<Settings>(jsonImport);

    }
}
