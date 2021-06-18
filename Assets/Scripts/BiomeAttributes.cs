using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BiomeAttributes", menuName = "MinecraftTutorial/Biome Attributes")]
public class BiomeAttributes : ScriptableObject {

    [Header("Header")]
    public string biomeName;
    public int offset;
    public float scale;

    // distance from solid ground to highest terrain peak
    public int terrainHeight;
    public float terrainScale;

    [Tooltip("Block ID for surface layer (i.e. grass)")]
    public byte surfaceBlock;
    [Tooltip("Block ID for layers under surface (i.e. dirt)")]
    public byte subSurfaceBlock;

    [Header("Major Flora")]
    [Tooltip("Whether biome has any major flora at all")]
    public bool placeMajorFlora = true;
    [Tooltip("Block ID for surface layer in areas where major flora might bloom")]
    public byte zoneSurfaceBlock;
    [Tooltip("Code number for type of flora to generate")]
    public int majorFloraIndex;
    public float majorFloraZoneScale = 1.3f;
    [Range(0, 1f)]
    public float majorFloraZoneTheshold = 0.6f;

    public float majorFloraPlacementScale = 15f;
    [Range(0, 1f)]
    public float majorFloraPlacementThreshold = 0.8f;

    public int minHeight = 5;
    public int maxHeight = 12;
    [Tooltip("Size of the 'head' of the plant")]
    public float headSize = 3.5f;

    public Lode[] lodes;

}

[System.Serializable]
public class Lode {

    public string lodeName;
    public byte blockID; //blockType code from World.blockTypes
    public int minHeight;
    public int maxHeight;
    public float scale;
    public float threshold;
    public float noiseOffset;

}