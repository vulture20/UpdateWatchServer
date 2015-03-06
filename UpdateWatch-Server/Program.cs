using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.Text;
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
        // Speichert die Verbindung zum Client und startet den Thread
        public ServerThread(TcpClient connection)
        {
            // Speichert die Verbindung zum Client,
            // um sie später schließen zu können
            this.connection = connection;
            // Initialisiert und startet den Thread
            new Thread(new ThreadStart(Run)).Start();
        }
        // Der eigentliche Thread
        public void Run()
        {
            UWUpdate.clSendData sendData = new UWUpdate.clSendData();
            string clientIP;
            long hostID = -1;

            Console.WriteLine("### neuer Thread ###");

            // Setze Flag für "Thread läuft"
            this.running = true;
            // Hole den Stream für's schreiben
            Stream networkStream = this.connection.GetStream();

            SQLiteConnection connection = Program.sqlConnection;
            SQLiteCommand command = new SQLiteCommand(connection);

            XmlSerializer xmlserializer = new XmlSerializer(typeof(UWUpdate.clSendData));
            object obj = xmlserializer.Deserialize(networkStream);
            sendData = (UWUpdate.clSendData)obj;

            String[] tmp = this.connection.Client.RemoteEndPoint.ToString().Split((':'));
            if (tmp.Count() == 2)
            {
                clientIP = tmp[0];
            }
            else
            {
                clientIP = "";
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

            Console.WriteLine("### Thread beendet ###");
        }

        private static void deleteUpdates(long hostID, SQLiteConnection connection)
        {
            SQLiteCommand command = new SQLiteCommand(connection);
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
    }

    class Program
    {
        private const string dataSource = "C:\\Users\\Schröpel\\Documents\\Visual Studio 2013\\Projects\\UpdateWatch-Server\\UpdateWatch-Server\\bin\\Debug\\UpdateWatch.sqlite";
        public static SQLiteConnection sqlConnection = new SQLiteConnection();
        private static ArrayList threads = new ArrayList();
        private static IPAddress ipaddress = new IPAddress(0x00000000L);
        private static TcpListener listener = new TcpListener(ipaddress, 4584);

        public static void Run()
        {
            while (true)
            {
                // Wartet auf eingehenden Verbindungsversuch
                TcpClient c = listener.AcceptTcpClient();
                // Initialisiert und startet einen Server-Thread
                // und fügt ihn zur Liste der Server-Threads hinzu
                threads.Add(new ServerThread(c));
            }
        }

        static void Main(string[] args)
        {
            // Listener starten
            listener.Start();

            sqlConnection.ConnectionString = "Data Source=" + dataSource;
            sqlConnection.Open();

            Console.WriteLine("Starting...");

            // Haupt-Server-Thread initialisieren und starten
            Thread th = new Thread(new ThreadStart(Run));
            th.Start();

            Console.WriteLine("System running; press any key to stop");
            Console.ReadKey(true);

            th.Abort();
            for (IEnumerator e = threads.GetEnumerator(); e.MoveNext(); )
            {
                ServerThread st = (ServerThread)e.Current;
                st.stop = true;
                while (st.running)
                    Thread.Sleep(1000);
            }
            listener.Stop();

            sqlConnection.Close();
            sqlConnection.Dispose();

            Console.WriteLine("System stopped");
        }
    }
}
