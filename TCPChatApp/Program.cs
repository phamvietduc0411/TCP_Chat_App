using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class Program
{
    // Danh sách các client đã kết nối
    private static List<TcpClient> clients = new List<TcpClient>();
    static int _port = 8888;

    static void Main(string[] args)
    {
        Console.WriteLine("Choose: 1 - Server, 2 - Client");
        string choice = Console.ReadLine();

        if (choice == "1")
        {
            RunServer();
        }
        else if (choice == "2")
        {
            RunClient();
        }
        else
        {
            Console.WriteLine("Invalid choice");
        }
    }

    // Chạy server
    private static void RunServer()
    {
        TcpListener listener = new TcpListener(IPAddress.Any, _port);
        listener.Start();
        Console.WriteLine($"Server start in port{ _port}.");

        ThreadPool.QueueUserWorkItem(_ =>
        {
            while (true)
            {
                TcpClient client = listener.AcceptTcpClient();
                lock (clients)
                {
                    clients.Add(client);
                }
                Console.WriteLine("One client connect:");
                ThreadPool.QueueUserWorkItem(state => HandleClient(client));
            }
        });

        Console.WriteLine("Enter to exits");
        Console.ReadLine();
        listener.Stop();
    }

    // Xử lý từng client
    private static void HandleClient(TcpClient client)
    {
        string clientName = string.Empty;

        try
        {
            NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[1024];

            // Đọc tên client khi kết nối
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            clientName = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
            Console.WriteLine($"Client-{clientName}");

            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                Console.WriteLine($"{clientName}: {message}");

                if (message.Equals("SEND_FILE", StringComparison.OrdinalIgnoreCase))
                {
                    ReceiveFile(stream, clientName);
                }
                else
                {
                    BroadcastMessage(client, $"{clientName}: {message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error from Client {clientName}: {ex.Message}");
        }
        finally
        {
            lock (clients)
            {
                clients.Remove(client);
            }
            client.Close();
        }
    }

    // Nhận file từ client
    private static void ReceiveFile(NetworkStream stream, string clientName)
    {
        try
        {
            byte[] fileBuffer = new byte[1024 * 1024];
            int bytesRead = stream.Read(fileBuffer, 0, fileBuffer.Length);

            if (bytesRead > 0)
            {
                string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                string fileName = $"{clientName}_file_{timestamp}.txt";

                File.WriteAllBytes(fileName, fileBuffer[..bytesRead]);
                Console.WriteLine($"File was save : {fileName}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error when receive file: {ex.Message}");
        }
    }

    // Gửi tin nhắn đến tất cả các client khác
    private static void BroadcastMessage(TcpClient sender, string message)
    {
        lock (clients)
        {
            foreach (var client in clients)
            {
                if (client != sender)
                {
                    try
                    {
                        NetworkStream clientStream = client.GetStream();
                        byte[] data = Encoding.UTF8.GetBytes(message);
                        clientStream.Write(data, 0, data.Length);
                    }
                    catch
                    {
                        // Bỏ qua client ngắt kết nối
                    }
                }
            }
        }
    }

    // Chạy client
    private static void RunClient()
    {
        try
        {
            TcpClient client = new TcpClient();
            client.Connect("127.0.0.1", _port);
            Console.WriteLine("Connected to the server successfully.");

            NetworkStream stream = client.GetStream();

            // Gửi tên client
            Console.Write("Enter your name to join to server: ");
            string clientName = Console.ReadLine();
            byte[] nameData = Encoding.UTF8.GetBytes(clientName);
            stream.Write(nameData, 0, nameData.Length);

            // Luồng để nhận tin nhắn từ server
            ThreadPool.QueueUserWorkItem(_ =>
            {
                while (true)
                {
                    try
                    {
                        byte[] buffer = new byte[1024];
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);

                        if (bytesRead > 0)
                        {
                            string receivedMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                            Console.WriteLine(receivedMessage);
                        }
                    }
                    catch
                    {
                        break;
                    }
                }
            });

            // Gửi tin nhắn hoặc file đến server
            while (true)
            {
                string message = Console.ReadLine();

                if (message.Equals("SEND_FILE", StringComparison.OrdinalIgnoreCase))
                {
                    Console.Write("Enter file path : ");
                    string filePath = Console.ReadLine();

                    if (File.Exists(filePath))
                    {
                        byte[] commandData = Encoding.UTF8.GetBytes("SEND_FILE");
                        stream.Write(commandData, 0, commandData.Length);

                        byte[] fileBytes = File.ReadAllBytes(filePath);
                        stream.Write(fileBytes, 0, fileBytes.Length);
                        Console.WriteLine($"Send file: {filePath}");
                    }
                    else
                    {
                        Console.WriteLine("File doesn't exits .");
                    }
                }
                else
                {
                    byte[] data = Encoding.UTF8.GetBytes(message);
                    stream.Write(data, 0, data.Length);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error client: {ex.Message}");
        }
    }
}
