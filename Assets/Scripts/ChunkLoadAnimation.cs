using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChunkLoadAnimation : MonoBehaviour
{
    float speed = 3f;
    Vector3 targetPos;
    float waitTimer;
    float timer;

    // Start is called before the first frame update
    void Start()
    {
        waitTimer = Random.Range(0f, 1f);
        timer = 0;
        targetPos = transform.position;
        transform.position = new Vector3(transform.position.x, -VoxelData.ChunkHeight, transform.position.z);
    }

    // Update is called once per frame
    void Update()
    {
        if(timer < waitTimer)
            timer += Time.deltaTime;

        else if(targetPos.y - transform.position.y < 0.05f) {

            transform.position = targetPos;
            Destroy(this);

        }
        else
            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * speed);
    }
}
