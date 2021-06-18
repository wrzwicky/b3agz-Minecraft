using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public enum CloudStyle {

    Off, Fast, Fancy
}

// create cloud chunks from cloudPattern texture
// move them around as player moves
public class Clouds : MonoBehaviour
{

    public int cloudHeight = 100;
    public int cloudDepth = 4;

    [SerializeField] private Texture2D cloudPattern = null;
    [SerializeField] private Material cloudMaterial = null;

    [NonSerialized]
    [HideInInspector] public CloudStyle style = CloudStyle.Off;

    bool[,] cloudData;  //true if cloud

    int cloudTexWidth = 0;

    int cloudTileSize;
    Vector3Int offset;

    Dictionary<Vector2Int, GameObject> clouds = new Dictionary<Vector2Int, GameObject>();


    // Start is called before the first frame update
    void Start() {

        cloudTexWidth = cloudPattern.width;
        cloudTileSize = VoxelData.ChunkWidth;
        offset = new Vector3Int(-cloudTexWidth / 2, 0, -cloudTexWidth / 2);

        transform.position = new Vector3(VoxelData.WorldCentre, cloudHeight, VoxelData.WorldCentre);

        LoadCloudData();
        CreateClouds();

    }

    // Update is called once per frame
    void Update() {
        
    }

    public void UpdateClouds(Vector3 center) {

        if(cloudData == null)
            return;

        for(int x=0; x<cloudTexWidth; x += cloudTileSize) {
            for(int y=0; y<cloudTexWidth; y += cloudTileSize) {

                Vector3 pos = center + new Vector3(x, 0, y) + offset;
                pos = new Vector3(RoundToCloud(pos.x), cloudHeight, RoundToCloud(pos.z));
                Vector2Int cloudPos = CloudTilePosFromV3(pos);

                clouds[cloudPos].transform.position = pos;

            }
        }
    }

    private void LoadCloudData() {

        if(style == CloudStyle.Off)
            return;

        cloudData = new bool[cloudTexWidth, cloudTexWidth];
        Color[] cloudPix = cloudPattern.GetPixels();

        for(int x=0; x<cloudTexWidth; x++) {
            for(int y=0; y<cloudTexWidth; y++) {

                int i = y * cloudTexWidth + x;
                cloudData[x, y] = (cloudPix[i].a > 0);

            }
        }
    }

    // origin of cloud tile containing 'value'
    private int RoundToCloud(float value) {

        return Mathf.FloorToInt(value / cloudTileSize) * cloudTileSize;

    }

    // create all 1024 cloud objects
    // note - are created centered around (0,0). Call UpdateClouds to fix!
    private void CreateClouds() {

        if(style == CloudStyle.Off)
            return;

        for(int x=0; x<cloudTexWidth; x += cloudTileSize) {
            for(int y=0; y<cloudTexWidth; y += cloudTileSize) {

                Mesh mesh;
                if(style == CloudStyle.Fast)
                    mesh = CreateCloudMesh_Fast(x, y);
                else
                    mesh = CreateCloudMesh_Fancy(x, y);

                Vector3 pos = new Vector3(x, cloudHeight, y);
                clouds.Add(CloudTilePosFromV3(pos),
                    CreateCloudTile(mesh, new Vector3(x, 0, y) + transform.position + offset));

            }
        }
    }

    private Mesh CreateCloudMesh_Fast(int x, int z) {

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector3> normals = new List<Vector3>();
        int vertCount = 0;

        for(int xi = 0; xi < cloudTileSize; xi ++) {
            for(int zi = 0; zi < cloudTileSize; zi ++) {

                int xVal = x + xi;
                int zVal = z + zi;

                if(cloudData[xVal, zVal]) {

                    // pos relative to gameobject pos
                    vertices.Add(new Vector3(xi, 0, zi));
                    vertices.Add(new Vector3(xi, 0, zi+1));
                    vertices.Add(new Vector3(xi+1, 0, zi+1));
                    vertices.Add(new Vector3(xi+1, 0, zi));

                    for(int i=0; i<4; i++)
                        normals.Add(Vector3.down);

                    // first tri
                    triangles.Add(vertCount + 1);
                    triangles.Add(vertCount);
                    triangles.Add(vertCount + 2);
                    // second tri
                    triangles.Add(vertCount + 2);
                    triangles.Add(vertCount);
                    triangles.Add(vertCount + 3);

                    vertCount += 4;

                }
            }
        }

        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.normals = normals.ToArray();

        return mesh;

    }

    private Mesh CreateCloudMesh_Fancy(int x, int z) {

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector3> normals = new List<Vector3>();
        int vertCount = 0;

        for(int xi = 0; xi < cloudTileSize; xi ++) {
            for(int zi = 0; zi < cloudTileSize; zi ++) {

                int xVal = x + xi;
                int zVal = z + zi;

                if(cloudData[xVal, zVal]) {

                    for(int p=0; p < 6; p++) {

                        if(!CheckCloudData(new Vector3Int(xVal, 0, zVal) + VoxelData.faceChecks[p])) {

                            // add 4 verts for this face
                            for(int i = 0; i < 4; i++) {

                                Vector3 vert = new Vector3Int(xi, 0, zi);
                                vert += VoxelData.voxelVerts[VoxelData.voxelTris[p, i]];
                                vert.y *= cloudDepth;
                                vertices.Add(vert);

                                normals.Add(VoxelData.faceChecks[p]);

                            }

                            triangles.Add(vertCount);
                            triangles.Add(vertCount+1);
                            triangles.Add(vertCount+2);
                            triangles.Add(vertCount+2);
                            triangles.Add(vertCount+1);
                            triangles.Add(vertCount+3);

                            vertCount += 4;

                        }
                    }
                }
            }
        }

        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.normals = normals.ToArray();

        return mesh;

    }

    // true if cloud at given point
    // clouds are 2d, so false if y != 0
    private bool CheckCloudData(Vector3Int point) {

        if(point.y != 0)
            return false;

        int x = point.x;
        int z = point.z;

        if(x < 0)
            x = cloudTexWidth - 1;
        else if(point.x > cloudTexWidth - 1)
            x = 0;

        if(z < 0)
            z = cloudTexWidth - 1;
        else if(z > cloudTexWidth - 1)
            z = 0;

        return cloudData[x, z];

    }

    /// <summary>
    /// Create a chunk of sky.
    /// </summary>
    private GameObject CreateCloudTile(Mesh mesh, Vector3 position) {

        GameObject newCloudTile = new GameObject();
        newCloudTile.transform.position = position;
        newCloudTile.transform.parent = transform;
        newCloudTile.name = "Cloud " + position.x + ", " + position.z;

        MeshFilter mf = newCloudTile.AddComponent<MeshFilter>();
        MeshRenderer mr = newCloudTile.AddComponent<MeshRenderer>();

        mr.material = cloudMaterial;
        mf.mesh = mesh;

        return newCloudTile;

    }

    private Vector2Int CloudTilePosFromV3(Vector3 pos) {

        return new Vector2Int(CloudTileCoordFromFloat(pos.x), CloudTileCoordFromFloat(pos.z));

    }

    private int CloudTileCoordFromFloat(float value) {

        // just value % cloudTexWidth
        float a = value / (float)cloudTexWidth; // get pos using cloudTexWidth as 1 unit
        a -= Mathf.FloorToInt(a);  // get pos within texture
        int b = Mathf.FloorToInt((float)cloudTexWidth * a);

        return b;

    }
}
