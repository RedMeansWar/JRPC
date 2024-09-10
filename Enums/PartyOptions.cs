namespace JRPC_Client
{
    #pragma warning disable
    public enum PartyOptions : uint
    {
        CreateParty = 0xAFC,
        PartySettings = 0xB08,
        InviteOnly = 1,
        Kick = 0xB02,
        OpenParty = 0,
        JoinParty = 0xB01,
        AltJoinParty = 0xB1B,
        LeaveParty = 0xAFD,
        InvitePlayer = 0xB15,
    }
}
