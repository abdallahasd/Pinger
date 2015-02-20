using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using System.Text.RegularExpressions;
using System.Net.NetworkInformation;
using System.Net;
using System.Runtime.InteropServices;


namespace Pinger
{
    public partial class Form1 : Form
    {

        #region Variables...

        public static string pingAddress = "8.8.8.8";       //Starting IP address                    
        public static Dictionary<string, int> map;          //Dictionary to map IP addresses to Ping Array
        public static List<string> IPList;                  //List of IP Addresses
        public static List<int>[] pings;                    //List of Ping Arrays
        public static bool started = false;                 //Flag for if the "Start Test" Button has been pressed for the first time
        public static bool started2 = false;                //Flag for if the test is currently running
        public static bool flagPingerReady = true;         //Flag for if Pinger is ready to start pinging
        public static bool flagPingerWaiting = false;       //Flag for if another pinger thread is waiting to start


        #endregion


        #region Import
        [DllImport("user32.dll")]
        public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
        #endregion

        #region Initialization
        public Form1()
        {
            InitializeComponent();

            //Set up images
            panel1.Visible = false;                                     //Loading Screen
            pictureBox1.Image = Properties.Resources.loading;           //Loading Image
            pictureBox2.Image = Properties.Resources.exit;              //Close Button
            pictureBox3.Image = Properties.Resources.min;               //Minimize Button

            //Set up events
            pictureBox2.Click += pictureBox2_Click;                     //Close button Event
            pictureBox3.Click += pictureBox3_Click;                     //Min Button Event
            this.backgroundWorker2.RunWorkerCompleted += backgroundWorker2_RunWorkerCompleted;      //Background process completed Event
            this.backgroundWorker1.RunWorkerCompleted += backgroundWorker1_RunWorkerCompleted;      //Background process completed Event
            this.Resize += Form1_Resize;                                //Form resize event
            backgroundWorker2.WorkerSupportsCancellation = true;

            //Resize columns to match fields
            foreach(ColumnHeader cH in listView1.Columns)
            {
                cH.AutoResize(ColumnHeaderAutoResizeStyle.HeaderSize);
            }
            
            //Create list tools
            map = new Dictionary<string, int>();
            IPList = new List<string>();
            pings = new List<int>[99];
            
        }




        #endregion


        #region Primary Methods...


        /// <summary>
        /// Gets a list of IPs resulting from a tracert to specified IP Address
        /// </summary>
        /// <param name="IP">IP Address to trace</param>
        /// <returns></returns>
        public static List<string> getIPs(string IP)
        {
            //Run Tracert to selected IP address
            string tracert = openapp("cmd", " /c \"tracert -d " + IP + "\"");

            //create list for output
            List<string> op = new List<string>();

            //Run Tracert to default IP if provided IP doesn't work.
            if (!tracert.Contains("Tracing route to") && !tracert.Contains(" ms "))
            {
                tracert = openapp("cmd", " /c \"tracert -d 8.8.8.8\"");
                pingAddress = IP = "8.8.8.8";
            }

            //Create building blocks for splitting Tracert Return
            char[] spc = { ' ' };
            char[] nl = { '\n' };
            char[] brk = { '[', ']' };

            //Split Tracert by line
            string[] byLine = tracert.Split(nl, StringSplitOptions.RemoveEmptyEntries);

            foreach (string blip in byLine)
            {
                if (blip.Contains(" ms "))
                {
                    //take lines that have time returns and split by column
                    string[] bySpc = blip.Split(spc, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string blop in bySpc)
                    {
                        //Separate IP addresses from Tracert and add to output List
                        if (!blip.Contains("[") && blop.Contains("."))
                        {
                            op.Add(blop);
                        }
                        else
                        {
                            if (blip.Contains("[") && blop.Contains("["))
                            {
                                op.Add(blop.Replace("[", "").Replace("]", ""));
                            }
                        }
                        
                    }
                }
            }


            return op;

        }


        /// <summary>
        /// Run the Tracert in the background
        /// </summary>
        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            //Get IP Addresses and add them to IPList
            IPList = getIPs(pingAddress);
        }



        /// <summary>
        /// Run the pings in the background
        /// </summary>
        private void backgroundWorker2_DoWork(object sender, DoWorkEventArgs e)
        {
            flagPingerReady = false;
            //Creat a list for the pings
            Dictionary<string, int> pingsList = new Dictionary<string, int>();

            //Fill Pingslist using the IP list
            pingsList = runPings(map);

            //Add each ping to the corresponding IP addresses Ping Array
            foreach (string s in pingsList.Keys)
            {
                if (!flagPingerWaiting)     //If the IP Address has been changed, this will break the loop to allow the change
                {
                    Console.WriteLine(s + " - " + pingsList[s].ToString());
                    int pingnum = map[s];
                    int ping = 0;
                    try
                    {
                        ping = pingsList[s];
                    }
                    catch
                    {
                        Console.WriteLine("Unable to transfer");
                    }
                    pings[pingnum].Add(ping);
                }

            }


            
            Console.WriteLine("BackgroundWorker2 Completed");
        }



#endregion


        #region Secondary Methods


        /// <summary>
        /// Get the Mode of a list of integers
        /// </summary>
        /// <param name="numbers">List of integars to get mode from</param>
        /// <returns></returns>
        public static int getMode(List<int> numbers)
        {
            Dictionary<int, int> counts = new Dictionary<int, int>();
            foreach (int a in numbers)
            {
                if (counts.ContainsKey(a))
                    counts[a] = counts[a] + 1;
                else
                    counts[a] = 1;
            }

            int result = int.MinValue;
            int max = int.MinValue;
            foreach (int key in counts.Keys)
            {
                if (counts[key] > max)
                {
                    max = counts[key];
                    result = key;
                }
            }
            return result;
        }



        /// <summary>
        /// Opens applications in Windows Interface
        /// </summary>
        /// <param name="app">Full path to the application</param>
        /// <param name="arguments">Arguments to be sent to application</param>
        /// <returns>standard output</returns>
        public static string openapp(string app, string arguments)
        {
            string op = "";



            ProcessStartInfo procStartInfo = new ProcessStartInfo(app, arguments);
            procStartInfo.RedirectStandardOutput = true;
            procStartInfo.UseShellExecute = false;
            procStartInfo.CreateNoWindow = true;
            var proc = new Process();
            proc.StartInfo = procStartInfo;
            proc.Start();
            string result = proc.StandardOutput.ReadToEnd();
            //proc.WaitForExit();
            Console.WriteLine(result);
            op = result;



            return result;

        }



        /// <summary>
        /// Ping multiple IP Addresses based on Dictionary<string, int> where the Key is the IP address
        /// </summary>
        /// <param name="map">Dictionary<string, int> where Keys are IP Addresses</param>
        /// <returns></returns>
        public static Dictionary<string, int> runPings(Dictionary<string, int> map)
        {
            Dictionary<string, int> pingList = new Dictionary<string, int>();
            foreach (string s in map.Keys)
            {
                int png = 0;
                try
                {
                    PingReply reply = sendPing(s);
                    png = Convert.ToInt32(reply.RoundtripTime);
                }
                catch
                {
                    png = 0;
                }
                pingList.Add(s, png);
                Console.WriteLine(s + " = " + png.ToString());

            }

            return pingList;

        }


        /// <summary>
        /// Pings specified IP Address.
        /// </summary>
        /// <param name="remoteAddress">IP Address to Ping</param>
        /// <returns></returns>
        public static PingReply sendPing(string remoteAddress)
        {
            Ping pingSender = new Ping();
            IPAddress address = IPAddress.Parse(remoteAddress);
            PingReply reply = pingSender.Send(address);
            return reply;
        }

        #endregion


        #region Event Handlers...

        /// <summary>
        /// Starts and stops test
        /// </summary>
        private void button1_Click(object sender, EventArgs e)
        {
            //Stop test if it is already running
            if (started)
            {
                started = false;
                label1.Text = "Pings stopped";
                button1.Text = "Start Test";
            }
            //Start test if it isn't
            else
            {
                started = true;
                button1.Enabled = false;
                //Start Test
                if (!started2)
                {
                    label1.Text = "Getting IP addresses. This may take a minute.";
                    panel1.Visible = true;
                    map = new Dictionary<string, int>();
                    IPList = new List<string>();
                    pings = new List<int>[99];
                    started2 = true;
                    flagPingerWaiting = false;
                    this.backgroundWorker1.RunWorkerAsync();
                }
                //Restart Test
                else
                {
                    button1.Enabled = true;
                    button1.Text = "Stop Test";
                    label1.Text = "Pinging IPs...";
                    try
                    {
                        this.backgroundWorker2.RunWorkerAsync();
                        //If the background task is already running,
                        //then it will not run in a new instance until it is complete.
                    }
                    catch { }
                }
            }
        }


        /// <summary>
        /// When Tracert is complete, fills information into GUI
        /// </summary>
        void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            int count = 0;
            foreach (string str in IPList)
            {
                map.Add(str, count);
                pings[count] = new List<int>();
                ListViewItem lvi = new ListViewItem(str);
                lvi.SubItems.Add("Pending");
                lvi.SubItems.Add("Pending");
                lvi.SubItems.Add("Pending");
                lvi.SubItems.Add("Pending");
                lvi.SubItems.Add("Pending");
                lvi.SubItems.Add("Pending");
                listView1.Items.Add(lvi);
                count++;
            }
            columnHeader1.AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);
            columnHeader7.AutoResize(ColumnHeaderAutoResizeStyle.HeaderSize);
            panel1.Visible = false;
            button1.Enabled = true;
            button1.Text = "Stop Test";
            label1.Text = "Pinging IPs...";
            if (flagPingerReady)
            {
                try
                {
                    this.backgroundWorker2.RunWorkerAsync();
                }
                catch
                {
                    //If Background process is still working on a previous command, thi will keep the program from erroring out.
                    System.Windows.Forms.Timer tm = new System.Windows.Forms.Timer();
                    tm.Interval = 500;
                    tm.Tick += tm_Tick;
                    tm.Start();
                }
            }
            else
            {
                //If Background process is still working on a previous command, this will keep the program from erroring out.
                System.Windows.Forms.Timer tm = new System.Windows.Forms.Timer();
                tm.Interval = 500;
                tm.Tick += tm_Tick;
                tm.Start();
            }
        }

        void tm_Tick(object sender, EventArgs e)
        {
            try
            {
                if (flagPingerReady)
                {
                    this.backgroundWorker2.RunWorkerAsync();
                    System.Windows.Forms.Timer tm = (System.Windows.Forms.Timer)sender;
                    tm.Stop();
                }
            }
            catch
            {
            }
        }

        
        /// <summary>
        /// When Pings are complete, fills information into GUI, then starts Pings again.
        /// </summary>
        void backgroundWorker2_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (started)        //If started is false, this should break the loop.
            {
                if (!flagPingerWaiting)     //If the IP Address has been changed, this will break the loop to allow the change
                {
                    try
                    {
                        Console.WriteLine(e.Result);
                    }
                    catch { }
                    Console.WriteLine("Background Process Completed.");
                    foreach (string s in map.Keys)
                    {
                        string currPing = "Timed Out";
                        int numLost = 0;
                        string avgPing = "N/A";
                        string maxPing = "N/A";
                        string modalPing = "N/A";
                        string totalPings = "N/A";
                        int pingcount = 0;
                        int num;
                        int png;

                        map.TryGetValue(s, out num);
                        png = pings[num].Last<int>();

                        //currPing
                        if (png > 0)
                        {
                            currPing = png.ToString() + "ms";
                        }

                        List<int> avglist = new List<int>();
                        foreach (int iter in pings[num])
                        {
                            if (iter == 0)
                            {
                                numLost = numLost + 1;
                            }
                            else
                            {
                                avglist.Add(iter);
                                pingcount = pingcount + 1;
                            }
                        }
                        totalPings = pingcount.ToString();
                        if (avglist.Count > 0)
                        {
                            avgPing = Math.Round(avglist.Average(), 1).ToString() + "ms";
                        }
                        try
                        {
                            maxPing = pings[num].Max().ToString() + "ms";
                            if (pings[num].Max() == 0)
                            {
                                maxPing = "Timed Out";
                            }
                        }
                        catch
                        {
                            maxPing = "N/A";
                        }
                        try
                        {
                            modalPing = getMode(pings[num]).ToString() + "ms";
                            if (getMode(pings[num]) <= 0)
                            {
                                modalPing = "Timed Out";
                            }
                        }
                        catch
                        {
                            modalPing = "N/A";
                        }
                        string lostPing = numLost.ToString();



                        listView1.Items[num].SubItems[1].Text = currPing;
                        listView1.Items[num].SubItems[2].Text = avgPing;
                        listView1.Items[num].SubItems[3].Text = maxPing;
                        listView1.Items[num].SubItems[4].Text = modalPing;
                        listView1.Items[num].SubItems[5].Text = lostPing;
                        listView1.Items[num].SubItems[6].Text = totalPings;

                    }
                    foreach (ColumnHeader cH in listView1.Columns)
                    {
                        cH.Width = -2;
                    }




                    Console.WriteLine("Background Worker 2 Event Completed.");

                    this.backgroundWorker2.RunWorkerAsync();
                }

            }
            flagPingerReady = true;
        }
       


        /// <summary>
        /// Sets New IP Address and rebuilds lists
        /// </summary>
        private void button2_Click(object sender, EventArgs e)
        {
            
            pingAddress = textBox1.Text;

            //If "Address" is hostname, then convert to IP.
            try
            {
                IPHostEntry hostEntry;

                hostEntry = Dns.GetHostEntry(textBox1.Text);

                //you might get more than one ip for a hostname since 
                //DNS supports more than one record

                if (hostEntry.AddressList.Length > 0)
                {
                    pingAddress = hostEntry.AddressList[0].ToString();
                }
            }
            catch { }
            panel1.Visible = true;
            if (flagPingerReady)
            {
                flagPingerWaiting = true;
                started = false;
                started2 = false;
                map = new Dictionary<string, int>();
                IPList = new List<string>();
                pings = new List<int>[99];
                listView1.Items.Clear();
                label1.Text = "Pings stopped";
                Console.WriteLine("Restarting Test...");
                button1_Click(this, null);
            }
            else
            {
                System.Windows.Forms.Timer tm2 = new System.Windows.Forms.Timer();
                tm2.Interval = 500;
                tm2.Tick += tm2_Tick;
                tm2.Start();
            }
        }

        void tm2_Tick(object sender, EventArgs e)
        {
            flagPingerWaiting = true;
            Console.WriteLine("Waiting...");
            if (flagPingerReady)
            {
                System.Windows.Forms.Timer tm2 = (System.Windows.Forms.Timer)sender;
                tm2.Stop();
                listView1.Items.Clear();
                started = false;
                started2 = false;
                label1.Text = "Pings stopped";
                Console.WriteLine("Resstarting Test...");
                button1_Click(this, null);
            }

        }

        /// <summary>
        /// Used for Aesthetics
        /// </summary>
        void Form1_Resize(object sender, EventArgs e)
        {
            columnHeader7.AutoResize(ColumnHeaderAutoResizeStyle.HeaderSize);
        }

        /// <summary>
        /// Exits Application
        /// </summary>
        void pictureBox2_Click(object sender, EventArgs e)
        {
            Environment.Exit(0);
            Application.Exit();
        }

        void Form1_Load(object sender, EventArgs e)
        {
            this.Icon = Properties.Resources.MiniLogoP;
        }

        /// <summary>
        /// Minimize Window
        /// </summary>
        void pictureBox3_Click(object sender, EventArgs e)
        {
            ShowWindowAsync(this.Handle, 2);
        }


        #endregion


        #region Form Movement
        /*
Constants in Windows API
0x84 = WM_NCHITTEST - Mouse Capture Test
0x1 = HTCLIENT - Application Client Area
0x2 = HTCAPTION - Application Title Bar

This function intercepts all the commands sent to the application. 
It checks to see of the message is a mouse click in the application. 
It passes the action to the base action by default. It reassigns 
the action to the title bar if it occured in the client area
to allow the drag and move behavior.
*/

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case 0x84:
                    base.WndProc(ref m);
                    if ((int)m.Result == 0x1)
                        m.Result = (IntPtr)0x2;
                    return;
            }

            base.WndProc(ref m);
        }

        #endregion



    }
}
