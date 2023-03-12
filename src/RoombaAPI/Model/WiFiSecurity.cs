namespace RoombaAPI.Model
{
    /// <summary>
    /// Wi-Fi security type.
    /// </summary>
    public enum WiFiSecurity : int
    {
        None = 0,
        WepOpen = 1,
        WepShared = 2,
        WPA = 3,
        WPA2 = 4,
        WpaWpa2Mixed = 5,
        EapTls = 6,
        Wildcard = 7
    }
}
