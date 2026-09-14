namespace PhoneGamepad;

public class GamepadState
{
    // ABXY
    public bool A { get; set; }
    public bool B { get; set; }
    public bool X { get; set; }
    public bool Y { get; set; }

    // 肩键
    public bool LB { get; set; }
    public bool RB { get; set; }

    // 左摇杆
    public float LX { get; set; }
    public float LY { get; set; }

    // 右摇杆
    public float RX { get; set; }
    public float RY { get; set; }

    public bool L3 { get; set; }
    public bool R3 { get; set; }

    // 扳机
    public bool LT { get; set; }
    public bool RT { get; set; }

    // Start / Back
    public bool Start { get; set; }
    public bool Back { get; set; }

    // 十字方向键
    public bool DPadUp { get; set; }
    public bool DPadDown { get; set; }
    public bool DPadLeft { get; set; }
    public bool DPadRight { get; set; }
}