using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DragAndDropHandler : MonoBehaviour
{
    World world;

    [SerializeField] private UIItemSlot cursorSlot = null;
    private ItemSlot cursorItemSlot;

    [SerializeField] private GraphicRaycaster m_Raycaster = null;
    private PointerEventData m_PointerEventData;
    [SerializeField] private EventSystem m_EventSystem = null;

    // Start is called before the first frame update
    void Start() {

        world = GameObject.Find("World").GetComponent<World>();

        cursorItemSlot = new ItemSlot(cursorSlot);
        
    }

    // Update is called once per frame
    void Update() {
        
        if(!world.inUI)
            return;

        cursorSlot.transform.position = Input.mousePosition;

        if(Input.GetMouseButtonDown(0)) {

            //TODO do the ui
            HandleSlotClick(CheckForSlot(Input.mousePosition));
            
        }
    }

    private void HandleSlotClick(UIItemSlot clickedSlot) {

        if(clickedSlot == null)
            return;

        if(!cursorSlot.HasItem && !clickedSlot.HasItem)
            // holding nothing, clicked empty -> ignore
            return;

        if(clickedSlot.itemSlot.isCreative) {

            // holding or not, clicked creative slot -> delete holding, get copy of slot
            cursorItemSlot.EmptySlot();
            cursorItemSlot.InsertStack(clickedSlot.itemSlot.stack);

        }

        if(!cursorSlot.HasItem && clickedSlot.HasItem) {
            
            //holding nothing, clicked something -> pick it up

            cursorItemSlot.InsertStack(clickedSlot.itemSlot.TakeAll());
            return;

        }

        if(cursorSlot.HasItem && !clickedSlot.HasItem) {

            // holding something, clicked empty -> put it down
            clickedSlot.itemSlot.InsertStack(cursorItemSlot.TakeAll());
            return;

        }

        if(cursorSlot.HasItem && clickedSlot.HasItem) {

            // holding something, clicked something -> ?
            if(cursorSlot.itemSlot.stack.id != clickedSlot.itemSlot.stack.id) {

                // different items? swap them
                ItemStack oldCurSlot = cursorSlot.itemSlot.TakeAll();
                ItemStack oldSlot = clickedSlot.itemSlot.TakeAll();
                clickedSlot.itemSlot.InsertStack(oldCurSlot);
                cursorSlot.itemSlot.InsertStack(oldSlot);

            }

            //TODO if id==id, combine them
        }
    }

    /// find which slot on screen is under the mouse at 'position'
    private UIItemSlot CheckForSlot(Vector2 position) {

        m_PointerEventData = new PointerEventData(m_EventSystem);
        m_PointerEventData.position = position;

        List<RaycastResult> results = new List<RaycastResult>();
        m_Raycaster.Raycast(m_PointerEventData, results);

        foreach(RaycastResult result in results) {

            if(result.gameObject.CompareTag("UIItemSlot"))
                return result.gameObject.GetComponent<UIItemSlot>();

        }

        return null;

    }
}
