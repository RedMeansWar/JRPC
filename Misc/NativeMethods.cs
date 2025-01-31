using System;
using System.Runtime.InteropServices;

namespace JRPC_Client
{
    #pragma warning disable
    public static class NativeMethods
    {
        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject([In] IntPtr hObject);
    }
}
