using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

using System.Windows.Shapes;

using Newtonsoft.Json;
using System;

using System.Collections.Generic;
using System.Linq;

using System.Threading.Tasks;
using WebSocketSharp.Net.WebSockets;
using System;
//using System.IO;

using WebSocketSharp;
using WebSocketSharp.Server;


namespace NewServerGUI
{

    
    public partial class MainWindow : Window
    {
        public static string logFilePath = "";
        public MainWindow()
        {
            InitializeComponent();
            Program.wssv = new WebSocketServer("ws://127.0.0.1:7890");

            
            Program.wssv.AddWebSocketService<EchoAll>("/EchoAll");

            // Start the WebSocket server
            Program.wssv.Start();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
            Program.SendCloseConnectionPacket();

           
            Program.wssv.Stop();

        }



        private void LoadLogFile(string logFilePath)
        {
            try
            {
                if (File.Exists(logFilePath))
                {
                    string logContent = File.ReadAllText(logFilePath);
                    textBox.Text = logContent;
                }
                else
                {
                    MessageBox.Show($"The log file '{logFilePath}' does not exist.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}");
            }
        }

        private void SentPacketsButton_Click(object sender, RoutedEventArgs e)
        {
            logFilePath = "sentPackets.log";
            LoadLogFile(logFilePath);
        }

        private void ReceivedPacketsButton_Click(object sender, RoutedEventArgs e)
        {
            logFilePath = "receivedPackets.log";
            LoadLogFile(logFilePath);
        }

        private void StateMachineLogButton_Click(object sender, RoutedEventArgs e)
        {
            logFilePath = "internalstate.log";
            LoadLogFile(logFilePath);
            
        }

        private void ConnectionsButton_Click(object sender, RoutedEventArgs e)
        {
            logFilePath = "connections.log";
            LoadLogFile(logFilePath);
        }

        private void SessionsButton_Click(object sender, RoutedEventArgs e)
        {
            logFilePath = "sessions.log";
            LoadLogFile(logFilePath);
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();

        }

        private void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            Program.SendCloseConnectionPacket();

            // Stop the WebSocket server
            Program.wssv.Stop();
        }
    }

    public enum ActionState
    {
        Idle,
        Drawing,
        Writing,
        AddingImage,
        Connected,
        Disconnected,
        Processing
    }

    public static class Logger
    {
        private const string SentPacketsLogFile = "sentPackets.log";
        private const string ReceivedPacketsLogFile = "receivedPackets.log";
        private const string OpenConnectionsLogFile = "OpenConnections.log";
        private const string OpenSessionsLogFile = "OpenSessions.log";
        private const string InternalStateLogFile = "internalstate.log";
        private const string logGeneralFile = "logGeneral.log";
        public const string connectionsLogFile = "connections.log";

        public static void LogGeneral(dynamic packet)
        {
            string packetstring = packet.ToString();
            LogMessageString(packetstring, logGeneralFile);
        }

        public static void LogSentPacket(Packet packet)
        {
            LogPacket(packet, SentPacketsLogFile);
        }


        public static void LogReceivedPacket(Packet packet)
        {
            LogPacket(packet, ReceivedPacketsLogFile);
        }

        public static void LogOpenConnection(string connectionId)
        {
            LogMessage($"New connection opened. ID: {connectionId}", OpenConnectionsLogFile);
        }

        public static void LogOpenSession(string sessionCode)
        {
            LogMessage($"New session created. Code: {sessionCode}", OpenSessionsLogFile);
        }

        public static void LogInternalState(ActionState currentState)
        {
            string logEntry = $"{DateTime.Now}: Current state: {currentState}";
            LogMessage(logEntry, InternalStateLogFile);
        }


        private static void LogPacket(Packet packet, string logFile)
        {
            string logEntry = $"{packet.PacketHeader.Type}, {packet.PacketHeader.SequenceNumber}, {packet.PacketHeader.TimeStamp}, {packet.PacketBody.Data}";
            LogMessage(logEntry, logFile);
        }

        public static void LogMessage(string message, string logFile)
        {
            try
            {
                using (StreamWriter writer = File.AppendText(logFile))
                {
                    writer.WriteLine($"{DateTime.Now}: {message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error logging message: {ex.Message}");
            }
        }

        private static void LogMessageString(string message, string logFile)
        {
            try
            {
                using (StreamWriter writer = File.AppendText(logFile))
                {
                    writer.WriteLine(message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error logging message: {ex.Message}");
            }
        }
    }
    public enum PacketType
    {
        EstablishConnection,
        DrawingData,
        AddImage,
        AddText,
        CreateSession,
        SessionDetails,
        JoinSession,
        Login,
        CloseConnection,
    }

    public class Packet
    {
        public Header PacketHeader { get; set; }
        public Body PacketBody { get; set; }

        public Packet(PacketType type, int sequenceNumber, DateTime timeStamp, object bodyData)
        {
            PacketHeader = new Header { Type = type, SequenceNumber = sequenceNumber, TimeStamp = timeStamp };
            PacketBody = new Body { Data = bodyData };
        }

        public string Serialize()
        {
            string json = JsonConvert.SerializeObject(this);
            return json;
            //return System.Text.Encoding.ASCII.GetBytes(json);
        }

        public static Packet Deserialize(byte[] data)
        {
            string json = System.Text.Encoding.ASCII.GetString(data);
            return JsonConvert.DeserializeObject<Packet>(json);
        }

        public class Header
        {
            public PacketType Type { get; set; }
            public int SequenceNumber { get; set; }
            public DateTime TimeStamp { get; set; }
        }

        public class Body
        {
            public object Data { get; set; }
        }
    }

    class Session
    {
        public string Code { get; }
        public WebSocketContext Context { get; }

        public Session(string code, WebSocketContext context)
        {
            Code = code;
            Context = context;
        }
    }

    public class EchoAll : WebSocketBehavior
    {

        public static ConcurrentDictionary<string, List<string>> sessions = new ConcurrentDictionary<string, List<string>>();
        //private Session currentSession;
        private ActionState actionState = ActionState.Idle;
        protected override void OnOpen()
        {
            base.OnOpen();
            Console.WriteLine($"New connection opened. ID: {ID}");
            Logger.LogMessage($"New connection opened. ID: {ID}", "connections.log");
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            try
            {
                dynamic jsonPacket = JsonConvert.DeserializeObject(e.Data);
                Logger.LogGeneral(jsonPacket);

                PacketType type = (PacketType)jsonPacket.PacketHeader.Type;
                int sequenceNumber = jsonPacket.PacketHeader.SequenceNumber;
                DateTime timeStamp = jsonPacket.PacketHeader.TimeStamp;

                // Initialize bodyData as dynamic to handle various types of data
                dynamic bodyData = jsonPacket.PacketBody.Data;
                Packet receivedPacketToBeLogged = new Packet(type, sequenceNumber, timeStamp, bodyData);
                Logger.LogReceivedPacket(receivedPacketToBeLogged);

                switch (type)
                {
                    case PacketType.DrawingData:
                        actionState = ActionState.Drawing;
                        break;
                    case PacketType.AddText:
                        actionState = ActionState.Writing;
                        break;
                    case PacketType.AddImage:
                        actionState = ActionState.AddingImage;
                        break;
                    case PacketType.EstablishConnection:
                        actionState = ActionState.Connected;
                        break;
                    case PacketType.CloseConnection:
                        actionState = ActionState.Disconnected;
                        break;
                    case PacketType.JoinSession:
                        actionState = ActionState.Processing;
                        break;
                    case PacketType.CreateSession:
                        actionState = ActionState.Processing;
                        break;
                    default:
                        actionState = ActionState.Idle;
                        break;
                }

                Logger.LogInternalState(actionState);

                switch (type)
                {

                    case PacketType.CreateSession:
                        Console.WriteLine("Create session packet received.");
                        HandleCreateSessionPacket(sequenceNumber, timeStamp, bodyData);
                        actionState = ActionState.Idle;
                        break;
                    case PacketType.JoinSession:
                        Console.WriteLine("Join Session packet received.");
                        HandleJoinSessionPacket(sequenceNumber, timeStamp, bodyData);
                        actionState = ActionState.Idle;
                        break;
                    case PacketType.DrawingData:
                        Console.WriteLine("Drawing Data Packet Recieved");
                        HandleDrawingDataPacket(sequenceNumber, timeStamp, bodyData);
                        actionState = ActionState.Idle;
                        break;
                    case PacketType.EstablishConnection:
                        Console.WriteLine("Establish Connection Packet Recieved");
                        HandleEstablishConnectionPacket(sequenceNumber, timeStamp, bodyData);
                        actionState = ActionState.Idle;
                        break;
                    case PacketType.CloseConnection:
                        Console.WriteLine("Close connection packet received.");
                        HandleCloseConnectionPacket(sequenceNumber, timeStamp, bodyData);
                        actionState = ActionState.Idle;
                        break;
                    case PacketType.AddText:
                        Console.WriteLine("Add Text packet received.");
                        HandleAddTextPacket(sequenceNumber, timeStamp, bodyData);
                        actionState = ActionState.Idle;
                        break;
                    case PacketType.AddImage:
                        Console.WriteLine("Add Image packet received.");
                        HandleAddImagePacket(sequenceNumber, timeStamp, bodyData);
                        actionState = ActionState.Idle;
                        break;

                    default:
                        Console.WriteLine("Unknown packet type received.");
                        break;
                }

                Logger.LogInternalState(actionState);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error processing packet: " + ex.Message);
            }
        }

        public static void WriteSessionsToFile(string filePath)
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(filePath))
                {
                    foreach (var session in sessions)
                    {
                        writer.WriteLine($"Session: {session.Key}");
                        foreach (var value in session.Value)
                        {
                            writer.WriteLine(value);
                        }
                        writer.WriteLine(); // Add an empty line between sessions
                    }
                }
                Console.WriteLine("Sessions written to file successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing sessions to file: {ex.Message}");
            }
        }


        private void HandleCreateSessionPacket(int sequenceNumber, DateTime timeStamp, dynamic bodyData)
        {
            //Packet receivedPacket = new Packet(PacketType.CreateSession, sequenceNumber, timeStamp, bodyData);
            var code = GenerateCode();
            sessions.TryAdd(code, new List<string> { ID }); // Add session code and connection ID to sessions
            Packet sessionDetailsPacket = new Packet(PacketType.SessionDetails, sequenceNumber, timeStamp, code);

            var sessionDetailsPacketSerialized = sessionDetailsPacket.Serialize();


            Sessions.SendTo(sessionDetailsPacketSerialized, ID);

            Logger.LogSentPacket(sessionDetailsPacket);
            WriteSessionsToFile("sessions.log");
            //Send(sessionDetailsPacketSerialized);
            //Sessions.Broadcast(sessionDetailsPacketSerialized);
            //SendToSessionMembers(sessionDetailsPacketSerialized);

        }

        private void HandleEstablishConnectionPacket(int sequenceNumber, DateTime timeStamp, dynamic bodyData)
        {

            string message = "Connection Open";
            Packet establishConnectionPacket = new Packet(PacketType.EstablishConnection, 1, DateTime.Now, message); ;

            var establishConnectionPacketSerialized = establishConnectionPacket.Serialize();

            Send(establishConnectionPacketSerialized);
            Logger.LogSentPacket(establishConnectionPacket);

        }
        private void HandleCloseConnectionPacket(int sequenceNumber, DateTime timeStamp, dynamic bodyData)
        {
            // Close the WebSocket connection for this client
            Context.WebSocket.Close();
            Console.WriteLine($"Connection closed for client: {ID}");
        }

        private void HandleDrawingDataPacket(int sequenceNumber, DateTime timeStamp, dynamic bodyData)
        {
            Packet drawingDataPacket = new Packet(PacketType.DrawingData, sequenceNumber, timeStamp, bodyData);

            var drawingDataPacketSerialized = drawingDataPacket.Serialize();



            foreach (var sessionId in sessions.Keys)
            {
                if (sessions[sessionId].Contains(ID)) // Check if the current ID belongs to this session
                {
                    foreach (var connID in sessions[sessionId])
                    {
                        Sessions.SendTo(drawingDataPacketSerialized, connID);
                        Logger.LogSentPacket(drawingDataPacket);
                    }
                    break; // No need to check other sessions
                }
            }
        }

        private void HandleAddTextPacket(int sequenceNumber, DateTime timeStamp, dynamic bodyData)
        {
            Packet addTextPacket = new Packet(PacketType.AddText, sequenceNumber, timeStamp, bodyData);

            var addTextPacketSerialized = addTextPacket.Serialize();

            foreach (var sessionId in sessions.Keys)
            {
                if (sessions[sessionId].Contains(ID)) // Check if the current ID belongs to this session
                {
                    foreach (var connID in sessions[sessionId])
                    {
                        Sessions.SendTo(addTextPacketSerialized, connID);
                        Logger.LogSentPacket(addTextPacket);
                    }
                    break; // No need to check other sessions
                }
            }
        }

        private void HandleAddImagePacket(int sequenceNumber, DateTime timeStamp, dynamic bodyData)
        {
            Packet addImagePacket = new Packet(PacketType.AddImage, sequenceNumber, timeStamp, bodyData);

            var addImagePacketSerialized = addImagePacket.Serialize();

            foreach (var sessionId in sessions.Keys)
            {
                if (sessions[sessionId].Contains(ID)) // Check if the current ID belongs to this session
                {
                    foreach (var connID in sessions[sessionId])
                    {
                        Sessions.SendTo(addImagePacketSerialized, connID);
                        Logger.LogSentPacket(addImagePacket);
                    }
                    break; // No need to check other sessions
                }
            }
        }



        private void HandleJoinSessionPacket(int sequenceNumber, DateTime timeStamp, dynamic bodyData)
        {
            Console.WriteLine("Inside Handle Join Session packet function.");
            Packet receivedPacket = new Packet(PacketType.JoinSession, sequenceNumber, timeStamp, bodyData);
            string invitation_code = receivedPacket.PacketBody.Data.ToString();
            //sessions.TryAdd(invitation_code, new List<string> { ID }); // Add session code and connection ID to sessions
            if (sessions.TryGetValue(invitation_code, out List<string> connIDs))
            {
                connIDs.Add(ID); // Add current connection ID to the list of IDs for this session
                Console.WriteLine("1 Connection successfully added");
                string message = "You have been Successfully added to the Session!";
                Packet sessionDetailsPacket = new Packet(PacketType.SessionDetails, 1, DateTime.Now, message); ;

                var sessionDetailsPacketSerialized = sessionDetailsPacket.Serialize();

                Send(sessionDetailsPacketSerialized);
                Logger.LogSentPacket(sessionDetailsPacket);
                WriteSessionsToFile("sessions.log");

            }
            else
            {

                Console.WriteLine("Session not found.");
                string message = "Invitation code is not valid, Session not found.";
                Packet sessionDetailsPacket = new Packet(PacketType.SessionDetails, 1, DateTime.Now, message);

                var sessionDetailsPacketSerialized = sessionDetailsPacket.Serialize();

                Send(sessionDetailsPacketSerialized);
                Logger.LogSentPacket(sessionDetailsPacket);
            }
        }
        private static string GenerateCode()
        {
            Random random = new Random();
            return random.Next(100000, 999999).ToString();
        }

        private void SendToSessionMembers(string message)
        {
            foreach (var sessionId in sessions.Keys)
            {
                if (sessions[sessionId].Contains(ID)) // Check if the current ID belongs to this session
                {
                    foreach (var connID in sessions[sessionId])
                    {
                        Sessions.SendTo(message, connID);
                        Console.WriteLine("Sent:" + message);
                    }
                    break; // No need to check other sessions
                }
            }
        }
    }

    class Program
    {
        public static WebSocketServer wssv;


        public static void SendCloseConnectionPacket()
        {

            foreach (var sessionsValue in EchoAll.sessions.Values)
            {
                foreach (var connID in sessionsValue)
                {
                    // Create and send CloseConnection packet to each connection ID
                    Packet closePacket = new Packet(PacketType.CloseConnection, 1, DateTime.Now, "Server closing connection");
                    string closePacketSerialized = closePacket.Serialize();
                    wssv.WebSocketServices["/EchoAll"].Sessions.SendTo(closePacketSerialized, connID);
                    Logger.LogSentPacket(closePacket);
                }
            }
            Console.WriteLine("CloseConnection packets sent to all clients.");
        }


        public static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {

            SendCloseConnectionPacket();

            wssv.Stop();
            Console.WriteLine("WS server stopped.");


            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}