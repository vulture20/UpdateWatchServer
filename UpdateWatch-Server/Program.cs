using System;
using System.Collections;
using System.ServiceProcess;
using System.Configuration.Install;

namespace UpdateWatch_Server
{
    class Program
    {
        public static bool console = false;

        static int Main(string[] args)
        {
            bool install = false, uninstall = false, rethrow = false;

            try
            {
                foreach (string arg in args)
                {
                    switch (arg)
                    {
                        case "-i":
                        case "-install":
                            install = true; break;
                        case "-u":
                        case "-uninstall":
                            uninstall = true; break;
                        case "-c":
                        case "-console":
                            console = true; break;
                        default:
                            Console.Error.WriteLine("Argument not expected: " + arg);
                            break;
                    }
                }
                if (uninstall)
                    Install(true, args);
                if (install)
                    Install(false, args);
                if (console)
                {
                    Console.WriteLine("Starting...");

                    UWServerService.initializeService();

                    Console.WriteLine("System running; press any key to stop");
                    Console.ReadKey(true);

                    UWServerService.terminateService();
                    Console.WriteLine("System stopped");
                }
                else if (!(install || uninstall))
                {
                    rethrow = true; // so that windows sees error...
                    System.ServiceProcess.ServiceBase.Run(new UWServerService());
                    rethrow = false;
                }
                return 0;
            }
            catch (Exception ex)
            {
                if (rethrow) throw;
                Console.Error.WriteLine(ex.InnerException.Message);
                return -1;
            }
        }

        static void Install(bool undo, string[] args)
        {
            try
            {
                Console.WriteLine(undo ? "Uninstalling..." : "Installing...");
                using (AssemblyInstaller inst = new AssemblyInstaller(typeof(UWServerInstaller).Assembly, args))
                {
                    IDictionary state = new Hashtable();
                    inst.UseNewContext = true;
                    try
                    {
                        if (undo)
                        {
                            inst.Uninstall(state);
                        }
                        else
                        {
                            inst.Install(state);
                            inst.Commit(state);

                            ServiceController service = new ServiceController("UpdateWatch-Server");
                            if (service.Status.Equals(ServiceControllerStatus.Stopped))
                                service.Start();
                        }
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            inst.Rollback(state);
                        }
                        catch
                        {
                            Console.Error.WriteLine(ex.Message);
                        }
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);    
            }
        }
    }
}
