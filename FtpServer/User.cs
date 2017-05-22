
// 添加额外的引用
using System.Net;
using System.Net.Sockets;

namespace FtpServer
{
    /// <summary>
    /// 保存与客户端通信需要的信息
    /// </summary>
    public class User
    {
        public UserSeesion commandSession { get; set; }
        public UserSeesion dataSession { get; set; }
        public TcpListener dataListener { get; set; }

        // 主动模式下使用的客户端监听的IPEndPoint
        public IPEndPoint remoteEndPoint { get; set; }

        // 用户名
        public string userName { get; set; }
       
        // 工作目录
        public string workDir { get; set; }

        // 当前工作目录
        public string currentDir { get; set; }

        // 初始状态为等待输入用户名
        public int loginOK { get; set; }

        // 是否使用二进制数据传输方式
        public bool isBinary { get; set; }

        // 数据连接使用的是否被动连接
        public bool isPassive { get; set; }
    }
}
