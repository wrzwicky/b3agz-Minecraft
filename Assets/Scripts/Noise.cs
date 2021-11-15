using UnityEngine;
public static class Noise {

    public static float Get2DPerlin(Vector2 position, float offset, float scale) {

        float px = position.x + offset + GameData.seed + 0.1f;
        float py = position.y + offset + GameData.seed + 0.1f;

        // Unity's perlin generator is unhappy at int boundaries
        return Mathf.PerlinNoise(
            px / GameData.ChunkWidth * scale,
            py / GameData.ChunkWidth * scale);

    }

    public static bool Get3DPerlin(Vector3 position, float offset, float scale, float threshold) {

        // from "Easy 3D Perlin Noise" by Carlpilot v=Aga0TBJkchM

        float x = (position.x + offset + GameData.seed + 0.1f) * scale;
        float y = (position.y + offset + GameData.seed + 0.1f) * scale;
        float z = (position.z + offset + GameData.seed + 0.1f) * scale;

        // Get all three permutations of noise for X, Y, and Z
        float XY = Mathf.PerlinNoise(x, y);
        float YZ = Mathf.PerlinNoise(y, z);
        float XZ = Mathf.PerlinNoise(x, z);

        // And their reverses ...
        float YX = Mathf.PerlinNoise(y, x);
        float ZY = Mathf.PerlinNoise(z, y);
        float ZX = Mathf.PerlinNoise(z, x);

        // And return the average.
        return ((XY+YZ+XZ+YX+ZY+ZX) / 6f > threshold);

    }

}