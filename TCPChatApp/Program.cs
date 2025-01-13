using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class Program
{
    // Danh sách tất cả các client kết nối
    static List<TcpClient> clients = new List<TcpClient>();

    static void Main(string[] args)
    {
        Console.WriteLine("Choose mode: 1 for Server, 2 for Client");
        string choice = Console.ReadLine();

        if (choice == "1")
        {
            StartServer();
        }
        else if (choice == "2")
        {
            StartClient();
        }
        else
        {
            Console.WriteLine("Invalid choice.");
        }
    }

    static void StartServer()
    {
        Console.WriteLine($"Current directory: {Directory.GetCurrentDirectory()}");
        TcpListener listener = new TcpListener(IPAddress.Any, 8888);
        listener.Start();
        Console.WriteLine("Server started on port 8888");

        ThreadPool.QueueUserWorkItem(_ =>
        {
            while (true)
            {
                TcpClient client = listener.AcceptTcpClient();
                lock (clients)
                {
                    clients.Add(client);
                }
                Console.WriteLine("New client connected.");
                ThreadPool.QueueUserWorkItem(_ => HandleClient(client));
            }
        });

        Console.WriteLine("Press Enter to exit.");
        Console.ReadLine();
        listener.Stop();
    }

    static void HandleClient(TcpClient client)
    {
        string clientName = string.Empty;
        try
        {
            NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[1024];
            int bytesRead;

            // Đọc tên client ngay khi kết nối
            bytesRead = stream.Read(buffer, 0, buffer.Length);
            clientName = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
            Console.WriteLine($"Client connected with name: {clientName}");

            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                Console.WriteLine($"Received message from {clientName}: {message}");

                if (message.StartsWith("SEND_FILE", StringComparison.OrdinalIgnoreCase))
                {
                    // Nhận file
                    byte[] fileBuffer = new byte[1024 * 1024];
                    bytesRead = stream.Read(fileBuffer, 0, fileBuffer.Length);

                    if (bytesRead > 0)
                    {
                        byte[] fileData = new byte[bytesRead];
                        Array.Copy(fileBuffer, fileData, bytesRead);

                        // Tạo tên file với client name và timestamp
                        string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                        string fileName = $"mat_{timestamp}.txt";

                        // Lưu file
                        File.WriteAllBytes(fileName, fileData);
                        Console.WriteLine($"File saved: {fileName}");
                    }
                }
                else
                {
                    // Gửi message cho tất cả client khác
                    string formattedMessage = $"{clientName}: {message}";
                    lock (clients)
                    {
                        foreach (var c in clients)
                        {
                            if (c != client)
                            {
                                try
                                {
                                    NetworkStream clientStream = c.GetStream();
                                    byte[] data = Encoding.UTF8.GetBytes(formattedMessage);
                                    clientStream.Write(data, 0, data.Length);
                                }
                                catch
                                {
                                    // Nếu có client nào đó ngắt kết nối, bỏ qua
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Client error: {ex.Message}");
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

    static void StartClient()
    {
        try
        {
            TcpClient client = new TcpClient();
            client.Connect("127.0.0.1", 8888);
            Console.WriteLine("Connected to server.");

            NetworkStream stream = client.GetStream();

            // Gửi tên client ngay khi kết nối
            Console.Write("Enter your name: ");
            string clientName = Console.ReadLine();
            byte[] nameData = Encoding.UTF8.GetBytes(clientName);
            stream.Write(nameData, 0, nameData.Length);

            ThreadPool.QueueUserWorkItem(_ =>
            {
                while (true)
                {
                    byte[] buffer = new byte[1024];
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);

                    if (bytesRead > 0)
                    {
                        string receivedMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        Console.WriteLine(receivedMessage);
                    }
                }
                
            });

            while (true)
            {
                string message = Console.ReadLine();
                if (message.StartsWith("SEND_FILE", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Enter file path:");
                    string filePath = Console.ReadLine();

                    if (File.Exists(filePath))
                    {
                        // Gửi lệnh "SEND_FILE" trước
                        byte[] commandData = Encoding.UTF8.GetBytes("SEND_FILE");
                        stream.Write(commandData, 0, commandData.Length);

                        // Gửi file
                        byte[] fileBytes = File.ReadAllBytes(filePath);
                        stream.Write(fileBytes, 0, fileBytes.Length);
                        Console.WriteLine($"File {filePath} sent to server.");
                    }
                    else
                    {
                        Console.WriteLine("File does not exist.");
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
            Console.WriteLine($"Client error: {ex.Message}");
        }
    }
}
