using RoombaAPI.API;

namespace RoombaAPI
{
    public class MessageReceivedEventArgs
    {
        public string Message { get; private set; }

        public RoombaState State { get; private set; }

        public MessageReceivedEventArgs(string message, RoombaState state)
        {
            Message = message;
            State = state;
        }
    }
}
