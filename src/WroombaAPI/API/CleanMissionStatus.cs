namespace RoombaAPI.API
{
    public class CleanMissionStatus
    {
        public string Cycle { get; set; }
        public string Phase { get; set; }
        public int ExpireM { get; set; }
        public int RechrgM { get; set; }
        public int Error { get; set; }
        public int NotReady { get; set; }
        public int MssnM { get; set; }
        public int Sqft { get; set; }
        public string Initiator { get; set; }
        public int NMssn { get; set; }
    }
}