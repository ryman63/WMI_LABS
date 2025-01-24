using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net;
using System.Text;

class WmiServer
{
    private const string Username = "admin";
    private const string Password = "password";

    static void Main()
    {
        HttpListener listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:5000/");
        listener.Start();
        Console.WriteLine("Server is running...");

        while (true)
        {
            var context = listener.GetContext();
            var request = context.Request;
            var response = context.Response;

            // Авторизация
            if (!Authenticate(request))
            {
                response.StatusCode = 401; // Unauthorized
                response.Close();
                continue;
            }

            // Обработка запросов
            string responseString = string.Empty;
            if (request.Url.AbsolutePath == "/monitor" && request.HttpMethod == "GET")
            {
                responseString = GetSystemStats();
            }
            else if (request.Url.AbsolutePath == "/process" && request.HttpMethod == "DELETE")
            {
                string id = request.QueryString["id"];
                responseString = KillProcessById(id);
            }
            else if(request.Url.AbsolutePath == "/processes" && request.HttpMethod == "GET")
            {
                responseString = GetRunningProcesses();
            }
            else
            {
                response.StatusCode = 404; // Not Found
            }

            byte[] buffer = Encoding.UTF8.GetBytes(responseString);
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.Close();
        }
    }

    static bool Authenticate(HttpListenerRequest request)
    {
        string authHeader = request.Headers["Authorization"];
        if (authHeader == null || !authHeader.StartsWith("Basic ")) return false;

        string encodedCredentials = authHeader.Substring(6);
        string decodedCredentials = Encoding.UTF8.GetString(Convert.FromBase64String(encodedCredentials));
        string[] credentials = decodedCredentials.Split(':');

        return credentials[0] == Username && credentials[1] == Password;
    }

    static string GetSystemStats()
    {
        var cpuUsage = GetWmiValue("Win32_Processor", "LoadPercentage");
        var memoryUsage = GetWmiValue("Win32_OperatingSystem", "FreePhysicalMemory");

        return $"{{ \"cpu\": \"{cpuUsage}%\", \"free memory\": \"{memoryUsage} KB\" }}";
    }

    static string GetWmiValue(string wmiClass, string propertyName)
    {
        using (var searcher = new ManagementObjectSearcher($"SELECT {propertyName} FROM {wmiClass}")) {
            foreach (var obj in searcher.Get())
            {
                return obj[propertyName]?.ToString() ?? "N/A";
            }
            return "N/A";
        }

    }

    static string KillProcessById(string id)
    {
        if (!int.TryParse(id, out int processId))
        {
            return "Invalid process ID.";
        }

        try
        {
            // Ищем процесс по ID через WMI
            string query = $"SELECT * FROM Win32_Process WHERE ProcessId = {processId}";
            using (var searcher = new ManagementObjectSearcher(query))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    // Используем метод WMI для завершения процесса
                    var result = obj.InvokeMethod("Terminate", null);
                    return result?.ToString() == "0"
                        ? "Process terminated successfully."
                        : $"Failed to terminate process. Error code: {result}";
                }

            }

            return "Process not found.";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    static string GetRunningProcesses()
    {
        StringBuilder result = new StringBuilder();
        try
        {
            using (var searcher = new ManagementObjectSearcher("SELECT ProcessId, Name, WorkingSetSize, ExecutablePath FROM Win32_Process"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    string name = obj["Name"]?.ToString() ?? "N/A";
                    string processId = obj["ProcessId"]?.ToString() ?? "N/A";
                    string memoryUsage = obj["WorkingSetSize"]?.ToString() ?? "N/A";
                    string executablePath = obj["ExecutablePath"]?.ToString() ?? "N/A";

                    result.AppendLine($"ID: {processId}, Name: {name}, Memory: {memoryUsage} KB, Path: {executablePath}");
                }
            }
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
        return result.ToString();
    }

}
