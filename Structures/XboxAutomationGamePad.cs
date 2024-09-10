using XDevkit;

namespace JRPC_Client
{
    public struct XboxAutomationGamePad
    {
        public XboxAutomationButtonFlags Buttons;
        public uint LeftTrigger;
        public uint RightTrigger;
        public int LeftThumbX;
        public int LeftThumbY;
        public int RightThumbX;
        public int RightThumbY;
    }
}
