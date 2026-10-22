namespace UnattendHost.Web;

/// <summary>
/// `dotnet UnattendHost.Web.dll --healthcheck` asks the running app for /healthz and exits
/// with 0 or 1. The container HEALTHCHECK uses this because the runtime image has no shell or curl.
/// </summary>
internal static class HealthCheck
{
    public static async Task<int> RunAsync()
    {
        string port = (Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS") ?? "8080").Split(';', ',')[0];
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        try
        {
            using var response = await http.GetAsync($"http://127.0.0.1:{port}/healthz");
            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}
