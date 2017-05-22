using System.IO;
using System.Net.Sockets;
using System.Text;

namespace FtpServer
{
    /// <summary>
    /// 保存连接设置信息
    /// </summary>
    public class UserSeesion
    {
        private NetworkStream networkStream;
        public readonly StreamReader streamReader;
        public readonly StreamWriter streamWriter;
        // TcpClient对象代表一个客户端对象
        public readonly TcpClient tcpClient;
        public readonly BinaryReader binaryReader;
        public readonly BinaryWriter binaryWriter;
        public UserSeesion(TcpClient client)
        {
            this.tcpClient = client;
            networkStream = client.GetStream();
            streamReader = new StreamReader(networkStream, Encoding.Default);
            streamWriter = new StreamWriter(networkStream, Encoding.Default);
            streamWriter.AutoFlush = true;
            binaryReader = new BinaryReader(networkStream, Encoding.Default);
            binaryWriter = new BinaryWriter(networkStream, Encoding.Default);
        }

        public void Close()
        {
            tcpClient.Client.Shutdown(SocketShutdown.Both);
            tcpClient.Client.Close();
            tcpClient.Close();
        }
    }
}
