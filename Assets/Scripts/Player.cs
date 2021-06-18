using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// all custom processing; Unity collision too slow for our constantly changing meshes

public class Player : MonoBehaviour
{

    public float walkSpeed = 3f;
    public float sprintSpeed = 6f;
    public float jumpForce = 5f;
    public float gravity = -9.8f;

    public float playerWidth = 0.15f; //radius
    public float playerHeight = 1.8f; //typical 1.8m human height
    public float boundsTolerance = 0.1f;

    public bool isGrounded;
    public bool isSprinting;

    public Transform blockHighlight;
    public Transform blockPlacer;
    public float checkIncrement = 0.1f;
    public float reach = 8f;

    //public byte selectedBlockIndex = 1;
    public Toolbar toolbar;

    Transform cam;
    World world;

    float horizontal;
    float vertical;
    float mouseHorizontal;
    float mouseVertical;
    Vector3 velocity;
    float verticalMomentum = 0;
    bool jumpRequest;

    // Start is called before the first frame update
    void Start() {

        cam = GameObject.Find("Main Camera").transform;
        world = GameObject.Find("World").GetComponent<World>();

        world.inUI = false;

    }

    // Update is called once per frame
    void Update() {

        if(Input.GetKeyDown(KeyCode.I)) {

            world.inUI = !world.inUI;
            //TODO if cursorSlot holding something, put it away!

        }

        if(!world.inUI) {

            GetPlayerInputs();

            transform.Rotate(Vector3.up * mouseHorizontal * world.settings.mouseSensitivity);
            cam.Rotate(Vector3.right * mouseVertical * world.settings.mouseSensitivity);

            PlaceCursorBlocks();

        }
    }

    void FixedUpdate() {

        if(!world.inUI) {

            if(jumpRequest)
                Jump();
            CalculateVelocity();
            transform.Translate(velocity, Space.World);
        
        }
    }

    void Jump() { 
        verticalMomentum = jumpForce;
        isGrounded = false;
        jumpRequest = false;
    }

    void CalculateVelocity() {

        if(verticalMomentum > gravity)
            verticalMomentum += gravity * Time.fixedDeltaTime;
        
        velocity = ((transform.forward * vertical) + (transform.right * horizontal)) * Time.fixedDeltaTime * (isSprinting ? sprintSpeed : walkSpeed);

        velocity += Vector3.up * verticalMomentum * Time.fixedDeltaTime;

        if((velocity.z > 0 && front) || (velocity.z < 0 && back))
            velocity.z = 0;
        if((velocity.x > 0 && right) || (velocity.x < 0 && left))
            velocity.x = 0;

        if(velocity.y < 0)
            velocity.y = CheckDownSpeed(velocity.y);
        if(velocity.y > 0)
            velocity.y = CheckUpSpeed(velocity.y);

    }

    void GetPlayerInputs() {

        if(Input.GetKeyDown(KeyCode.Escape))
            Application.Quit();

        horizontal = Input.GetAxis("Horizontal");
        vertical = Input.GetAxis("Vertical");
        mouseHorizontal = Input.GetAxis("Mouse X");
        mouseVertical = Input.GetAxis("Mouse Y");

        if(Input.GetButtonDown("Sprint"))
            isSprinting = true;
        if(Input.GetButtonUp("Sprint"))
            isSprinting = false;

        if(isGrounded && Input.GetButtonDown("Jump"))
            jumpRequest = true;

        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if(blockHighlight.gameObject.activeSelf) {

            // delete block
            if(Input.GetMouseButtonDown(0))
                //world.GetChunkFromPosition(blockHighlight.position).EditVoxel(blockHighlight.position, 0);
                world.modifications.Add(new ReplaceVoxelMod(blockHighlight.position, 0));

            // place block
            if(Input.GetMouseButtonDown(1))
                if(toolbar.hasItem) {
                    //world.GetChunkFromPosition(blockPlacer.position).EditVoxel(blockPlacer.position, selectedBlockIndex);
                    world.modifications.Add(new AddVoxelMod(blockPlacer.position, toolbar.selectedBlockIndex));
                    toolbar.slots[toolbar.slotIndex].itemSlot.Take(1);
                }

        }

    }   

    void PlaceCursorBlocks() {

        float step = checkIncrement;
        Vector3 lastPos = new Vector3();

        // 'fake' raycast by stepping thru world
        while(step < reach) {

            Vector3 pos = cam.position + (cam.forward * step);

            if(world.Voxel(pos).isSolid) {

                blockHighlight.position = new Vector3(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y), Mathf.FloorToInt(pos.z));
                blockPlacer.position = lastPos;

                blockHighlight.gameObject.SetActive(true);
                blockPlacer.gameObject.SetActive(true);

                return;

            }

            lastPos = new Vector3(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y), Mathf.FloorToInt(pos.z));
            step += checkIncrement;

        }

        blockHighlight.gameObject.SetActive(false);
        blockPlacer.gameObject.SetActive(false);

    }

    // if speed would put player into ground, adjust speed and note that we're grounded
    // downSpeed should be negative
    float CheckDownSpeed(float downSpeed) {
        if(
            world.Voxel(new Vector3(transform.position.x - playerWidth, transform.position.y + downSpeed, transform.position.z - playerWidth)).isSolid ||
            world.Voxel(new Vector3(transform.position.x + playerWidth, transform.position.y + downSpeed, transform.position.z - playerWidth)).isSolid ||
            world.Voxel(new Vector3(transform.position.x + playerWidth, transform.position.y + downSpeed, transform.position.z + playerWidth)).isSolid ||
            world.Voxel(new Vector3(transform.position.x - playerWidth, transform.position.y + downSpeed, transform.position.z + playerWidth)).isSolid
        ) {

            isGrounded = true;
            return 0;

        }
        else {

            isGrounded = false;
            return downSpeed;

        }
    }

    // if speed would put player into ground, adjust speed
    // upSpeed should be positive
    float CheckUpSpeed(float upSpeed) {
        if(
            world.Voxel(new Vector3(transform.position.x - playerWidth, transform.position.y + upSpeed + playerHeight+0.2f, transform.position.z - playerWidth)).isSolid ||
            world.Voxel(new Vector3(transform.position.x + playerWidth, transform.position.y + upSpeed + playerHeight+0.2f, transform.position.z - playerWidth)).isSolid ||
            world.Voxel(new Vector3(transform.position.x + playerWidth, transform.position.y + upSpeed + playerHeight+0.2f, transform.position.z + playerWidth)).isSolid ||
            world.Voxel(new Vector3(transform.position.x - playerWidth, transform.position.y + upSpeed + playerHeight+0.2f, transform.position.z + playerWidth)).isSolid
        ) {

            return 0;

        }
        else {

            return upSpeed;

        }
    }

    // true if any blocks in front of player
    public bool front {
        get {
            return (
                world.Voxel(new Vector3(transform.position.x, transform.position.y, transform.position.z + playerWidth)).isSolid ||
                world.Voxel(new Vector3(transform.position.x, transform.position.y + 1f, transform.position.z + playerWidth)).isSolid
            );
        }
    }

    // true if any blocks behind player
    public bool back {
        get {
            return (
                world.Voxel(new Vector3(transform.position.x, transform.position.y, transform.position.z - playerWidth)).isSolid ||
                world.Voxel(new Vector3(transform.position.x, transform.position.y + 1f, transform.position.z - playerWidth)).isSolid
            );
        }
    }

    // true if any blocks to left of player
    public bool left {
        get {
            return (
                world.Voxel(new Vector3(transform.position.x - playerWidth, transform.position.y, transform.position.z)).isSolid ||
                world.Voxel(new Vector3(transform.position.x - playerWidth, transform.position.y + 1f, transform.position.z)).isSolid
            );
        }
    }

    // true if any blocks to right of player
    public bool right {
        get {
            return (
                world.Voxel(new Vector3(transform.position.x + playerWidth, transform.position.y, transform.position.z)).isSolid ||
                world.Voxel(new Vector3(transform.position.x + playerWidth, transform.position.y + 1f, transform.position.z)).isSolid
            );
        }
    }

}
