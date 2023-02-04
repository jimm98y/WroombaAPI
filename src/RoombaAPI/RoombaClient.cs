using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using uPLibrary.Networking.M2Mqtt;
using RoombaAPI.API;

namespace RoombaAPI
{
    public class MessageReceivedEventArgs
    {
        private string message;

        public MessageReceivedEventArgs(string message, ConfigurationState state)
        {
            this.Message = message;
            this.State = state;
        }

        public string Message { get => message; private set => message = value; }

        public ConfigurationState State { get; private set; }
    }

    public class RoombaClient : IDisposable
    {
        private const int ROOMBA_BROADCAST_PORT = 5678;
        private const string ROOMBA_BROADCAST_MESSAGE = "irobotmcs";
        private const int ROOMBA_BROADCAST_TIMEOUT = 10000;

        private MqttClient client = null;

        internal MqttClient Client
        {
            get
            {
                return client;
            }

            private set { client = value; }
        }

        public event EventHandler<MessageReceivedEventArgs> MessegeReceived;

        public RoombaClient(string address)
        {
            this.Client = CreateMqttClient(address);
        }

        private MqttClient CreateMqttClient(string address)
        {
            // we have to use a custom build of the m2mqtt because of a necessary password length check adjustment in MqttMsgConnect.cs
            MqttClient client = new MqttClient(address, 8883, true, null, null, MqttSslProtocols.TLSv1_2, OnCertificateValidation, OnCertificateSelection);
            client.ProtocolVersion = MqttProtocolVersion.Version_3_1_1;
            client.MqttMsgPublishReceived += Client_MqttMsgPublishReceived;
            return client;
        }

        private X509Certificate OnCertificateSelection(object sender, string targetHost, X509CertificateCollection localCertificates, X509Certificate remoteCertificate, string[] acceptableIssuers)
        {
            return remoteCertificate;
        }

        private bool OnCertificateValidation(object arg1, X509Certificate arg2, X509Chain arg3, SslPolicyErrors arg4)
        {
            // ignoreall certificate errors
            return true;
        }

        public byte Connect(string user, string password)
        {
            byte ret = client.Connect(user, user, password, false, 30);
            return ret;
        }

        public void SetTime()
        {
            int offset = GetUtcTimeOffset();
            long time = GetTimestamp();
            SetTime(time, offset);
        }

        public void SetTime(long time, int offset)
        {
            string cmd = "{\"utctime\": " + time + ", \"localtimeoffset\": " + offset + "}";
            ExecuteDeltaCommand(cmd);
        }

        public void SetWiFi(string ssid, string password, int sec)
        {
            // turn on robot (three buttons down, home, clean, spot)
            // hold two buttons Home + Spot for !5 seconds till the wifi light starts flashing up
            // SSID is Roomba_<user>
            // IP is 192.168.10.1
            // after calling this method, hit "Start" on the robot
            string ssidEncoded = GetSsid(ssid);
            string cmd = "{\"wlcfg\": { \"ssid\": \"" + ssidEncoded + "\", \"sec\": " + sec + ", \"pass\": \"" + password + "\"}}";
            ExecuteDeltaCommand(cmd);
        }

        public void SetSchedule(string[] actions, int[] hours, int[] minutes)
        {
            if (actions == null || actions.Length != 7 ||
                hours == null || hours.Length != 7 ||
                minutes == null || minutes.Length != 7)
                throw new ArgumentException();

            string scheduleCommand =
                "{\"cleanSchedule\": " +
                    "{" +
                        // su mo tu we th fr sa
                        string.Format("\"cycle\": [ \"{0}\", \"{1}\", \"{2}\", \"{3}\", \"{4}\", \"{5}\", \"{6}\" ],",
                                        actions[0],
                                        actions[1],
                                        actions[2],
                                        actions[3],
                                        actions[4],
                                        actions[5],
                                        actions[6]) +
                        string.Format("\"h\": [ {0}, {1}, {2}, {3}, {4}, {5}, {6} ],",
                                        hours[0],
                                        hours[1],
                                        hours[2],
                                        hours[3],
                                        hours[4],
                                        hours[5],
                                        hours[6]) +
                        string.Format("\"m\": [ {0}, {1}, {2}, {3}, {4}, {5}, {6} ]",
                                        minutes[0],
                                        minutes[1],
                                        minutes[2],
                                        minutes[3],
                                        minutes[4],
                                        minutes[5],
                                        minutes[6]) +
                    "}" +
                "}";
            ExecuteDeltaCommand(scheduleCommand);
        }

        public void Start()
        {
            ExecuteCommand("start");
        }

        public void Stop()
        {
            ExecuteCommand("stop");
        }

        public void Pause()
        {
            ExecuteCommand("pause");
        }

        public void Resume()
        {
            ExecuteCommand("resume");
        }

        public void Dock()
        {
            ExecuteCommand("dock");
        }

        private void Client_MqttMsgPublishReceived(object sender, uPLibrary.Networking.M2Mqtt.Messages.MqttMsgPublishEventArgs e)
        {
            string msg = Encoding.UTF8.GetString(e.Message);
            ConfigurationState state = null;

            try
            {
                state = JsonSerializer.Deserialize<ConfigurationState>(msg, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

            MessegeReceived?.Invoke(this, new MessageReceivedEventArgs(msg, state));
        }

        public Tuple<string[], int[], int[]> ParseScheduleMessage(string msg)
        {
            //{"state":{"reported":{"cleanSchedule":{"cycle":["none","start","start","start","start","start","none"],"h":[9,9,9,9,9,9,9],"m":[0,0,0,0,0,0,0]},"bbchg3":{"avgMin":371,"hOnDock":337,"nAvail":32,"estCap":7451,"nLithChrg":12,"nNimhChrg":0,"nDocks":35}}}}
            JsonNode root = JsonValue.Parse(msg);
            var cleanSchedule = root["state"].AsObject()["reported"].AsObject()["cleanSchedule"].AsObject();
            var cycle = cleanSchedule["cycle"].AsArray().Select(x => x.GetValue<string>()).ToArray();
            var h = cleanSchedule["h"].AsArray().Select(x => x.GetValue<int>()).ToArray();
            var m = cleanSchedule["m"].AsArray().Select(x => x.GetValue<int>()).ToArray();
            return new Tuple<string[], int[], int[]>(cycle, h, m);
        }

        private void ExecuteCommand(string action)
        {
            string command = "{\"command\": \"" + action + "\", \"time\": " + GetTimestamp() + ", \"initiator\": \"localApp\"}";
            var ret = Client.Publish("cmd", Encoding.UTF8.GetBytes(command));
        }

        private void ExecuteDeltaCommand(string state)
        {
            string command = "{\"state\": " + state + "}";
            var ret = Client.Publish("delta", Encoding.UTF8.GetBytes(command));
        }

        private long GetTimestamp()
        {
            return (long)Math.Floor(GetTime() / 1000.0);
        }

        private Int64 GetTime()
        {
            var st = new DateTime(1970, 1, 1);
            TimeSpan t = (DateTime.Now.ToUniversalTime() - st);
            long retval = (long)(t.TotalMilliseconds + 0.5);
            return retval;
        }

        public string GetCycle(bool on)
        {
            return on ? "start" : "none";
        }

        private static string GetSsid(string ssid)
        {
            return BitConverter.ToString(ssid.Select(x => (byte)x).ToArray()).Replace("-", "");
        }

        private static int GetUtcTimeOffset()
        {
            return (int)Math.Floor((DateTime.Now.Subtract(DateTime.UtcNow)).Add(TimeSpan.FromMilliseconds(500)).TotalMinutes);
        }

        #region Discovery

        public static SemaphoreSlim _discoverySlim = new SemaphoreSlim(1);

        private class UdpState
        {
            public UdpClient Client { get; set; }
            public IPEndPoint Endpoint { get; set; }
            public Dictionary<string, Tuple<string, string>> Result { get; set; }
        }

        /// <summary>
        /// Discovery.
        /// </summary>
        /// <returns></returns>
        public static async Task<Dictionary<string, Tuple<string, string>>> DiscoverAsync(string networkIpAddress, int broadcastTimeout = ROOMBA_BROADCAST_TIMEOUT)
        {
            await _discoverySlim.WaitAsync();

            Dictionary<string, Tuple<string, string>> robots = new Dictionary<string, Tuple<string, string>>();
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, ROOMBA_BROADCAST_PORT + 1);
            IPEndPoint broadcastEndpoint = new IPEndPoint(IPAddress.Parse(networkIpAddress), ROOMBA_BROADCAST_PORT);

            try
            {
                using (UdpClient client = new UdpClient(endPoint))
                {
                    UdpState s = new UdpState();
                    s.Endpoint = endPoint;
                    s.Client = client;
                    s.Result = robots;

                    client.BeginReceive(MessageReceived, s);

                    byte[] message = Encoding.ASCII.GetBytes(ROOMBA_BROADCAST_MESSAGE);
                    await client.SendAsync(message, message.Count(), broadcastEndpoint);

                    // make sure we do not wait forever
                    await Task.Delay(broadcastTimeout);

                    return s.Result;
                }
            }
            finally
            {
                _discoverySlim.Release();
            }
        }

        private static async void MessageReceived(IAsyncResult result)
        {
            try
            {
                UdpClient client = ((UdpState)result.AsyncState).Client;
                IPEndPoint endpoint = ((UdpState)result.AsyncState).Endpoint;
                byte[] receiveBytes = client.EndReceive(result, ref endpoint);
                string json = Encoding.ASCII.GetString(receiveBytes);
                string host = endpoint.Address.ToString();
                var robots = ((UdpState)result.AsyncState).Result;

                if (string.Compare(json, ROOMBA_BROADCAST_MESSAGE) != 0)
                {
                    var parsed = JsonObject.Parse(json);
                    JsonNode parsedNode;
                    string hostName = "";
                    if (parsed.AsObject().TryGetPropertyValue("hostname", out parsedNode))
                    {
                        hostName = parsedNode.AsValue().ToString().Split('-').Last();
                    }

                    string password = await GetPassword(host);
                    robots.Add(host, new Tuple<string, string>(hostName, password));
                }

                client.BeginReceive(MessageReceived, result.AsyncState);
            }
            catch(Exception ex)
            {

            }
        }

        private static async Task<string> GetPassword(string host)
        {
            string password = string.Empty;

            using (TcpClient tcpClient = new TcpClient(host, 8883))
            {
                using (SslStream sslStream = new SslStream(tcpClient.GetStream(), false,
                    new RemoteCertificateValidationCallback((sender, certificate, chain, sslPolicyErrors) => { return true; }) // ignore all SSL errors
                ))
                {

                    // The server name must match the name on the server certificate.
                    try
                    {
                        sslStream.AuthenticateAsClient(host, null, SslProtocols.Tls12, false);

                        // send get password message
                        var msg = new byte[] { 0xf0, 0x05, 0xef, 0xcc, 0x3b, 0x29, 0x00 };
                        await sslStream.WriteAsync(msg, 0, msg.Length);
                        await sslStream.FlushAsync();

                        // read the response
                        byte[] buffer = new byte[2048];
                        StringBuilder messageData = new StringBuilder();
                        int inBufferCnt = -1;
                        int sliceFrom = 13;

                        do
                        {
                            inBufferCnt = await sslStream.ReadAsync(buffer, 0, buffer.Length);

                            if (inBufferCnt == 2)
                            {
                                sliceFrom = 9;
                                continue;
                            }

                            if (inBufferCnt <= 7)
                                return password; // blank password or error

                            password = Encoding.UTF8.GetString(buffer, sliceFrom, inBufferCnt - sliceFrom);
                            break;
                        } while (inBufferCnt != 0);
                    }
                    catch (AuthenticationException e)
                    {
                        Debug.WriteLine("Exception: {0}", e.Message);

                        if (e.InnerException != null)
                        {
                            Debug.WriteLine("Inner exception: {0}", e.InnerException.Message);
                        }

                        Debug.WriteLine("Authentication failed - closing the connection.");
                        tcpClient.Close();
                    }
                }
            }

            return password;
        }

        #endregion // Discovery

        #region IDisposable Support

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (Client != null)
                    {
                        Client.MqttMsgPublishReceived -= Client_MqttMsgPublishReceived;
                        Client.Disconnect();
                        Client = null;
                    }
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        #endregion // IDisposable Support
    }
}
