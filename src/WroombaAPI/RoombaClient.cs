﻿using System;
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
using RoombaAPI.API;
using RoombaAPI.Model;
using uPLibrary.Networking.M2Mqtt;

namespace RoombaAPI
{
    /// <summary>
    /// iRobot Roomba client.
    /// </summary>
    /// <remarks>Based upon https://github.com/koalazak/dorita980</remarks>
    public class RoombaClient : IDisposable
    {
        private class UdpState
        {
            public UdpClient Client { get; set; }
            public IPEndPoint Endpoint { get; set; }
            public IList<RoombaRobot> Result { get; set; }
        }

        private const int ROOMBA_BROADCAST_PORT = 5678;
        private const string ROOMBA_BROADCAST_MESSAGE = "irobotmcs";
        private const int ROOMBA_BROADCAST_TIMEOUT = 10000;
        private const int KEEP_ALIVE_PERIOD = 30;
        private const int MQTT_PORT = 8883;

        private static SemaphoreSlim _discoverySlim = new SemaphoreSlim(1);
        private readonly MqttClient _client = null;

        /// <summary>
        /// Raised when a new message from Roomba is received.
        /// </summary>
        public event EventHandler<MessageReceivedEventArgs> MessegeReceived;

        /// <summary>
        /// Ctor.
        /// </summary>
        /// <param name="address"></param>
        public RoombaClient(string address)
        {
            this._client = CreateMqttClient(address);
        }

        /// <summary>
        /// Sign in.
        /// </summary>
        /// <param name="user">User name.</param>
        /// <param name="password">Password.</param>
        /// <returns>true if successful, false otherwise.</returns>
        public bool SignIn(string user, string password)
        {
            return this._client.Connect(user, user, password, false, KEEP_ALIVE_PERIOD) == 0;
        }

        /// <summary>
        /// Sets the current time.
        /// </summary>
        public void SetCurrentTime()
        {
            int offset = GetUtcTimeOffset();
            long time = GetTimestamp();
            SetTime(time, offset);
        }

        /// <summary>
        /// Sets the time.
        /// </summary>
        /// <param name="time">UTC time in miliseconds since 1/1/1970.</param>
        /// <param name="offset">Time offset from UTC in minutes.</param>
        public void SetTime(long time, int offset)
        {
            string cmd = "{\"utctime\": " + time + ", \"localtimeoffset\": " + offset + "}";
            ExecuteDeltaCommand(cmd);
        }

        /// <summary>
        /// Set Wi-Fi.
        /// </summary>
        /// <param name="ssid">Wi-Fi SSID.</param>
        /// <param name="password">Password.</param>
        /// <param name="sec"><see cref="WiFiSecurity"/>.</param>
        public void SetWiFi(string ssid, string password, WiFiSecurity sec)
        {
            // turn on robot (three buttons down, home, clean, spot)
            // hold two buttons Home + Spot for !5 seconds till the wifi light starts flashing up
            // SSID is Roomba_<user>
            // IP is 192.168.10.1
            // after calling this method, hit "Start" on the robot
            string ssidEncoded = GetSsid(ssid);
            string cmd = "{\"wlcfg\": { \"ssid\": \"" + ssidEncoded + "\", \"sec\": " + (int)sec + ", \"pass\": \"" + password + "\"}}";
            ExecuteDeltaCommand(cmd);
        }

        /// <summary>
        /// Start cleaning.
        /// </summary>
        public void Start()
        {
            ExecuteCommand("start");
        }

        /// <summary>
        /// Stop cleaning.
        /// </summary>
        public void Stop()
        {
            ExecuteCommand("stop");
        }

        /// <summary>
        /// Pause cleaning.
        /// </summary>
        public void Pause()
        {
            ExecuteCommand("pause");
        }

        /// <summary>
        /// Resume cleaning.
        /// </summary>
        public void Resume()
        {
            ExecuteCommand("resume");
        }

        /// <summary>
        /// Return to the dock.
        /// </summary>
        public void Dock()
        {
            ExecuteCommand("dock");
        }

        /// <summary>
        /// Set Roomba schedule.
        /// </summary>
        /// <param name="actions">An array of true/false. True means to start cleaning, False no action planned.</param>
        /// <param name="hours">An array of hours.</param>
        /// <param name="minutes">An array of minutes.</param>
        /// <exception cref="ArgumentException"></exception>
        public void SetSchedule(bool[] actions, int[] hours, int[] minutes)
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
                                        GetCycle(actions[0]),
                                        GetCycle(actions[1]),
                                        GetCycle(actions[2]),
                                        GetCycle(actions[3]),
                                        GetCycle(actions[4]),
                                        GetCycle(actions[5]),
                                        GetCycle(actions[6])) +
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

        /// <summary>
        /// Discover Roomba in the local network.
        /// </summary>
        /// <param name="networkIpAddress">Network IP address.</param>
        /// <param name="broadcastTimeout"><see cref="ROOMBA_BROADCAST_TIMEOUT"/>.</param>
        /// <returns>A list of discovered <see cref="RoombaRobot"/>.</returns>
        public static async Task<IList<RoombaRobot>> DiscoverAsync(string networkIpAddress, int broadcastTimeout = ROOMBA_BROADCAST_TIMEOUT)
        {
            await _discoverySlim.WaitAsync();

            IList<RoombaRobot> robots = new List<RoombaRobot>();
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

        private MqttClient CreateMqttClient(string address)
        {
            // we have to use a custom build of the m2mqtt because of a necessary password length check adjustment in MqttMsgConnect.cs
            MqttClient client = new MqttClient(address, MQTT_PORT, true, null, null, MqttSslProtocols.TLSv1_2, OnCertificateValidation, OnCertificateSelection);
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
            // ignore all certificate errors
            return true;
        }

        private void Client_MqttMsgPublishReceived(object sender, uPLibrary.Networking.M2Mqtt.Messages.MqttMsgPublishEventArgs e)
        {
            string msg = Encoding.UTF8.GetString(e.Message);
            RoombaState state = null;

            try
            {
                state = JsonSerializer.Deserialize<RoombaState>(msg, 
                    new JsonSerializerOptions
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

        private int ExecuteCommand(string action)
        {
            string command = "{\"command\": \"" + action + "\", \"time\": " + GetTimestamp() + ", \"initiator\": \"localApp\"}";
            return this._client.Publish("cmd", Encoding.UTF8.GetBytes(command));
        }

        private int ExecuteDeltaCommand(string state)
        {
            string command = "{\"state\": " + state + "}";
            return this._client.Publish("delta", Encoding.UTF8.GetBytes(command));
        }

        private static string GetCycle(bool on)
        {
            return on ? "start" : "none";
        }

        private long GetTimestamp()
        {
            return (long)Math.Floor(GetTime() / 1000.0);
        }

        private long GetTime()
        {
            var st = new DateTime(1970, 1, 1);
            TimeSpan t = (DateTime.Now.ToUniversalTime() - st);
            long retval = (long)(t.TotalMilliseconds + 0.5);
            return retval;
        }

        private static string GetSsid(string ssid)
        {
            return BitConverter.ToString(ssid.Select(x => (byte)x).ToArray()).Replace("-", "");
        }

        private static int GetUtcTimeOffset()
        {
            return (int)Math.Floor(DateTime.Now.Subtract(DateTime.UtcNow).Add(TimeSpan.FromMilliseconds(500)).TotalMinutes);
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

                    string password = await GetPasswordAsync(host);
                    robots.Add(new RoombaRobot(host, hostName, password));
                }

                client.BeginReceive(MessageReceived, result.AsyncState);
            }
            catch(Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        private static async Task<string> GetPasswordAsync(string host)
        {
            string password = string.Empty;

            using (TcpClient tcpClient = new TcpClient(host, MQTT_PORT))
            {
                using (SslStream sslStream = new SslStream(
                    tcpClient.GetStream(),
                    false,
                    new RemoteCertificateValidationCallback(
                        (sender, certificate, chain, sslPolicyErrors) =>
                        { 
                            return true;
                        })
                    )
                )
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
                        }
                        while (inBufferCnt != 0);
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

        #region IDisposable

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    this._client.MqttMsgPublishReceived -= Client_MqttMsgPublishReceived;
                    this._client.Disconnect();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        #endregion // IDisposable
    }
}
