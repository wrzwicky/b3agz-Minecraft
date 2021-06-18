using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DebugScreen : MonoBehaviour
{
    World world;
    Text text;

    float frameRate;
    float frames;
    float timer;

    // Start is called before the first frame update
    void Start() {

        world = GameObject.Find("World").GetComponent<World>();
        text = GetComponent<Text>();
        
    }

    // Update is called once per frame
    void Update() {

        if(timer > 1f) {

            frameRate = Mathf.RoundToInt(frames / timer);
            //(int)(1f / Time.unscaledDeltaTime);
            frames = 0;
            timer = 0;

        }
        else {

            frames++;
            timer += Time.deltaTime;

        }

        string debugText = "b3agz' Code a Game Like Minecraft in Unity";
        debugText += "\n\n" + frameRate + " fps";
        debugText += "\nVoxel: " + Mathf.FloorToInt(world.player.transform.position.x) + ", "
                + Mathf.FloorToInt(world.player.transform.position.y) + ", "
                + Mathf.FloorToInt(world.player.transform.position.z);
        debugText += "\nChunk: " + world.GetChunkCoordFromPosition(world.player.transform.position);
        text.text = debugText;
        
    }
}
