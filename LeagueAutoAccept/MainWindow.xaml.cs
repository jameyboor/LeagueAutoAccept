using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Drawing;
using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.CvEnum;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO;
using System.ComponentModel;
using Newtonsoft.Json;

namespace LeagueAutoAccept
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
    static class Extender
    {
        public static void InvokeIfRequired(this Control control, Action action)
        {
            if (!control.Dispatcher.CheckAccess())
            {
                control.Dispatcher.Invoke(action);
            }
            else {
                action();
            }
        }
    }

    public partial class MainWindow : Window
    {
        public ConfigFile config;
        public string nativeTitle;

        public List<string> queueModes = new List<string>()
        {
            "Ranked",
            "Normal"
        };

        public Dictionary<string, string> images = new Dictionary<string, string>();
        public BindingList<string> champions = new BindingList<string>();

        public bool isRunning = false;

        public MainWindow()
        {
            InitializeComponent();
            logBox.FontSize -= 2;
        }

        private bool CheckMatch(Mat image, Mat template, ref System.Drawing.Point loc, int threshhold = 80)
        {
            System.Drawing.Point matchLoc;
            Mat result = new Mat();
            CvInvoke.MatchTemplate(image, template, result, TemplateMatchingType.CcoeffNormed);
            double minVal = 0;
            double maxVal = 0;
            System.Drawing.Point minLoc = new System.Drawing.Point();
            System.Drawing.Point maxLoc = new System.Drawing.Point();

            CvInvoke.MinMaxLoc(result, ref minVal, ref maxVal, ref minLoc, ref maxLoc, new Mat());
            matchLoc = maxLoc;

            if(maxVal*100 > threshhold) // at least x% accurate
            {
                loc = matchLoc;
                return true;
            }
            return false;
        }

        private void InitImageDictionary()
        {
            images.Clear();
            string selectedMode = "ranked"; // TODO : Hardcoded, had dropdown list but queue button is same across all queues now so probably don't need it.
            selectedMode = String.Concat(selectedMode.ToLower(), "_");
            foreach(var file in Directory.GetFiles("images"))
            {
                if(!file.Contains("_")) //not dependant, just add.
                {
                    images.Add(System.IO.Path.GetFileNameWithoutExtension(file).ToLower(), file);
                    continue;
                }

                var fileExt = System.IO.Path.GetExtension(file);
                var startIndex = file.IndexOf(selectedMode) + selectedMode.Length;
                var endIndex = file.LastIndexOf(fileExt);
                var key = file.Substring(startIndex, endIndex - startIndex);
                images.Add(key, file);
            }
        }

        Bitmap GetClientScreenShot(int wait)
        {
            var league = GetLeague();
            if (league == null)
                throw new InvalidOperationException("League client not running!");

            WindowHelper.BringProcessToFront(league);
            Thread.Sleep(wait);
            WindowHelper.RECT rect = new WindowHelper.RECT();
            WindowHelper.GetWindowRect(league.MainWindowHandle, out rect);
            System.Drawing.Size size = new System.Drawing.Size(Math.Abs(rect.Left - rect.Right), Math.Abs(rect.Top - rect.Bottom));
            var screenshot = new Bitmap(size.Width, size.Height);
            var graph = Graphics.FromImage(screenshot);
            graph.CopyFromScreen(rect.Left, rect.Top, 0, 0, size, CopyPixelOperation.SourceCopy);
            graph.Dispose();
            return screenshot;
        }

        Mat GetMatFromPath(string filename)
        {
            return CvInvoke.Imread(filename, LoadImageType.Color);
        }

        void TransformStart()
        {
            this.InvokeIfRequired(() =>
            {
                if ((string)startButton.Content == "Start")
                {
                    Log("Starting Acceptor..");
                    startButton.Content = "Stop";
                    Title = nativeTitle + " - Started";
                }
                else
                {
                    Log("Stopping Acceptor..");
                    startButton.Content = "Start";
                    Title = nativeTitle + " - Not Started";
                }
            });
            isRunning = !isRunning;
        }

        bool IsStarted()
        {
            return isRunning;
        }

        void Log(string text)
        {
            this.InvokeIfRequired(() =>
            {
                logBox.AppendText($"[{DateTime.Now.ToString("HH:mm:ss")}] {text} {Environment.NewLine}");
            });
        }

        void DoTranslationMouseClick(Mat template, System.Drawing.Point location, IntPtr handle)
        {
            int new_X = location.X;
            new_X += template.Width / 2;

            int new_Y = location.Y;
            new_Y += template.Height / 2;
            WindowHelper.DoMouseClick(new_X, new_Y, handle);
        }

        private bool IsInQueue()
        {
            bool cancelqueueMatch = false;
            using (var img = new Image<Bgr, Byte>(GetClientScreenShot(config.ScreenCaptureWaitTime)))
            {
                cancelqueueMatch = CheckMatch(img.Mat, GetMatFromPath(images["cancelqueue"]), ref pnt, 90);
            }
            return cancelqueueMatch;
        }

        private void startButton_Click(object sender, RoutedEventArgs e)
        {
            bool isStarted = IsStarted();
            TransformStart();
            if (!isStarted)
            {
                SaveConfig();
                if (GetLeague() == null)
                {
                    Log("ERROR : Couldn't find LoL client.");
                    Dispatcher.Invoke(() => { CustomMessageBox.Show("No League of Legends client found", "Please have the League of Legends client open."); });
                    TransformStart();
                    return;
                }

                var task = Task.Run(() =>
                {
                    var processHandle = GetLeague().MainWindowHandle;
                    //first things first, find out if we're in queue
                    System.Drawing.Point pnt = new System.Drawing.Point();
                    if (!IsInQueue())
                    {
                        Log("ERROR : You are not in queue.");
                        Dispatcher.Invoke(() => { CustomMessageBox.Show("You are not in queue!", "You're not in a game queue."); });
                        TransformStart();
                        return;
                    }

                    var template = GetMatFromPath(images["accept"]);
                    while (isRunning) // keep on waiting for queue changes...
                    {
                        System.Drawing.Point location = new System.Drawing.Point();                        
                        bool acceptButton = false;
                        using (var img = new Image<Bgr, Byte>(GetClientScreenShot(config.ScreenCaptureWaitTime)))
                        {
                           acceptButton = CheckMatch(img.Mat, template, ref location, 70);
                        }

                        if (acceptButton) // match found, let's enter champ select
                        {
                            Log("Found Match, accepting..");
                            DoTranslationMouseClick(template, location, processHandle);

                            Thread.Sleep(9000);

                            //re-check if we're in queue, if we still are, someone didn't accept..

                            if (IsInQueue())
                                Log("Someone declined this queue..");
                            else
                                break; // we're in select, stop trying to enter queue..
                        }
                        Thread.Sleep(config.CheckQueueTimeout);
                    }
                    template.Dispose();

                    TransformStart();
                    //TODO : figure out our position in champion select..
                });
            }
        }

        private void SetConfigDefaults()
        {
            config.CurrentChampion = "Ahri";
            config.CheckQueueTimeout = 2000;
            config.ScreenCaptureWaitTime = 100;
        }

        [Obsolete]
        void FillChampions()
        {
            var process = GetLeague();
            if (process == null)
                return;

            config.LeagueFolder = System.IO.Path.GetDirectoryName(process.MainModule.FileName);
            string dirName = System.IO.Path.GetDirectoryName(process.MainModule.FileName) + System.IO.Path.DirectorySeparatorChar + @"assets\sounds\en_US\champions";
            var champNames = Directory.GetFiles(dirName).ToList();
            for (int i = 0; i < champNames.Count; ++i)
            {
                champNames[i] = System.IO.Path.GetFileNameWithoutExtension(champNames[i]);
            }

            config.Champions = champNames;
            process.Dispose();
        }

        [Obsolete]
        private void UseConfigValues()
        {
            foreach (string champ in config.Champions)
            {
                champions.Add(champ);
            }
        }


        public Process GetLeague()
        {
            var processes = Process.GetProcessesByName("LeagueClientUx");
            return processes.FirstOrDefault();
        }


        private void SaveConfig()
        {
            string json = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText("config.json", json);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (!File.Exists("config.json"))
            {
                Log("Creating configuration file..");
                config = new ConfigFile();
                SetConfigDefaults();
                SaveConfig();
            }
            else
                config = JsonConvert.DeserializeObject<ConfigFile>(File.ReadAllText("config.json"));

            nativeTitle = Title;
            Title = nativeTitle + " - Not Started";

            InitImageDictionary();
        }

        private void aboutButton_Click(object sender, RoutedEventArgs e)
        {
            CustomMessageBox.Show("About", "League of Legends Auto Accept\nLeague of Legends Auto Accept does NOT violate any current(2017) Terms of Service by Riot Games and thus is NOT bannable.\n\n Developed by Jameyboor © 2017 All Rights Reserved.");
        }
    }

    public static class WindowHelper
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        public static void BringProcessToFront(Process process)
        {
            IntPtr handle = FindWindow(null, process.MainWindowTitle);
            if (handle != IntPtr.Zero)
            {
                if (IsIconic(handle))
                    ShowWindow(handle, SW_RESTORE);
                SetForegroundWindow(handle);
            }
        }

        const int SW_RESTORE = 9;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("User32.dll")]
        private static extern bool SetForegroundWindow(IntPtr handle);
        [DllImport("User32.DLL", CharSet = CharSet.Unicode)]
        public static extern IntPtr FindWindow(String lpClassName, String lpWindowName);
        [DllImport("User32.dll")]
        private static extern bool ShowWindow(IntPtr handle, int nCmdShow);
        [DllImport("User32.dll")]
        private static extern bool IsIconic(IntPtr handle);
        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint cButtons, uint dwExtraInfo);

        [DllImport("user32.dll")]
        static extern bool ClientToScreen(IntPtr hWnd, ref System.Drawing.Point lpPoint);

        [DllImport("user32.dll")]
        static extern bool SetCursorPos(int x, int y);

        private const int MOUSEEVENTF_LEFTDOWN = 0x02;
        private const int MOUSEEVENTF_LEFTUP = 0x04;
        private const int MOUSEEVENTF_RIGHTDOWN = 0x08;
        private const int MOUSEEVENTF_RIGHTUP = 0x10;

        public static void DoMouseClick(int X, int Y, IntPtr hwd)
        {
            System.Drawing.Point pnt = new System.Drawing.Point(X, Y);
            ClientToScreen(hwd, ref pnt);
            SetCursorPos(pnt.X, pnt.Y);
            mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, (uint)pnt.X, (uint)pnt.Y, 0, 0);
        }
    }
}
