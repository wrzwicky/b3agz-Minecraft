using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using UnityEngine;

public static class SaveSystem {

    public static void SaveWorld(WorldData world, string worldPath) {

        Debug.Log("Saving world " + world.worldName + " to " + worldPath);

        string filePath = Path.Combine(worldPath, "world.b3agz");

        if(!Directory.Exists(worldPath))
            Directory.CreateDirectory(worldPath);

        world.savePath = worldPath;

        BinaryFormatter formatter = new BinaryFormatter();
        FileStream stream = new FileStream(filePath, FileMode.Create);
        formatter.Serialize(stream, world);
        stream.Close();

        Thread thread = new Thread(() => SaveChunks(world, worldPath));
        thread.Start();

    }

    public static void SaveChunks(WorldData world, string path) {

        int count = 0;
        while(true) {

            ChunkData c = world.modifiedChunks.Any();
            if(c == null)
                break;
            SaveSystem.SaveChunk(c, path);
            count ++;

        }

        Debug.Log("Saved " + count + " chunks");
        
    }

    // path = folder containing the world
    public static WorldData LoadWorld(string worldPath, int seed=0) {

        string filePath = Path.Combine(worldPath, "world.b3agz");

        if(File.Exists(filePath)) {

            Debug.Log("Loading world from " + filePath);

            BinaryFormatter formatter = new BinaryFormatter();
            FileStream stream = new FileStream(filePath, FileMode.Open);
            WorldData world = formatter.Deserialize(stream) as WorldData;
            stream.Close();

            world.savePath = worldPath;

            return world;

        }
        else
            Debug.Log("No saved world found at " + worldPath);

        return null;
        
    }

    // chunk = what to save
    // path = folder of world with trailing /
    public static void SaveChunk(ChunkData chunk, string worldPath) {

        string chunkPath = Path.Combine(worldPath, "chunks");
        string fileName = chunk.position.x + "-" + chunk.position.y + ".chunk";
        string filePath = Path.Combine(chunkPath, fileName);

        if(!Directory.Exists(chunkPath))
            Directory.CreateDirectory(chunkPath);

        Debug.Log("Saving chunk to " + filePath);

        BinaryFormatter formatter = new BinaryFormatter();
        FileStream stream = new FileStream(filePath, FileMode.Create);
        formatter.Serialize(stream, chunk);
        stream.Close();

    }

    public static ChunkData LoadChunk(string worldPath, Vector2Int position) {

        string chunkPath = Path.Combine(worldPath, "chunks");
        string fileName = position.x + "-" + position.y + ".chunk";
        string filePath = Path.Combine(chunkPath, fileName);

        if(File.Exists(filePath)) {

            Debug.Log("Loading chunk from " + filePath);

            BinaryFormatter formatter = new BinaryFormatter();
            FileStream stream = new FileStream(filePath, FileMode.Open);
            ChunkData chunkData = formatter.Deserialize(stream) as ChunkData;
            stream.Close();

            return chunkData;

        }
        else
            return null;

    }
}