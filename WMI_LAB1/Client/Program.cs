using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

class WmiClient
{
    private static string ServerUrl = "http://localhost:5000/";
    private static HttpClient client = new HttpClient();

    static async Task Main()
    {
        // Запрашиваем логин и пароль у пользователя
        Console.Write("Enter username: ");
        string username = Console.ReadLine();

        Console.Write("Enter password: ");
        string password = ReadPassword();

        // Устанавливаем авторизацию
        var authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

        Console.WriteLine("\nConnected to server.\n");

        while (true)
        {
            Console.WriteLine("1. View system stats");
            Console.WriteLine("2. View all running processes");
            Console.WriteLine("3. Kill process");
            Console.WriteLine("0. Exit");
            Console.Write("Choose an option: ");
            string choice = Console.ReadLine();

            switch (choice)
            {
                case "1":
                    await ViewSystemStats();
                    break;

                case "2":
                    await ViewAllRunningProcesses();
                    break;

                case "3":
                    await KillProcess();
                    break;

                case "0":
                    Console.WriteLine("Exiting...");
                    return;

                default:
                    Console.WriteLine("Invalid choice. Try again.");
                    break;
            }
        }
    }

    private static async Task ViewSystemStats()
    {
        try
        {
            string stats = await client.GetStringAsync(ServerUrl + "monitor");
            Console.WriteLine("\nSystem Stats:");
            Console.WriteLine(stats);
            Console.WriteLine("");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    private static async Task ViewAllRunningProcesses()
    {
        try
        {
            string stats = await client.GetStringAsync(ServerUrl + "processes");
            Console.WriteLine("\nRunning processes:");
            Console.WriteLine(stats);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    private static async Task KillProcess()
    {
        Console.Write("Enter process ID to kill: ");
        string id = Console.ReadLine();

        try
        {
            HttpResponseMessage response = await client.DeleteAsync(ServerUrl + "process?id=" + id);
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                Console.WriteLine($"Error: Unauthorized");
            }
            else
            {
                string result = await response.Content.ReadAsStringAsync();
                Console.WriteLine("Response: " + result);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    private static string ReadPassword()
    {
        StringBuilder password = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                break;
            }
            if (key.Key == ConsoleKey.Backspace && password.Length > 0)
            {
                Console.Write("\b \b");
                password.Length--;
            }
            else if (!char.IsControl(key.KeyChar))
            {
                Console.Write("*");
                password.Append(key.KeyChar);
            }
        }
        return password.ToString();
    }
}
