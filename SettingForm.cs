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

namespace PCControl
{
    public partial class SettingForm : Form
    {
        public SettingForm()
        {
            InitializeComponent();
            WindowState = FormWindowState.Minimized;
        }
        [DllImport("user32.dll")]
        static extern void mouse_event(Int32 dwFlags, Int32 dx, Int32 dy, Int32 dwData, UIntPtr dwExtraInfo);
        private const int MOUSEEVENTF_MOVE = 0x0001;

        [DllImport("user32.dll")]
        public static extern bool SendMessage(IntPtr hwnd, int wMsg, int wParam, int lParam);
        private const int vMsg = 0x112;
        private const int wParam = 0xF170;
        private const int OFF = 2;
        [DllImport("user32")]
        public static extern bool ExitWindowsEx(uint uFlags, uint dwReason);
        [DllImport("user32")]
        public static extern void LockWorkStation();
        [DllImport("PowrProf.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        public static extern bool SetSuspendState(bool hiberate, bool forceCritical, bool disableWakeEvent);

        public static readonly string ServerPath = AppDomain.CurrentDomain.BaseDirectory;

        private string RunningPort;

        public HttpListener listener;
        private readonly RegistryKey registryKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", true);
        private static readonly string ToolName = "PCControl.exe";

        private readonly Dictionary<string, string>  extensions = new Dictionary<string, string>()
        { 
            //{ "extension", "content type" }
            //text
            { "htm", "text/html" },
            { "html", "text/html" },
            { "xml", "text/xml" },
            { "txt", "text/plain" },
            { "css", "text/css" },
            {"js","text/javascript" },
            //media
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
            try
            {
                listener.Start();
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.ToString(), "ERROR",MessageBoxButtons.OK,MessageBoxIcon.Error);
                Environment.Exit(0);
            }
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

                if (!File.Exists(page))
                {
                    response.StatusCode = 404;
                }
                else
                {
                    response.StatusCode = 200;
                    response.ContentType = type;
                    response.ContentEncoding = Encoding.UTF8;
                    if (type.Split('/')[0] != "text")
                    {
                        //多媒体用字节转二进制传输
                        img = File.ReadAllBytes(page);
                        using (BinaryWriter writer = new BinaryWriter(response.OutputStream))
                        {
                            writer.Write(img);
                            writer.Close();
                        }
                    }
                    else
                    {
                        //文本直接传输
                        ret = File.ReadAllText(page);
                        using (StreamWriter writer = new StreamWriter(response.OutputStream, Encoding.UTF8))
                        {
                            writer.Write(ret);
                            writer.Close();
                        }
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
                    string filename = Path.GetFileName(context.Request.Url.AbsolutePath);
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

                    string path = Path.Combine(ServerPath, filename);
                    switch (request.HttpMethod)
                    {
                        case "POST":
                            {
                                Stream stream = context.Request.InputStream;
                                StreamReader reader = new StreamReader(stream, Encoding.UTF8);
                                content = reader.ReadToEnd();

                                using (StreamWriter streamWriter = new StreamWriter(Path.Combine(ServerPath, "log.txt"), true, Encoding.UTF8))
                                {
                                    string IP = context.Request.RemoteEndPoint.Address.ToString();
                                    string time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss:fff");
                                    streamWriter.WriteLine($"{time}: {content} by {IP}");
                                    streamWriter.Close();
                                }
                                switch (content)
                                {
                                    case "request=wakeup":
                                        mouse_event(MOUSEEVENTF_MOVE, 0, 1, 0, UIntPtr.Zero);
                                        mouse_event(MOUSEEVENTF_MOVE, 0, -1, 0, UIntPtr.Zero);
                                        break;
                                    case "request=shutdown":
                                        Process.Start("shutdown", "/s /t 0");
                                        EXIT();
                                        break;
                                    case "request=reboot":
                                        Process.Start("shutdown", "/r /t 0");
                                        EXIT();
                                        break;
                                    case "request=exit":
                                        ExitWindowsEx(0, 0);
                                        EXIT();
                                        break;
                                    case "request=lock":
                                        LockWorkStation();
                                        break;
                                    case "request=screenclose":
                                        Invoke(new MethodInvoker(delegate { SendMessage(Handle, vMsg, wParam, OFF);}));
                                        break;
                                    case "request=sleep":
                                        SetSuspendState(false, true, true);
                                        break;
                                    
                                }
                                context.Response.Close();
                            }
                            break;
                        case "GET":
                            {
                                switch (filename)
                                {
                                    case "screenshot.jpg":
                                        Screenshort();
                                        break;
                                }
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
                    listener.Stop();
                    RunCommand("netsh http delete urlacl url = http://*:" + RunningPort + "/" + Environment.NewLine +
                    "netsh advfirewall firewall delete rule name=\"PCControl\"");
                }
                RunCommand("netsh http delete urlacl url = http://*:" + port + "/" + Environment.NewLine +
                    "netsh http add urlacl url=http://*:" + port + "/ user=Everyone" + Environment.NewLine +
                    "netsh advfirewall firewall add rule name=PCControl dir=in action=allow protocol=TCP localport=" + port + Environment.NewLine +
                    "netsh advfirewall firewall add rule name=PCControl dir=out action=allow protocol=TCP localport=" + port);
                StartHttpListen(port);
                RunningPort = port;
                if (BWSToolStripMenuItem.Checked)
                {
                    registryKey.SetValue(ToolName, $"{Path.Combine(ServerPath, ToolName)} /{RunningPort}");
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
        private void Screenshort()
        {
            string path = Path.Combine(ServerPath, "screenshot.jpg");
            Rectangle rc = SystemInformation.VirtualScreen;
            int iWidth = rc.Width;
            int iHeight =rc.Height;
            Image screenshot = new Bitmap(rc.Width, rc.Height);         
            Graphics g = Graphics.FromImage(screenshot); 
            g.CopyFromScreen(new Point(0, 0), new Point(0, 0), new Size(iWidth, iHeight));
            screenshot.Save(path);
        }


        private void Form1_Load(object sender, EventArgs e)
        {
            if (registryKey.GetValue(ToolName) != null)
            {
                BWSToolStripMenuItem.Checked = true;
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

        private void EXIT()
        {
            RunCommand("netsh http delete urlacl url = http://*:" + RunningPort + "/" + Environment.NewLine +
        "netsh advfirewall firewall delete rule name=\"PCControl\"");
            notifyIcon1.Visible = false;
            Environment.Exit(0);
        }

        private void ExitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            EXIT();
        }

        private void BWSToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (BWSToolStripMenuItem.Checked)
            {
                registryKey.DeleteValue(ToolName);
                BWSToolStripMenuItem.Checked = false;
            }
            else
            {
                registryKey.SetValue(ToolName, $"{Path.Combine(ServerPath, ToolName)} /{RunningPort}");
                BWSToolStripMenuItem.Checked = true;
            }
        }

        private void SettingToolStripMenuItem_MouseUp(object sender, MouseEventArgs e)
        {
            notifyIcon1_MouseDoubleClick(sender, e);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Visible = false;
        }
    }

}
