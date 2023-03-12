namespace RoombaAPI.Model
{
    public class RoombaRobot
    {
        public string HostName { get; private set; }
        public string UserName { get; private set; }
        public string Password { get; private set; }

        public RoombaRobot(string hostName, string userName, string password)
        {
            HostName = hostName;
            UserName = userName;
            Password = password;
        }
    }
}
