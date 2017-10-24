using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DotNetSimpleProxyServer
{
    internal class ProxyServer
    {
        TcpListener tcpListener;
        public ProxyServer(string ip, int port)
        {
            tcpListener = new TcpListener(IPAddress.Parse(ip), port);
        }

        public void Run()
        {
            tcpListener.Start(1000);
            while (true)
            {
                Socket socket = tcpListener.AcceptSocket();
                Thread thread = new Thread(ThreadHandleClient);
                thread.Start(socket);
            }
        }
        const int BUFFER_SIZE = 1024 * 10;
        private byte[] ReadStream(Stream stream)
        {
            var datas = new List<byte>();
            var buffer = new byte[BUFFER_SIZE];
            int rec;
            do
            {
                rec = stream.Read(buffer, 0, buffer.Length);
                datas.AddRange(buffer.Take(rec));
            } while (rec == buffer.Length);
            return datas.ToArray();
        }

        public void ThreadHandleClient(object o)
        {
            try
            {
                Socket client = (Socket)o;
                var clientSocketStream = new NetworkStream(client);
                //RECEIVE CLIENT DATA 
                var commandLineData = ReadStream(clientSocketStream);
                var commandLine = Encoding.ASCII.GetString(commandLineData);
                Console.WriteLine(commandLine);
                var commandLines = commandLine.Split(new string[] { "\r\n" }, StringSplitOptions.None);
                //PARSE DESTINATION AND SEND REQUEST
                string methodLine = commandLines[0];
                var httpVersion = methodLine.Split(' ')[2];
                string hostLine = commandLines.First(line => line.StartsWith("Host"));
                var hostAndPort = hostLine.Split(' ')[1];
                var port = 80;
                var host = hostAndPort;
                if (hostAndPort.Contains(":"))
                {
                    host = hostAndPort.Split(':')[0];
                    port = int.Parse(hostAndPort.Split(':')[1]);
                }
                var url = methodLine.Split(' ')[1];
                bool isHttps = commandLine.StartsWith("CONNECT");

                IPHostEntry rh = Dns.GetHostEntry(host);
                Socket webServer = new Socket(rh.AddressList[0].AddressFamily, SocketType.Stream, ProtocolType.IP);
                webServer.Connect(new IPEndPoint(rh.AddressList[0], port));
                if (isHttps)
                {
                    var webServerSocketStream = new NetworkStream(webServer);
                    string rq = httpVersion + " 200 Connection established\r\nProxy-Agent: Proxy\r\n\r\n";
                    var sendData = Encoding.ASCII.GetBytes(rq);
                    clientSocketStream.Write(sendData, 0, sendData.Length);

                    Action<Task<int>> clientAsyncCallback = null;
                    Action<Task<int>> webServerAsyncCallback = null;

                    var clientArraySegment = new ArraySegment<byte>(new byte[BUFFER_SIZE]);
                    var webServerSegment = new ArraySegment<byte>(new byte[BUFFER_SIZE]);

                    clientAsyncCallback = new Action<Task<int>>((i) =>
                    {
                        if (i.Result > 0)
                        {
                            try
                            {
                                var data = clientArraySegment.Take(i.Result).ToArray();
                                webServer.Send(data);
                                Console.WriteLine(data.Length);
                                client.ReceiveAsync(clientArraySegment, SocketFlags.None).ContinueWith(clientAsyncCallback);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e);
                            }
                        }
                    });
                    webServerAsyncCallback = new Action<Task<int>>((i) =>
                   {
                       if (i.Result > 0)
                       {
                           try
                           {
                               var data = webServerSegment.Take(i.Result).ToArray();
                               client.Send(data);
                               Console.WriteLine(data.Length);
                               webServer.ReceiveAsync(webServerSegment, SocketFlags.None).ContinueWith(webServerAsyncCallback);
                           }
                           catch (Exception e)
                           {
                               Console.WriteLine(e);
                           }
                       }
                   });

                    client.ReceiveAsync(clientArraySegment, SocketFlags.None).ContinueWith(clientAsyncCallback);
                    webServer.ReceiveAsync(webServerSegment, SocketFlags.None).ContinueWith(webServerAsyncCallback);

                }
                else
                {
                    var webServerSegment = new ArraySegment<byte>(new byte[BUFFER_SIZE]);
                    Action<Task<int>> webServerAsyncCallback = null;
                    var datas = new List<byte>();
                    webServerAsyncCallback = new Action<Task<int>>((i) =>
                    {
                        if (i.Result > 0)
                        {
                            try
                            {
                                var data = webServerSegment.Take(i.Result).ToArray();
                                datas.AddRange(data);
                                client.Send(data);
                                Console.WriteLine(data.Length);
                                webServer.ReceiveAsync(webServerSegment, SocketFlags.None).ContinueWith(webServerAsyncCallback);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e);
                            }
                        }
                    });
                    webServer.Send(commandLineData, commandLineData.Length, SocketFlags.None);
                    webServer.ReceiveAsync(webServerSegment, SocketFlags.None).ContinueWith(webServerAsyncCallback);

                    //var buffer = new Byte[BUFFER_SIZE];
                    //var transferred = 0;
                    //do
                    //{
                    //    rec = webServer.Receive(buffer, buffer.Length, SocketFlags.None);
                    //    sent = client.Send(buffer, rec, SocketFlags.None);
                    //    transferred += rec;
                    //    //Console.WriteLine(Encoding.ASCII.GetString(buffer.Take(rec).ToArray()));
                    //} while (rec == buffer.Length);
                    //Console.WriteLine("http共发送:" + transferred);
                    //client.Close();
                    //webServer.Close();
                }

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Print("Error occured: " + ex.Message);
                System.Diagnostics.Debug.Print("Error occured: " + ex.StackTrace);

            }
        }

    }
}
