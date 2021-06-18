using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

public class AtlasPacker : EditorWindow {

    int atlasSizeInBlocks = 16;
    int blockSizeInPixels = 16;
    int atlasSize;

    List<Texture2D> sortedTexture = new List<Texture2D>();
    // generated result
    Texture2D atlas;

    [MenuItem("b3agz Minecraft/Atlas Packer")]
    private static void ShowWindow() {
        //EditorWindow.GetWindow(typeof(AtlasPacker));
        var window = GetWindow<AtlasPacker>();
        window.titleContent = new GUIContent("AtlasPacker");
        window.Show();
    }

    private void OnGUI() {
        
        atlasSize = blockSizeInPixels * atlasSizeInBlocks;
        GUILayout.Label("Texture Atlas Packer", EditorStyles.boldLabel);

        blockSizeInPixels = EditorGUILayout.IntField("Block Size", blockSizeInPixels);
        atlasSizeInBlocks = EditorGUILayout.IntField("Atlas Size", atlasSizeInBlocks);

        GUILayout.Label(atlas);

        if(GUILayout.Button("Load Textures")) {
            
            Debug.Log("Atlas Packer: button pressed");
            LoadTextures();
            PackAtlas();

            Debug.Log("Atlas Packer: textures packed");

        }

        if(GUILayout.Button("Clear Textures")) {

            atlas = new Texture2D(atlasSize, atlasSize);
            Debug.Log("Atlas Packer: textures cleared");

        }

        if(GUILayout.Button("Save Atlas")) {

            byte[] bytes = atlas.EncodeToPNG();

            try {

                File.WriteAllBytes(Application.dataPath + "/Textures/Packed_Atlas.png", bytes);
                Debug.Log("Atlas Packer: atlas saved");

            }
            catch {

                Debug.Log("Atlas Packer: Couldn't save atlas to file.");

            }

        }

    }

    void LoadTextures() {

        object[] rawTextures = Resources.LoadAll("AtlasPacker", typeof(Texture2D));

        sortedTexture.Clear();

        int index = 0;
        foreach(object tex in rawTextures) {

            Texture2D t = (Texture2D)tex;

            if(t.width == blockSizeInPixels && t.height == blockSizeInPixels)
                sortedTexture.Add(t);
            else
                Debug.Log("Atlas Packer: \""+t.name+"\" incorrect size. Texture not loaded.");

            index++;

        }

        Debug.Log("Atlas Packer: "+sortedTexture.Count+" textures loaded");

    }

    void PackAtlas() {

        atlas = new Texture2D(atlasSize, atlasSize);
        Color[] pixels = new Color[atlasSize * atlasSize];

        for(int x = 0; x < atlasSize; x++) {
            for(int y = 0; y < atlasSize; y++) {

                // which block?
                int blockX = x / blockSizeInPixels;
                int blockY = y / blockSizeInPixels;
                int index = blockY * atlasSizeInBlocks + blockX;

                // which pixel in block?
                int pixelX = x - (blockX * blockSizeInPixels);
                int pixelY = y - (blockY * blockSizeInPixels);

                if(index < sortedTexture.Count)
                    pixels[(atlasSize - y - 1) * atlasSize + x] = sortedTexture[index].GetPixel(x, blockSizeInPixels - y - 1);
                else
                    pixels[(atlasSize - y - 1) * atlasSize + x] = new Color(0,0,0,0);

           }
        }

        atlas.SetPixels(pixels);
        atlas.Apply();

    }
}
