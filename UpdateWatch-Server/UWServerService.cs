using System;
using System.Collections;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace UpdateWatch_Server
{
    class ServerThread
    {
        // Stop-Flag
        public bool stop = false;
        // Flag für "Thread läuft"
        public bool running = false;
        // Die Verbindung zum Client
        private TcpClient connection = null;
        // Die SQL-Verbindung zur Datenbank
        private SQLiteConnection sqlConnection = null;
        private static EventLog eventLog = new EventLog("Application");
        // Speichert die Verbindung zum Client und startet den Thread
        public ServerThread(TcpClient _connection, SQLiteConnection _sqlConnection)
        {
            // Speichert die Verbindung zum Client,
            // um sie später schließen zu können
            connection = _connection;
            sqlConnection = _sqlConnection;
            // Initialisiert und startet den Thread
            eventLog.Source = "UpdateWatch-Server";
            new Thread(new ThreadStart(Run)).Start();
        }
        // Der eigentliche Thread
        public void Run()
        {
            UWUpdate.clSendData sendData = new UWUpdate.clSendData();
            string clientIP;
            long hostID = -1;

            // Setze Flag für "Thread läuft"
            this.running = true;
            // Hole den Stream für's schreiben
            Stream networkStream = this.connection.GetStream();

            SQLiteConnection connection = sqlConnection;
            SQLiteCommand command = new SQLiteCommand(connection);

            XmlSerializer xmlserializer = new XmlSerializer(typeof(UWUpdate.clSendData));
            object obj = xmlserializer.Deserialize(networkStream);
            sendData = (UWUpdate.clSendData)obj;

            String[] tmp = this.connection.Client.RemoteEndPoint.ToString().Split((':'));
            if (tmp.Count() == 2)
            {
                clientIP = tmp[0];
                eventLog.WriteEntry("Got update from: " + sendData.machineName + "(" + clientIP + ")");
            }
            else
            {
                clientIP = "";
                eventLog.WriteEntry("Couldn't determine client ip!", EventLogEntryType.Warning);
            }

            command.CommandText = "SELECT ID FROM Hosts WHERE IP='" + clientIP + "';";
            SQLiteDataReader reader = command.ExecuteReader();
            if (!reader.HasRows)
            {
                reader.Close();
                reader.Dispose();
                command.CommandText = "INSERT INTO Hosts (IP, machineName, dnsName, osVersion, tickCount, updateCount, lastChange) VALUES (" +
                    "'" + clientIP + "', '" + sendData.machineName + "', '" + sendData.dnsName + "', '" + sendData.osVersion + "', '" + sendData.tickCount + "', " +
                    "'" + sendData.updateCount + "', datetime('now', 'localtime'));";
                command.ExecuteNonQuery();
                hostID = connection.LastInsertRowId;
            }
            else
            {
                if (reader.Read())
                    hostID = long.Parse(reader["ID"].ToString());
                reader.Close();
                reader.Dispose();
                command.CommandText = "UPDATE Hosts SET machineName='" + sendData.machineName + "', dnsName='" + sendData.dnsName + "', osVersion='" + sendData.osVersion + "', " +
                    "tickCount='" + sendData.tickCount + "', updateCount='" + sendData.updateCount + "', lastChange=datetime('now', 'localtime') WHERE IP='" + clientIP + "'";
                command.ExecuteNonQuery();
            }

            deleteUpdates(hostID, connection);

            foreach (UWUpdate.WUpdate update in sendData.wUpdate)
            {
                long updateID;

                command.CommandText = "INSERT INTO Updates (Title, Description, ReleaseNotes, SupportUrl, UpdateID, RevisionNumber, isMandatory, isUninstallable, KBArticleIDs, " +
                    "MsrcSeverity, Type) VALUES ('" + update.Title + "', '" + update.Description + "', '" + update.ReleaseNotes + "', '" + update.SupportUrl + "', " +
                    "'" + update.UpdateID + "', '" + update.RevisionNumber + "', '" + update.isMandatory.ToString() + "', '" + update.isUninstallable + "', " +
                    "'" + update.KBArticleIDs + "', '" + update.MsrcSeverity + "', '" + update.Type + "');";
                command.ExecuteNonQuery();
                updateID = connection.LastInsertRowId;
                command.CommandText = "INSERT INTO HostUpdates (HostID, UpdateID) VALUES ('" + hostID + "', '" + updateID + "');";
                command.ExecuteNonQuery();
            }

            command.Dispose();

            if (Program.console)
            {
                Console.WriteLine("dnsName: " + sendData.dnsName);
                Console.WriteLine("IP: " + clientIP);
                Console.WriteLine("HostID: " + hostID);
                Console.WriteLine("machineName: " + sendData.machineName);
                Console.WriteLine("osVersion: " + sendData.osVersion);
                Console.WriteLine("TickCount: " + sendData.tickCount.ToString());
                Console.WriteLine("updateCount: " + sendData.updateCount.ToString());
                foreach (UWUpdate.WUpdate update in sendData.wUpdate)
                {
                    Console.WriteLine("-----------");
                    Console.WriteLine("Title: " + update.Title);
                    Console.WriteLine("Description: " + update.Description);
                    Console.WriteLine("ReleaseNotes: " + update.ReleaseNotes);
                    Console.WriteLine("SupportUrl: " + update.SupportUrl);
                    Console.WriteLine("UpdateID: " + update.UpdateID);
                    Console.WriteLine("RevisionNumber: " + update.RevisionNumber);
                    Console.WriteLine("isMandatory: " + update.isMandatory);
                    Console.WriteLine("isUninstallable: " + update.isUninstallable);
                    Console.WriteLine("KBArticleIDs: " + update.KBArticleIDs);
                    Console.WriteLine("MsrcSeverity: " + update.MsrcSeverity);
                    Console.WriteLine("Type: " + update.Type);
                }
                Console.WriteLine("");
            }
        }

        private static void deleteUpdates(long hostID, SQLiteConnection connection)
        {
            SQLiteCommand command = new SQLiteCommand(connection);
            try
            {
                command.CommandText = "BEGIN;";
                command.ExecuteNonQuery();
                command.CommandText = "SELECT * FROM HostUpdates WHERE HostID='" + hostID + "';";
                SQLiteDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    SQLiteCommand deleteCommand = new SQLiteCommand(connection);
                    deleteCommand.CommandText = "DELETE FROM Updates WHERE ID='" + reader["UpdateID"] + "';";
                    deleteCommand.ExecuteNonQuery();
                    deleteCommand.Dispose();
                }
                reader.Close();
                reader.Dispose();
                command.CommandText = "DELETE FROM HostUpdates WHERE HostID='" + hostID + "';";
                command.ExecuteNonQuery();
                command.CommandText = "END;";
                command.ExecuteNonQuery();
                command.Dispose();
            }
            catch (SQLiteException ex)
            {
                eventLog.WriteEntry("Couldn't delete updates from db: " + ex.Message, EventLogEntryType.Warning);
                if (Program.console)
                {
                    Console.WriteLine("Couldn't delete updates from db: " + ex.Message);
                }
            }
            finally
            {
                command.Dispose();
            }
        }
    }

    public partial class UWServerService : ServiceBase
    {
        private static UWConfig config = new UWConfig();
        private static EventLog eventLog = new EventLog("Application");
        public static SQLiteConnection sqlConnection = new SQLiteConnection();
        private static ArrayList threads = new ArrayList();
        private static IPAddress ipaddress;
        private static TcpListener listener;
        private static Thread thread;
        private static string configfile;
        private static string dataSource;

        public UWServerService()
        {
            this.ServiceName = "UpdateWatch-Server";
            this.CanStop = true;
            this.CanPauseAndContinue = false;
            this.AutoLog = true;
            eventLog.Source = "UpdateWatch-Server";
        }

        private static void getConfig()
        {
            configfile = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "\\UWServerConfig.xml";
            if (!File.Exists(configfile))
            {
                try
                {
                    eventLog.WriteEntry("No config file found. Creating new one...", EventLogEntryType.Warning);

                    FileStream fileStream = new FileStream(configfile, FileMode.CreateNew);
                    XmlSerializer xmlSerializer = new XmlSerializer(typeof(UWConfig));

                    xmlSerializer.Serialize(fileStream, config);

                    fileStream.Close();
                }
                catch (Exception ex)
                {
                    eventLog.WriteEntry("Error at creating the config file: " + ex.InnerException.Message, EventLogEntryType.Error);
                    if (Program.console)
                    {
                        Console.WriteLine("Create Config: " + ex.Message);
                    }
                    else
                        throw;
                }
            }
            try
            {
                eventLog.WriteEntry("Reading config file...", EventLogEntryType.Information);

                StreamReader sr = new StreamReader(configfile, true);
                XmlSerializer xmlSerializer = new XmlSerializer(typeof(UWConfig));

                config = (UWConfig)xmlSerializer.Deserialize(sr);

                sr.Close();
            }
            catch (Exception ex)
            {
                eventLog.WriteEntry("Error at reading the config file: " + ex.InnerException.Message, EventLogEntryType.Error);
                if (Program.console)
                {
                    Console.WriteLine("Read config: " + ex.Message);
                }
                else
                    throw;
            }
        }

        public static void Run()
        {
            while (true)
            {
                // Wartet auf eingehenden Verbindungsversuch
                TcpClient client = listener.AcceptTcpClient();
                // Initialisiert und startet einen Server-Thread
                // und fügt ihn zur Liste der Server-Threads hinzu
                threads.Add(new ServerThread(client, sqlConnection));
            }
        }

        public static void initializeService()
        {
            eventLog.Source = "UpdateWatch-Server";

            getConfig();

            dataSource = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "\\" + config.dbFile;

            try
            {
                ipaddress = IPAddress.Parse(config.bindIP);
            }
            catch (ArgumentNullException ex)
            {
                eventLog.WriteEntry("Error at parsing IP from config file: " + ex.Message, EventLogEntryType.Error);
                if (Program.console)
                    Console.WriteLine("Error at parsing IP from config file: " + ex.Message);
                throw;
            }
            catch (FormatException ex)
            {
                eventLog.WriteEntry("Error at parsing IP from config file: " + ex.Message, EventLogEntryType.Error);
                if (Program.console)
                    Console.WriteLine("Error at parsing IP from config file: " + ex.Message);
                throw;
            }
            try
            {
                listener = new TcpListener(ipaddress, config.bindPort);
                // Listener starten
                listener.Start();
            }
            catch (SocketException ex)
            {
                eventLog.WriteEntry("Error at creating tcp listener: " + ex.Message, EventLogEntryType.Error);
                if (Program.console)
                    Console.WriteLine("Error at creating tcp listener: " + ex.Message);
                throw;
            }

            try
            {
                sqlConnection.ConnectionString = "Data Source=" + dataSource;
                sqlConnection.Open();
            }
            catch (Exception ex)
            {
                eventLog.WriteEntry("Error at opening the database: " + ex.Message, EventLogEntryType.Error);
                if (Program.console)
                    Console.WriteLine("Error at opening the database: " + ex.Message);
                throw;
            }

            // Haupt-Server-Thread initialisieren und starten
            thread = new Thread(new ThreadStart(Run));
            thread.Start();
        }

        public static void terminateService()
        {
            thread.Abort();
            for (IEnumerator e = threads.GetEnumerator(); e.MoveNext(); )
            {
                ServerThread serverThread = (ServerThread)e.Current;
                serverThread.stop = true;
                while (serverThread.running)
                    Thread.Sleep(1000);
            }
            listener.Stop();

            sqlConnection.Close();
            sqlConnection.Dispose();
        }

        protected override void OnStart(string[] args)
        {
            eventLog.WriteEntry("Starting service...", EventLogEntryType.Information);
            initializeService();

            base.OnStart(args);
        }

        protected override void OnStop()
        {
            eventLog.WriteEntry("Stopping service...", EventLogEntryType.Information);
            terminateService();

            base.OnStop();
        }
    }
}
