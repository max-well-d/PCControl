using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Drawing;
using Microsoft.Win32;

namespace LANHelper
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            WindowState = FormWindowState.Minimized;
        }

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        public static extern bool SendMessage(IntPtr hwnd, int wMsg, int wParam, int lParam);
        [DllImport("user32")]
        public static extern bool ExitWindowsEx(uint uFlags, uint dwReason);
        [DllImport("user32")]
        public static extern void LockWorkStation();
        [DllImport("PowrProf.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        public static extern bool SetSuspendState(bool hiberate, bool forceCritical, bool disableWakeEvent);

        private string RunningPort;

        public HttpListener listener;

        private readonly RegistryKey registryKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", true);
        private static readonly string ToolName = "LANHelper.exe";

        private readonly Dictionary<string, string>  extensions = new Dictionary<string, string>()
        { 
            //{ "extension", "content type" }
            { "htm", "text/html" },
            { "html", "text/html" },
            { "xml", "text/xml" },
            { "txt", "text/plain" },
            { "css", "text/css" },
            {"js","text/javascript" },

            { "png", "image/png" },
            { "gif", "image/gif" },
            { "jpg", "image/jpg" },
            { "jpeg", "image/jpeg" },
            { "zip", "application/zip"},
            {"ico", "image/x-icon"}
        };
        public bool IsInt(string str)
        {
            try
            {
                int a = Convert.ToInt32(str);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void StartHttpListen(string port)
        {
            listener = new HttpListener();
            listener.Prefixes.Add("http://*:" + port + "/");
            listener.Start();
            listener.BeginGetContext(ListenerHandle, listener);
        }

        public String RunCommand(string Command)
        {
            Process p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.Verb = "runas";
            p.StartInfo.CreateNoWindow = false;
            p.StartInfo.FileName = "cmd.exe";
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.RedirectStandardInput = true;
            p.StartInfo.RedirectStandardOutput = true;

            p.Start();
            p.StandardInput.WriteLine(Command);
            p.StandardInput.AutoFlush = true;
            p.StandardInput.WriteLine("exit");
            string outstr = p.StandardOutput.ReadToEnd();
            p.Close();
            return outstr;

        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            if (textBox1.Text == "") return;

            if (IsInt(textBox1.Text))
            {
                if (Convert.ToInt32(textBox1.Text) >= 65536)
                {
                    textBox1.Text = "65535";
                }
                else if (Convert.ToInt32(textBox1.Text) < 1)
                {
                    textBox1.Text = "0";
                }
            }
            else
            {
                textBox1.Text = "11451";
            }

        }
        private void LoadPage(HttpListenerContext context, string page, string type)
        {
            try
            {
                string ret = "";
                byte[] img;
                HttpListenerResponse response = context.Response;
                response.StatusCode = 200;
                response.ContentType = type;
                response.ContentEncoding = Encoding.UTF8;
                if (type.Split('/')[0] != "text")
                {
                    img = File.ReadAllBytes(page);
                    using (BinaryWriter writer = new BinaryWriter(response.OutputStream))
                    {
                        writer.Write(img);
                        writer.Close();
                    }
                }
                else
                {
                    ret = File.ReadAllText(page);
                    using (StreamWriter writer = new StreamWriter(response.OutputStream, Encoding.UTF8))
                    {
                        writer.Write(ret);
                        writer.Close();
                    }
                }
                response.Close();
                GC.Collect();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private void ListenerHandle(IAsyncResult result)
        {

            try
            {
                if (listener.IsListening)
                {
                    listener.BeginGetContext(ListenerHandle, result);

                    HttpListenerContext context = listener.EndGetContext(result);
                    HttpListenerRequest request = context.Request;
                    string content = "";

                    string filename = context.Request.Url.AbsolutePath.Trim('/');
                    if (filename == "")
                    {
                        filename = "index.html";
                    }
                    string[] type_list = filename.Split('.');
                    string type = "";
                    if (type_list.Length > 1)
                    {
                        type = extensions[type_list[type_list.Length - 1]];
                    }

                    string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filename);

                    

                    switch (request.HttpMethod)
                    {
                        case "POST":
                            {
                                Stream stream = context.Request.InputStream;
                                StreamReader reader = new StreamReader(stream, Encoding.UTF8);
                                content = reader.ReadToEnd();

                                switch (content)
                                {
                                    case "request=shutdown":

                                        Process.Start("shutdown", "/s /t 0");
                                        break;
                                    case "request=reboot":
                                        Process.Start("shutdown", "/r /t 0");
                                        break;
                                    case "request=exit":
                                        ExitWindowsEx(0, 0);
                                        break;
                                    case "request=lock":
                                        LockWorkStation();
                                        break;
                                    case "request=screenclose":
                                        Invoke(new MethodInvoker(delegate { SendMessage(Handle, 0x112, 0xF170, 2);}));
                                        break;
                                    case "request=sleep":
                                        SetSuspendState(false, true, true);
                                        break;
                                    case "request=screenshot":
                                        while (!Screenshort()) ;
                                        LoadPage(context, path, type);
                                        break;
                                }
                                context.Response.Close();
                            }
                            break;
                        case "GET":
                            {
                                try
                                {
                                    LoadPage(context, path, type);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine(ex.Message);
                                }

                            }
                            break;
                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private void Changeport()
        {
            string port = textBox1.Text;
            if (IsInt(port))
            {
                if (port == RunningPort && listener != null) return;
                if (listener != null)
                {
                    RunCommand("netsh http delete urlacl url = http://*:" + RunningPort + "/" + Environment.NewLine +
                    "netsh advfirewall firewall delete rule name=\"LANHelper\"");
                }
                RunCommand("netsh http delete urlacl url = http://*:" + port + "/" + Environment.NewLine +
                    "netsh http add urlacl url=http://*:" + port + "/ user=Everyone" + Environment.NewLine +
                    "netsh advfirewall firewall add rule name=LANHelper dir=in action=allow protocol=TCP localport=" + port + Environment.NewLine +
                    "netsh advfirewall firewall add rule name=LANHelper dir=out action=allow protocol=TCP localport=" + port);
                StartHttpListen(port);
                RunningPort = port;
                if (开机启动ToolStripMenuItem.Checked)
                {
                    registryKey.SetValue(ToolName, $"{Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ToolName)} /{RunningPort}");
                }
                List<string> list = GetLocalIpAddress(port);
                textBox2.Text = string.Join(Environment.NewLine, list.ToArray());

            }
            else
            {
                MessageBox.Show("请输入端口");
            }
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            Changeport();
            Visible = false;
        }
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Visible = false;
            e.Cancel = true;
        }

        public static List<string> GetLocalIpAddress(string port)
        {
            string hostName = Dns.GetHostName();
            IPAddress[] addresses = Dns.GetHostAddresses(hostName);

            List<string> IPList = new List<string>();
            for (int i = 0; i < addresses.Length; i++)
            {
                if (addresses[i].AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    IPList.Add("http://" + addresses[i].ToString() + ":" + port);
            }
            return IPList;
        }
        private bool Screenshort()
        {
            Screen scr = Screen.PrimaryScreen;
            Rectangle rc = scr.Bounds;
            int iWidth = rc.Width;
            int iHeight = rc.Height;
            //创建一个和屏幕一样大的Bitmap            
            Image screenshot = new Bitmap(iWidth, iHeight);
            //从一个继承自Image类的对象中创建Graphics对象            
            Graphics g = Graphics.FromImage(screenshot);
            //抓屏并拷贝到myimage里            
            g.CopyFromScreen(new Point(0, 0), new Point(0, 0), new Size(iWidth, iHeight));
            screenshot.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "screenshot.jpg"));
            return true;
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            if (registryKey.GetValue(ToolName) != null)
            {
                开机启动ToolStripMenuItem.Checked = true;
                string value = registryKey.GetValue(ToolName).ToString();
                value = value.Split('/')[value.Split('/').Length - 1];
                textBox1.Text = value;

            }
            this.notifyIcon1.ContextMenuStrip = contextMenuStrip1;
            RunningPort = textBox1.Text;

            Changeport();

            Visible = false;

        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (Visible == false)
            {
                Visible = true;
                WindowState = FormWindowState.Normal;
            }

            else
                Visible = false;
        }

        private void 退出ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RunCommand("netsh http delete urlacl url = http://*:" + RunningPort + "/" + Environment.NewLine +
                    "netsh advfirewall firewall delete rule name=\"LANHelper\"");
            notifyIcon1.Visible = false;
            Environment.Exit(0);
        }

        private void 开机启动ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (开机启动ToolStripMenuItem.Checked)
            {
                registryKey.DeleteValue(ToolName);
                开机启动ToolStripMenuItem.Checked = false;
            }
            else
            {
                registryKey.SetValue(ToolName, $"{Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ToolName)} /{RunningPort}");
                开机启动ToolStripMenuItem.Checked = true;
            }
        }
    }

}
