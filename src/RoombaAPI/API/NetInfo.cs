namespace RoombaAPI.API
{
    public class NetInfo
    {
        public bool Dhcp { get; set; }
        public uint Addr { get; set; }
        public uint Mask { get; set; }
        public uint Gw { get; set; }
        public uint Dns1 { get; set; }
        public uint Dns2 { get; set; }
        public string Bssid { get; set; }
        public uint Sec { get; set; }
    }
}