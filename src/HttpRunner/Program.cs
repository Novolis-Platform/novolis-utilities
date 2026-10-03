using System.Text.Json;
using Novolis.Http.Client;
using Novolis.Http.Documents;
using Novolis.Http.Variables;

namespace Novolis.HttpRunner;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: Novolis.HttpRunner <request.json>");
            return 2;
        }

        var path = Path.GetFullPath(args[0]);
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"Request document was not found: {path}");
            return 2;
        }

        var document = JsonSerializer.Deserialize<HttpRequestDocument>(
            await File.ReadAllTextAsync(path),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (document is null)
        {
            Console.Error.WriteLine("Request document was empty or invalid.");
            return 2;
        }

        var resolved = await new TemplateHttpVariableResolver().ResolveAsync(
            document,
            new HttpEnvironment());
        using var request = resolved.CreateRequest();
        using var http = new HttpClient();
        using var response = await new RestClient(http, [], []).SendAsync(request, CancellationToken.None);
        Console.WriteLine($"{(int)response.StatusCode} {response.ReasonPhrase}");
        Console.WriteLine(await response.Content.ReadAsStringAsync());
        return response.IsSuccessStatusCode ? 0 : 1;
    }
}
