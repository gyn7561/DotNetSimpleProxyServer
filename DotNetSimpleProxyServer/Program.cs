using System;

namespace DotNetSimpleProxyServer
{
    class Program
    {
        static void Main(string[] args)
        {
            ProxyServer proxyServer = new ProxyServer("127.0.0.1",8569);
            proxyServer.Run();
            Console.ReadLine();
        }
    }
}
