using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CreativeInventory : MonoBehaviour
{
    public GameObject slotPrefab;
    World world;

    List<ItemSlot> slots = new List<ItemSlot>();

    // Start is called before the first frame update
    void Start() {

        world = GameObject.Find("World").GetComponent<World>();

        for(int id=1; id < world.blockTypes.Length; id++) {

            GameObject newSlot = Instantiate(slotPrefab, transform);

            ItemStack stack = new ItemStack((byte)id, 64);
            ItemSlot slot = new ItemSlot(newSlot.GetComponent<UIItemSlot>(), stack);
            slot.isCreative = true;
            slots.Add(slot);

        }
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
