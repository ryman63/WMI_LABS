using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Text;
using System.Xml.Linq;

class WmiServer
{
    private const string Username = "admin";
    private const string Password = "password";
    private static int UpdateIntervalMs = 5000; // Интервал обновления данных

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
                LogAction("Requested basic system stats");
            }
            else if (request.Url.AbsolutePath == "/process" && request.HttpMethod == "DELETE")
            {
                string id = request.QueryString["id"];
                responseString = KillProcessById(id);
                LogAction($"Attempted to terminate process ID: {id}");
            }
            else if (request.Url.AbsolutePath == "/processes" && request.HttpMethod == "GET")
            {
                string filter = request.QueryString["filter"];
                responseString = GetRunningProcesses(filter);
                LogAction(filter != null
                    ? $"Requested running processes with filter: {filter}"
                    : "Requested running processes list");
            }
            else if (request.Url.AbsolutePath == "/system-info" && request.HttpMethod == "GET")
            {
                responseString = GetExtendedSystemInfo();
                LogAction("Requested extended system info");
            }
            else if (request.Url.AbsolutePath == "/update-interval" && request.HttpMethod == "POST")
            {
                string interval = new StreamReader(request.InputStream).ReadToEnd();
                responseString = SetUpdateInterval(interval);
                LogAction($"Update interval changed to {interval} ms");
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

    static string GetExtendedSystemInfo()
    {
        var processCount = GetWmiValue("Win32_Process", "ProcessId", true).Split('\n').Length;
        var systemUptime = GetSystemUptime();

        return $"{{ \"processes\": {processCount}, \"uptime\": \"{systemUptime}\" }}";
    }

    static string GetSystemUptime()
    {
        using (var searcher = new ManagementObjectSearcher("SELECT LastBootUpTime FROM Win32_OperatingSystem"))
        {
            foreach (var obj in searcher.Get())
            {
                var bootTime = ManagementDateTimeConverter.ToDateTime(obj["LastBootUpTime"].ToString());
                var uptime = DateTime.Now - bootTime;
                return uptime.ToString(@"d\.hh\:mm\:ss"); // Days.Hours:Minutes:Seconds
            }
        }
        return "N/A";
    }

    static string SetUpdateInterval(string interval)
    {
        if (int.TryParse(interval, out int newInterval) && newInterval > 0)
        {
            UpdateIntervalMs = newInterval;
            return $"Update interval set to {UpdateIntervalMs} ms.";
        }
        return "Invalid interval.";
    }

    static string KillProcessById(string id)
    {
        if (!int.TryParse(id, out int processId))
        {
            return "Invalid process ID.";
        }

        try
        {
            string query = $"SELECT * FROM Win32_Process WHERE ProcessId = {processId}";
            using (var searcher = new ManagementObjectSearcher(query))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
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

    static string GetRunningProcesses(string filter = null)
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

                    string processInfo = $"ID: {processId}, Name: {name}, Memory: {memoryUsage} KB, Path: {executablePath}";

                    if (string.IsNullOrEmpty(filter))
                    {
                        result.AppendLine(processInfo);
                    }
                    else
                    {
                        // Если filter - это число (ID процесса)
                        if (int.TryParse(filter, out int idFilter))
                        {
                            string mask = "ID: " + idFilter + ",";
                            if (processInfo.Contains(mask))
                            {
                                result.AppendLine(processInfo);
                            }
                        }
                        // Если filter - это строка (название процесса)
                        else
                        {
                            if (name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                result.AppendLine(processInfo);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
        return result.ToString();
    }


    static string GetWmiValue(string wmiClass, string propertyName, bool countOnly = false)
    {
        StringBuilder result = new StringBuilder();
        using (var searcher = new ManagementObjectSearcher($"SELECT {propertyName} FROM {wmiClass}"))
        {
            foreach (var obj in searcher.Get())
            {
                if (countOnly) result.AppendLine();
                else result.AppendLine(obj[propertyName]?.ToString() ?? "N/A");
            }
        }
        return result.ToString();
    }

    static void LogAction(string action)
    {
        File.AppendAllText("server.log", $"{DateTime.Now}: {action}\n");
    }
}
