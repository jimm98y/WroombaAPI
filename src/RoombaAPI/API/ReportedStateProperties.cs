namespace RoombaAPI.API
{
    public class ReportedStateProperties
    {
        public NetInfo Netinfo { get; set; }
        public WiFiStat Wifistat { get; set; }
        public WlCfg Wlcfg { get; set; }
        public string Mac { get; set; }
        public string Country { get; set; }
        public string CloudEnv { get; set; }
        public SvcEndpoints SvcEndpoints { get; set; }
        public bool MapUploadAllowed { get; set; }
        public int Localtimeoffset { get; set; }
        public uint Utctime { get; set; }
        public Pose Pose { get; set; }
        public int BatPct { get; set; }
        public Dock Dock { get; set; }
        public Bin Bin { get; set; }
        public Audio Audio { get; set; }
        public CleanMissionStatus CleanMissionStatus { get; set; }
        public int Language { get; set; }
        public bool NoAutoPasses { get; set; }
        public bool NoPP { get; set; }
        public bool EcoCharge { get; set; }
        public bool VacHigh { get; set; }
        public bool BinPause { get; set; }
        public bool CarpetBoost { get; set; }
        public bool OpenOnly { get; set; }
        public bool TwoPass { get; set; }
        public bool SchedHold { get; set; }
        public RoombaCommand LastCommand { get; set; }
        public Language[] Langs { get; set; }
        public BbNav Bbnav { get; set; }
        public BbPanic Bbpanic { get; set; }
        public BbMssn Bbmssn { get; set; }
        public BbRstInfo Bbrstinfo { get; set; }
        public Cap Cap { get; set; }
        public string Sku { get; set; }
        public string BatteryType { get; set; }
        public string SoundVer { get; set; }
        public string UiSwVer { get; set; }
        public string NavSwVer { get; set; }
        public string WifiSwVer { get; set; }
        public string MobilityVer { get; set; }
        public string BootloaderVer { get; set; }
        public string UmiVer { get; set; }
        public string SoftwareVer { get; set; }
        public Tz Tz { get; set; }
        public string Timezone { get; set; }
        public string Name { get; set; }
        public CleanSchedule CleanSchedule { get; set; }
        public BbChg3 Bbchg3 { get; set; }
        public BbChg Bbchg { get; set; }
        public BbSwitch Bbswitch { get; set; }
        public BbBrun Bbrun { get; set; }
        public BbSys Bbsys { get; set; }
        public Signal Signal { get; set; }
    }
}