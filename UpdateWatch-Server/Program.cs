using System;
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
    class Program
    {
        public const string dataSource = "UpdateWatch.sqlite";
        static SQLiteConnection connectionTest = new SQLiteConnection();

        static Thread thread;

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

        private static void listen()
        {
            UWUpdate.clSendData sendData = new UWUpdate.clSendData();
            IPAddress ipaddress = new IPAddress(0x00000000L);
            string clientIP;
            long hostID = -1;

            //            SQLiteConnection connection = new SQLiteConnection();
            //            connection.ConnectionString = "Data Source=" + dataSource;
            //            connection.Open();
            SQLiteConnection connection = connectionTest;
            SQLiteCommand command = new SQLiteCommand(connection);

            TcpListener listener = new TcpListener(ipaddress, 4584);
            listener.Start();
            TcpClient c = listener.AcceptTcpClient();
            Stream networkStream = c.GetStream();

            XmlSerializer xmlserializer = new XmlSerializer(typeof(UWUpdate.clSendData));
            object obj = xmlserializer.Deserialize(networkStream);
            sendData = (UWUpdate.clSendData)obj;

            String[] tmp = c.Client.RemoteEndPoint.ToString().Split((':'));
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

                command.CommandText = "INSERT INTO Updates (Title, Description, ReleaseNotes, SupportUrl) VALUES ('" + update.Title + "', '" + update.Description + "', " +
                    "'" + update.ReleaseNotes + "', '" + update.SupportUrl + "');";
                command.ExecuteNonQuery();
                updateID = connection.LastInsertRowId;
                command.CommandText = "INSERT INTO HostUpdates (HostID, UpdateID) VALUES ('" + hostID + "', '" + updateID + "');";
                command.ExecuteNonQuery();
            }

            command.Dispose();

            connection.Close();
            connection.Dispose();

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
                Console.WriteLine("Description: " + update.Description);
                Console.WriteLine("ReleaseNotes: " + update.ReleaseNotes);
                Console.WriteLine("SupportUrl: " + update.SupportUrl);
                Console.WriteLine("Title: " + update.Title);
            }
            Console.WriteLine("");

            //c.Close();
            //listener.Stop();
        }

        static void Main(string[] args)
        {
            connectionTest.ConnectionString = "Data Source=" + dataSource;
            connectionTest.Open();

            Thread th = new Thread(new ThreadStart(listen));
            thread = th;

            th.Start();
        }
    }
}
