using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIItemSlot : MonoBehaviour {

    World world;

    public bool isLinked = false;
    public ItemSlot itemSlot;
    public Image slotImage;
    public Image slotIcon;
    public Text slotAmount;

    public bool HasItem {

        get {

            if(itemSlot == null)
                return false;
            else
                return itemSlot.HasItem;

        }
    }

    void Awake() {

        world = GameObject.Find("World").GetComponent<World>();

    }

    public void Link(ItemSlot slot) {

        itemSlot = slot;
        itemSlot.LinkUISlot(this);
        isLinked = true;
        UpdateSlot();

    }

    public void UnLink() {

        isLinked = false;
        itemSlot.unLinkUISlot();
        itemSlot = null;
        UpdateSlot();

    }

    public void UpdateSlot() {

        if(HasItem) {

            slotIcon.sprite = world.blockTypes[itemSlot.stack.id].icon;
            slotAmount.text = itemSlot.stack.amount.ToString();
            slotIcon.enabled = true;
            slotAmount.enabled = true;

        }
        else
            Clear();
    }

    public void Clear() {

        slotIcon.sprite = null;
        slotAmount.text = "";
        slotIcon.enabled = false;
        slotAmount.enabled = false;

    }

    void OnDestroy() {

        if(isLinked)
            itemSlot.unLinkUISlot();
        
    }
}

public class ItemSlot {

    public ItemStack stack;
    private UIItemSlot uiItemSlot;

    // true for infinite blocks
    public bool isCreative;

    public ItemSlot(UIItemSlot _slot, ItemStack _stack) {

        stack = _stack;
        uiItemSlot = _slot;
        uiItemSlot.Link(this);

    }

    public ItemSlot(UIItemSlot slot) {

        stack = null;
        uiItemSlot = slot;
        uiItemSlot.Link(this);

    }

    public bool HasItem {

        get {

            return stack != null;

        }
    }

    public void LinkUISlot(UIItemSlot uiSlot) {

        uiItemSlot = uiSlot;

    }

    public void unLinkUISlot() {

        uiItemSlot = null;
        
    }

    public void EmptySlot() {

        stack = null;
        if(uiItemSlot != null)
            uiItemSlot.UpdateSlot();
            
    }

    public int Take(int amt) {

        if(amt > stack.amount) {
            int _amt = stack.amount;
            EmptySlot();
            return _amt;
        }
        else if(amt < stack.amount) {
            stack.amount -= amt;
            uiItemSlot.UpdateSlot();
            return amt;
        }
        else {
            EmptySlot();
            return amt;
        }
    }

    public ItemStack TakeAll() {

        ItemStack handOver = new ItemStack(stack.id, stack.amount);
        EmptySlot();
        return handOver;

    }

    public void InsertStack(ItemStack newStack) {

        stack = newStack;
        uiItemSlot.UpdateSlot();

    }

}