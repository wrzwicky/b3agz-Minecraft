
[System.Serializable]
public class VoxelState {
    
    /// <summary>block type id</summary>
    public byte id;
    /// <summary>How much light is falling on this block</summary>
    [System.NonSerialized]
    private byte _light;


    public VoxelState(byte id) {

        this.id = id;
        this.light = 0;

    }

    // b3agz calls it 'properties'
    public BlockType blockType {
        get { return World.Instance.blockTypes[this.id]; }
    }

    public byte light {
        get { return _light; }
        set { _light = value; }
    }

    public float lightAsFloat {
        get { return (float)light * VoxelData.unitOfLight; }
    }

    // Get amount of light this block is casting to neighbors.
    public byte castLight {
        get {
            int neu = _light - blockType.opacity - 1;
            if(neu < 0) neu = 0;
            return (byte)neu;
        }
    }
}
