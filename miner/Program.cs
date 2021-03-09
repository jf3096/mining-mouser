using Gma.System.MouseKeyHook;
using System;
using System.Threading;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace miner
{
    class Program
    {
        static Process ExternalProcess = new Process();
        static DateTime datetime = DateTime.Now;

        static void Main(string[] args)
        {
            TextWriterTraceListener writer = new TextWriterTraceListener(Console.Out);
            Debug.Listeners.Add(writer);

            _handler += new EventHandler(Handler);
            SetConsoleCtrlHandler(_handler, true);

            ExternalProcess.StartInfo.WorkingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            ExternalProcess.StartInfo.FileName = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "NBMiner_Win/start_eth.bat");
            ExternalProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            StartMiner();

            var debounce = ThrottleDebounce.Debouncer.Debounce(() =>
            {
                StartMiner();
            }, new TimeSpan(0, 0, 300 /* 半小时 */));

            var throttle = ThrottleDebounce.Throttler.Throttle(() =>
            {
                StopMiner();
            }, new TimeSpan(0, 0, 10 /* 半小时 */));

            Hook.GlobalEvents().MouseMove += async (sender, e) =>
            {
                throttle.Run();
                debounce.Run();
            };

            AppDomain.CurrentDomain.ProcessExit += (object sender, EventArgs e) =>
            {
                StopMiner();
            };

            Application.ApplicationExit += (object sender, EventArgs e) =>
            {
                StopMiner();
            };

            Application.Run(new ApplicationContext());
        }

        static void StartMiner()
        {
            try
            {
                if (!ExternalProcess.HasExited)
                {
                    return;
                }
            }
            catch { }

            try
            {
                StopMiner();
            }
            catch { }
            finally
            {
                ExternalProcess.Start();
            }
        }

        static void StopMiner()
        {
            var thread = new Thread(()=>
            {
                var now = DateTime.Now;

                datetime = now;
                try
                {
                    if (!ExternalProcess.HasExited)
                    {
                        try
                        {
                            KillProcessAndChildrens(ExternalProcess.Id);
                        }
                        catch { }

                        //Debug.WriteLine("Stop: " + ExternalProcess.Id);
                        try
                        {
                            //KillProcessAndChildrens(ExternalProcess.Id);
                            ExternalProcess.Close();
                        }
                        catch { }

                        try
                        {
                            //KillProcessAndChildrens(ExternalProcess.Id);
                            ExternalProcess.Kill();
                        }
                        catch { }
                    }
                }
                catch { }
            });
            thread.IsBackground = true;
            thread.Start();
        }
        [DllImport("Kernel32")]
        private static extern bool SetConsoleCtrlHandler(EventHandler handler, bool add);

        private delegate bool EventHandler(CtrlType sig);
        static EventHandler _handler;

        enum CtrlType
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT = 1,
            CTRL_CLOSE_EVENT = 2,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT = 6
        }

        private static bool Handler(CtrlType sig)
        {
            switch (sig)
            {
                case CtrlType.CTRL_C_EVENT:
                case CtrlType.CTRL_LOGOFF_EVENT:
                case CtrlType.CTRL_SHUTDOWN_EVENT:
                case CtrlType.CTRL_CLOSE_EVENT:
                default:
                    StopMiner();
                    return false;
            }
        }

        private static void KillProcessAndChildrens(int pid)
        {
            ManagementObjectSearcher processSearcher = new ManagementObjectSearcher
              ("Select * From Win32_Process Where ParentProcessID=" + pid);
            ManagementObjectCollection processCollection = processSearcher.Get();

            try
            {
                Process proc = Process.GetProcessById(pid);
                if (!proc.HasExited)
                {
                    proc.Kill();
                }
            }
            catch (ArgumentException)
            {
                // Process already exited.
            }

            if (processCollection != null)
            {
                foreach (ManagementObject mo in processCollection)
                {
                    KillProcessAndChildrens(Convert.ToInt32(mo["ProcessID"])); //kill child processes(also kills childrens of childrens etc.)
                }
            }
        }
    }
}


