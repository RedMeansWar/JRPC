using XDevkit;

namespace JRPC_Client
{
    #pragma warning disable
    public class Tray
    {
        protected static IXboxConsole console;

        public bool IsTrayOpen
        {
            get => false;
            set { }
        }

        public static void Open()
        {
            console.CallVoid(console.ResolveFunction(Modules.XAM, 96u), [0, 0, 0, 0]);
        }

        public static void Close()
        {
            console.CallVoid(console.ResolveFunction(Modules.XAM, 98u), [0, 0, 0, 0]);
        }

        public bool Options(TrayState TrayState)
        {
            switch (TrayState)
            {
                case TrayState.Open:
                    console.CallVoid(console.ResolveFunction(Modules.XAM, 96u), [0, 0, 0, 0]);
                    IsTrayOpen = true;
                    break;

                case TrayState.Close:
                    console.CallVoid(console.ResolveFunction(Modules.XAM, 98u), [0, 0, 0, 0]);
                    IsTrayOpen = false;
                    break;
            }

            return IsTrayOpen;
        }
    }
}
