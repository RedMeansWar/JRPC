namespace JRPC_Client
{
    #pragma warning disable
    internal enum DumpFlag
    {
        Normal = 0,
        WithDataSegs = 1,
        WithFullMemory = 2,
        WithHandleData = 4,
        FilterMemory = 8,
        ScanMemory = 16, // 0x00000010
        WithUnloadedModules = 32, // 0x00000020
        WithIndirectlyReferencedMemory = 64, // 0x00000040
        FilterModulePaths = 128, // 0x00000080
        WithProcessThreadData = 256, // 0x00000100
        WithPrivateReadWriteMemory = 512, // 0x00000200
    }
}
