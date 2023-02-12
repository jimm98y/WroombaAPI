using RoombaAPI.API;

namespace RoombaAPI
{
    public class MessageReceivedEventArgs
    {
        private string message;

        public MessageReceivedEventArgs(string message, RoombaState state)
        {
            this.Message = message;
            this.State = state;
        }

        public string Message { get => message; private set => message = value; }

        public RoombaState State { get; private set; }
    }
}
