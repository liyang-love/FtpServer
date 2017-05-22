using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FtpServer
{
    class Program
    {
        static void Main(string[] args)
        {
            FtpServer Server = new FtpServer();
            Server.Start();
        }
    }

    public partial class FtpServer
    {
        TcpListener myTcpListener = null;
        private Thread listenThread;

        // 保存用户名和密码
        Dictionary<string, string> users;


        string FtpRoot = "";
        string FtpServerIp = "";
        string FtpServerPort = "";
        bool Status = false;
        public FtpServer()
        {
            // 初始化用户名和密码
            users = new Dictionary<string, string>();
            users.Add("admin", "admin");

            // 设置默认的主目录
            FtpRoot = "F:/MyFtpServerRoot/";
            //IPAddress[] ips = Dns.GetHostAddresses("");
            //FtpServerIp = ips[1].ToString();
            FtpServerPort = "21";
        }

        // 启动服务器
        public void Start()
        {
            if (myTcpListener == null)
            {
                listenThread = new Thread(ListenClientConnect);
                listenThread.IsBackground = true;
                listenThread.Start();
                Status = true;
                Console.ReadKey();
                //lstboxStatus.Enabled = true;
                //lstboxStatus.Items.Clear();
                //lstboxStatus.Items.Add("已经启动Ftp服务...");
                //btnFtpServerStartStop.Text = "停止";
            }
            else
            {
                myTcpListener.Stop();
                myTcpListener = null;
                listenThread.Abort();
                Status = false;
                //lstboxStatus.Items.Add("Ftp服务已停止！");
                //lstboxStatus.TopIndex = lstboxStatus.Items.Count - 1;

                //btnFtpServerStartStop.Text = "启动";
            }
        }

        // 监听端口，处理客户端连接
        public void ListenClientConnect()
        {
            myTcpListener = new TcpListener(IPAddress.Any, 21);
            // 开始监听传入的请求
            myTcpListener.Start();
            AddInfo("启动FTP服务成功！");
            AddInfo("Ftp服务器运行中...[点击”停止“按钮停止FTP服务]");
            while (true)
            {
                try
                {
                    // 接收连接请求
                    TcpClient tcpClient = myTcpListener.AcceptTcpClient();
                    AddInfo(string.Format("客户端（{0}）与本机（{1}）建立Ftp连接", tcpClient.Client.RemoteEndPoint, myTcpListener.LocalEndpoint));
                    User user = new User();
                    user.commandSession = new UserSeesion(tcpClient);
                    user.workDir = FtpRoot;
                    Thread t = new Thread(UserProcessing);
                    t.IsBackground = true;
                    t.Start(user);
                }
                catch
                {
                    break;
                }
            }
        }

        // 处理客户端用户请求
        public void UserProcessing(object obj)
        {
            User user = (User)obj;
            string sendString = "220 FTP Server v1.0";
            RepleyCommandToUser(user, sendString);
            while (true)
            {
                string receiveString = null;
                try
                {
                    // 读取客户端发来的请求信息
                    receiveString = user.commandSession.streamReader.ReadLine();
                }
                catch (Exception ex)
                {
                    if (user.commandSession.tcpClient.Connected == false)
                    {
                        AddInfo(string.Format("客户端({0}断开连接！)", user.commandSession.tcpClient.Client.RemoteEndPoint));
                    }
                    else
                    {
                        AddInfo("接收命令失败！" + ex.Message);
                    }

                    break;
                }

                if (receiveString == null)
                {
                    AddInfo("接收字符串为null,结束线程！");
                    break;
                }

                AddInfo(string.Format("来自{0}：[{1}]", user.commandSession.tcpClient.Client.RemoteEndPoint, receiveString));

                // 分解客户端发来的控制信息中的命令和参数
                string command = receiveString;
                string param = string.Empty;
                int index = receiveString.IndexOf(' ');
                if (index != -1)
                {
                    command = receiveString.Substring(0, index).ToUpper();
                    param = receiveString.Substring(command.Length).Trim();
                }

                // 处理不需登录即可响应的命令（这里只处理QUIT）
                if (command == "QUIT")
                {
                    // 关闭TCP连接并释放与其关联的所有资源
                    user.commandSession.Close();
                    return;
                }
                else
                {
                    switch (user.loginOK)
                    {
                        // 等待用户输入用户名：
                        case 0:
                            CommandUser(user, command, param);
                            break;

                        // 等待用户输入密码
                        case 1:
                            CommandPassword(user, command, param);
                            break;

                        // 用户名和密码验证正确后登陆
                        case 2:
                            switch (command)
                            {
                                case "CWD":
                                    CommandCWD(user, param);
                                    break;
                                case "PWD":
                                    CommandPWD(user);
                                    break;
                                case "PASV":
                                    CommandPASV(user);
                                    break;
                                case "PORT":
                                    CommandPORT(user, param);
                                    break;
                                case "LIST":
                                    CommandLIST(user, param);
                                    break;
                                case "NLIST":
                                    CommandLIST(user, param);
                                    break;
                                // 处理下载文件命令
                                case "RETR":
                                    CommandRETR(user, param);
                                    break;
                                // 处理上传文件命令
                                case "STOR":
                                    CommandSTOR(user, param);
                                    break;
                                // 处理删除命令
                                case "DELE":
                                    CommandDELE(user, param);
                                    break;
                                // 使用Type命令在ASCII和二进制模式进行变换
                                case "TYPE":
                                    CommandTYPE(user, param);
                                    break;
                                default:
                                    sendString = "502 command is not implemented.";
                                    RepleyCommandToUser(user, sendString);
                                    break;
                            }

                            break;
                    }
                }
            }
        }

        // 想客户端返回响应码
        public void RepleyCommandToUser(User user, string str)
        {
            try
            {
                user.commandSession.streamWriter.WriteLine(str);
                AddInfo(string.Format("向客户端（{0}）发送[{1}]", user.commandSession.tcpClient.Client.RemoteEndPoint, str));
            }
            catch
            {
                AddInfo(string.Format("向客户端（{0}）发送信息失败", user.commandSession.tcpClient.Client.RemoteEndPoint));
            }
        }

        // 向屏幕输出显示状态信息（这里使用了委托机制）
        public delegate void AddInfoDelegate(string str);

        public void AddInfo(string str)
        {
            // 如果调用AddInfo()方法的线程与创建ListView控件的线程不在一个线程时
            // 此时利用委托在创建ListView的线程上调用
            if (Status == true)
            {
                Console.WriteLine(str);
                //AddInfoDelegate d = new AddInfoDelegate(AddInfo);
                //this.Invoke(d, str);
            }
            else
            {
                //lstboxStatus.Items.Add(str);
                //lstboxStatus.TopIndex = lstboxStatus.Items.Count - 1;
                //lstboxStatus.ClearSelected();
            }
        }

        #region 处理各个命令

        #region 登录过程，即用户身份验证过程
        // 处理USER命令，接收用户名但不进行验证
        public void CommandUser(User user, string command, string param)
        {
            string sendString = string.Empty;
            if (command == "USER")
            {
                sendString = "331 USER command OK, password required.";
                user.userName = param;
                // 设置loginOk=1为了确保后面紧接的要求输入密码
                // 1表示已接收到用户名，等到接收密码
                user.loginOK = 1;
            }
            else
            {
                sendString = "501 USER command syntax error.";
            }

            RepleyCommandToUser(user, sendString);
        }

        // 处理PASS命令，验证用户名和密码
        public void CommandPassword(User user, string command, string param)
        {
            string sendString = string.Empty;
            if (command == "PASS")
            {
                string password = null;
                if (users.TryGetValue(user.userName, out password))
                {
                    if (password == param)
                    {
                        sendString = "230 User logged in success";
                        // 2表示登录成功
                        user.loginOK = 2;
                    }
                    else
                    {
                        sendString = "530 Password incorrect.";
                    }
                }
                else
                {
                    sendString = "530 User name or password incorrect.";
                }
            }
            else
            {
                sendString = "501 PASS command Syntax error.";
            }

            RepleyCommandToUser(user, sendString);
            // 用户当前工作目录
            user.currentDir = user.workDir;
        }

        #endregion

        #region 文件管理命令
        // 处理CWD命令，改变工作目录
        public void CommandCWD(User user, string temp)
        {
            string sendString = string.Empty;
            try
            {
                string dir = user.workDir.TrimEnd('/') + temp;

                // 是否为当前目录的子目录，且不包含父目录名称
                if (Directory.Exists(dir))
                {
                    user.currentDir = dir;
                    sendString = "250 Directory changed to '" + dir + "' successfully";
                }
                else
                {
                    sendString = "550 Directory '" + dir + "' does not exist";
                }
            }
            catch
            {
                sendString = "502 Directory changed unsuccessfully";
            }

            RepleyCommandToUser(user, sendString);
        }

        // 处理PWD命令，显示工作目录
        public void CommandPWD(User user)
        {
            string sendString = string.Empty;
            sendString = "257 '" + user.currentDir + "' is the current directory";
            RepleyCommandToUser(user, sendString);
        }

        // 处理LIST/NLIST命令，想客户端发送当前或指定目录下的所有文件名和子目录名
        public void CommandLIST(User user, string parameter)
        {
            string sendString = string.Empty;
            DateTimeFormatInfo dateTimeFormat = new CultureInfo("en-US", true).DateTimeFormat;

            // 得到目录列表
            string[] dir = Directory.GetDirectories(user.currentDir);
            if (string.IsNullOrEmpty(parameter) == false)
            {
                if (Directory.Exists(user.currentDir + parameter))
                {
                    dir = Directory.GetDirectories(user.currentDir + parameter);
                }
                else
                {
                    string s = user.currentDir.TrimEnd('/');
                    user.currentDir = s.Substring(0, s.LastIndexOf("/") + 1);
                }
            }
            for (int i = 0; i < dir.Length; i++)
            {
                string folderName = Path.GetFileName(dir[i]);
                DirectoryInfo d = new DirectoryInfo(dir[i]);

                // 按下面的格式输出目录列表
                sendString += @"dwr-\t" + Dns.GetHostName() + "\t" + dateTimeFormat.GetAbbreviatedMonthName(d.CreationTime.Month)
                    + d.CreationTime.ToString(" dd yyyy") + "\t" + folderName + Environment.NewLine;
            }

            // 得到文件列表
            string[] files = Directory.GetFiles(user.currentDir);
            if (string.IsNullOrEmpty(parameter) == false)
            {
                if (Directory.Exists(user.currentDir + parameter + "/"))
                {
                    files = Directory.GetFiles(user.currentDir + parameter + "/");
                }
            }
            for (int i = 0; i < files.Length; i++)
            {
                FileInfo f = new FileInfo(files[i]);
                string fileName = Path.GetFileName(files[i]);
                // 按下面的格式输出文件列表
                sendString += "-wr-\t" + Dns.GetHostName() + "\t" + f.Length + " "
                    + dateTimeFormat.GetAbbreviatedMonthName(f.CreationTime.Month)
                    + f.CreationTime.ToString(" dd yyyy") + "\t" + fileName + Environment.NewLine;
            }

            // List命令指示获得FTP服务器上的文件列表字符串信息
            // 所以调用List命令过程，客户端接受的指示一些字符串
            // 所以isBinary是false,代表传输的是ASCII数据

            // 但是为了防止isBinary因为 设置user.isBinary = false而改变
            // 所以事先保存user.IsBinary的引用（此时为true）,方便后面下载文件
            bool isBinary = user.isBinary;
            user.isBinary = false;
            RepleyCommandToUser(user, "150 Opening ASCII data connection");
            InitDataSession(user);
            SendByUserSession(user, sendString);
            RepleyCommandToUser(user, "226 Transfer complete");
            user.isBinary = isBinary;
        }

        // 处理RETR命令，提供下载功能，将用户请求的文件发送给用户
        public void CommandRETR(User user, string filename)
        {
            string sendString = "";

            // 下载的文件全名
            string path = user.currentDir + filename;
            FileStream filestream = new FileStream(path, FileMode.Open, FileAccess.Read);

            // 发送150到用户，表示服务器文件状态良好，将要打开数据连接传输文件
            if (user.isBinary)
            {
                sendString = "150 Opening BINARY mode data connection for download";
            }
            else
            {
                sendString = "150 Opening ASCII mode data connection for download";
            }

            RepleyCommandToUser(user, sendString);
            InitDataSession(user);
            SendFileByUserSession(user, filestream);
            RepleyCommandToUser(user, "226 Transfer complete");
        }

        // 处理STOR命令，提供上传功能，接收客户端上传的文件
        public void CommandSTOR(User user, string filename)
        {
            string sendString = "";
            // 上传的文件全名
            string path = user.currentDir + filename;
            FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write);

            // 发送150到用户，表示服务器状态良好
            if (user.isBinary)
            {
                sendString = "150 Opening BINARY mode data connection for upload";
            }
            else
            {
                sendString = "150 Opeing ASCII mode data connection for upload";
            }

            RepleyCommandToUser(user, sendString);
            InitDataSession(user);
            ReadFileByUserSession(user, fs);
            RepleyCommandToUser(user, "226 Transfer complete");
        }

        // 处理DELE命令，提供删除功能，删除服务器上的文件
        public void CommandDELE(User user, string filename)
        {
            string sendString = "";

            // 删除的文件全名
            string path = user.currentDir + filename;
            AddInfo("正在删除文件" + filename + "...");
            File.Delete(path);
            AddInfo("删除成功");
            sendString = "250 File " + filename + " has been deleted.";
            RepleyCommandToUser(user, sendString);
        }

        #endregion

        #region 模式设置命令

        // 处理PASV命令， 使用被动模式进行传输
        public void CommandPASV(User user)
        {
            string sendString = string.Empty;
            IPAddress localip = Dns.GetHostEntry("").AddressList[1];

            // 被动模式，即服务器接收客户端的连接请求
            // 被动模式下FTP服务器使用随机生成的端口进行传输数据
            // 而主动模式下FTP服务器使用端口20进行数据传输
            Random random = new Random();
            int random1, random2;
            int port;
            while (true)
            {
                // 随机生成一个端口进行数据传输
                random1 = random.Next(5, 200);
                random2 = random.Next(0, 200);
                // 生成的端口号控制>1024的随机端口
                // 下面这个运算算法只是为了得到一个大于1024的端口值
                port = random1 << 8 | random2;
                try
                {
                    user.dataListener = new TcpListener(localip, port);
                    AddInfo("TCP 数据连接已打开（被动模式）--" + localip.ToString() + "：" + port);
                }
                catch
                {
                    continue;
                }

                user.isPassive = true;
                string temp = localip.ToString().Replace('.', ',');

                // 必须把端口号IP地址告诉客户端，客户端接收到响应命令后，
                // 再通过新的端口连接服务器的端口P，然后进行文件数据传输
                sendString = "227 Entering Passive Mode(" + temp + "," + random1 + "," + random2 + ")";
                RepleyCommandToUser(user, sendString);
                user.dataListener.Start();
                break;
            }
        }

        // 处理PORT命令，使用主动模式进行传输
        public void CommandPORT(User user, string portstring)
        {
            // 主动模式时，客户端必须告知服务器接收数据的端口号，PORT 命令格式为：PORT address
            // address参数的格式为i1、i2、i3、i4、p1、p2,其中i1、i2、i3、i4表示IP地址
            // 下面通过.字符串来组合这四个参数得到IP地址
            // p1、p2表示端口号，下面通过int.Parse(temp[4]) << 8) | int.Parse(temp[5]
            // 这个算法来获得一个大于1024的端口来发送给服务器
            string sendString = string.Empty;
            string[] temp = portstring.Split(',');
            string ipString = "" + temp[0] + "." + temp[1] + "." + temp[2] + "." + temp[3];

            // 客户端发出PORT命令把客户端的IP地址和随机的端口告诉服务器
            int portNum = (int.Parse(temp[4]) << 8) | int.Parse(temp[5]);
            user.remoteEndPoint = new IPEndPoint(IPAddress.Parse(ipString), portNum);
            sendString = "200 PORT command successful.";

            // 服务器以接受到的客户端IP地址和端口为目标发起主动连接请求
            // 服务器根据客户端发送过来的IP地址和端口主动发起与客户端建立连接
            RepleyCommandToUser(user, sendString);
        }

        // 处理TYPE命令,设置数据传输方式
        public void CommandTYPE(User user, string param)
        {
            string sendstring = "";
            if (param == "I")
            {
                // 二进制
                user.isBinary = true;
                sendstring = "220 Type set to I(Binary)";
            }
            else
            {
                // ASCII方式
                user.isBinary = false;
                sendstring = "330 Type set to A(ASCII)";
            }

            RepleyCommandToUser(user, sendstring);
        }

        #endregion

        #endregion

        // 初始化数据连接
        public void InitDataSession(User user)
        {
            TcpClient client = null;
            if (user.isPassive)
            {
                AddInfo("采用被动模式返回LIST目录和文件列表");
                client = user.dataListener.AcceptTcpClient();
            }
            else
            {
                AddInfo("采用主动模式向用户发送LIST目录和文件列表");
                client = new TcpClient();
                client.Connect(user.remoteEndPoint);
            }

            user.dataSession = new UserSeesion(client);
        }

        // 使用数据连接发送字符串
        public void SendByUserSession(User user, string sendString)
        {
            AddInfo("向用户发送(字符串信息)：[" + sendString + "]");
            try
            {
                user.dataSession.streamWriter.WriteLine(sendString);
                AddInfo("发送完毕");
            }
            finally
            {
                user.dataSession.Close();
            }
        }

        // 使用数据连接发送文件流（客户端发送下载文件命令）
        public void SendFileByUserSession(User user, FileStream fs)
        {
            AddInfo("向用户发送(文件流)：[...");
            try
            {
                if (user.isBinary)
                {
                    byte[] bytes = new byte[1024];
                    BinaryReader binaryReader = new BinaryReader(fs);
                    int count = binaryReader.Read(bytes, 0, bytes.Length);
                    while (count > 0)
                    {
                        user.dataSession.binaryWriter.Write(bytes, 0, count);
                        user.dataSession.binaryWriter.Flush();
                        count = binaryReader.Read(bytes, 0, bytes.Length);
                    }
                }
                else
                {
                    StreamReader streamReader = new StreamReader(fs);
                    while (streamReader.Peek() > -1)
                    {
                        user.dataSession.streamWriter.WriteLine(streamReader.ReadLine());
                    }
                }

                AddInfo("...]发送完毕！");
            }
            finally
            {
                user.dataSession.Close();
                fs.Close();
            }
        }

        // 使用数据连接接收文件流(客户端发送上传文件功能)
        public void ReadFileByUserSession(User user, FileStream fs)
        {
            AddInfo("接收用户上传数据（文件流）：[...");
            try
            {
                if (user.isBinary)
                {
                    byte[] bytes = new byte[1024];
                    BinaryWriter binaryWriter = new BinaryWriter(fs);
                    int count = user.dataSession.binaryReader.Read(bytes, 0, bytes.Length);
                    while (count > 0)
                    {
                        binaryWriter.Write(bytes, 0, count);
                        binaryWriter.Flush();
                        count = user.dataSession.binaryReader.Read(bytes, 0, bytes.Length);
                    }
                }
                else
                {
                    StreamWriter streamWriter = new StreamWriter(fs);
                    while (user.dataSession.streamReader.Peek() > -1)
                    {
                        streamWriter.Write(user.dataSession.streamReader.ReadLine());
                        streamWriter.Flush();
                    }
                }

                AddInfo("...]接收完毕");
            }
            finally
            {
                user.dataSession.Close();
                fs.Close();
            }
        }
    }
}
