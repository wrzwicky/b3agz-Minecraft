using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Toolbar : MonoBehaviour {

    public Player player;

    public UIItemSlot[] slots;

    public RectTransform highlight;
    public int slotIndex = 0;


    public bool hasItem {
        get { return slots[slotIndex].HasItem; }
    }
    
    public byte selectedBlockIndex {
        get { return slots[slotIndex].itemSlot.stack.id; }
    }

    void Start() {

        byte index = 1;
        foreach(UIItemSlot s in slots) {

            ItemStack stack = new ItemStack(index, Random.Range(2,65));
            ItemSlot slot = new ItemSlot(s, stack);
            index++;

        }
    }

    // Update is called once per frame
    void Update()
    {
        
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if(scroll != 0) {

            if(scroll > 0)
                slotIndex --;
            else
                slotIndex++;

            if(slotIndex > slots.Length-1)
                slotIndex = 0;
            else if(slotIndex < 0)
                slotIndex = slots.Length - 1;

            highlight.position = slots[slotIndex].slotIcon.transform.position;

        }
    }
}


public class Toolbar_old : MonoBehaviour
{
    World world;
    public Player player;
    public RectTransform highlight;
    public ItemSlot_old[] itemSlots;

    int slotIndex = 0;

    // Start is called before the first frame update
    void Start()
    {
        
        world = GameObject.Find("World").GetComponent<World>();

        foreach(ItemSlot_old slot in itemSlots) {

            slot.icon.sprite = world.blockTypes[slot.itemID].icon;
            slot.icon.enabled = true;

        }
    }

    // Update is called once per frame
    void Update()
    {
        
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if(scroll != 0) {

            if(scroll > 0)
                slotIndex --;
            else
                slotIndex++;

            if(slotIndex > itemSlots.Length-1)
                slotIndex = 0;
            else if(slotIndex < 0)
                slotIndex = itemSlots.Length - 1;

            highlight.position = itemSlots[slotIndex].icon.transform.position;

        }

    }
}


[System.Serializable]
public class ItemSlot_old {

    public byte itemID;
    public Image icon;

}