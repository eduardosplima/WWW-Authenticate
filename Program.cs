using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;

public class DigestAuthHelper
{
    private static HttpClient httpClient = new HttpClient();

    public static async Task<Dictionary<string, string>> GetDigestAuthHeaderDetailsAsync(string url)
    {
        HttpResponseMessage response = await httpClient.GetAsync(url);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            IEnumerable<string> wwwAuthenticateHeaders;
            if (response.Headers.TryGetValues("WWW-Authenticate", out wwwAuthenticateHeaders))
            {
                var wwwAuthenticateHeader = wwwAuthenticateHeaders.FirstOrDefault();
                if (!string.IsNullOrEmpty(wwwAuthenticateHeader))
                {
                    return ParseWwwAuthenticateHeader(wwwAuthenticateHeader);
                }
            }
        }

        return null;
    }

    private static Dictionary<string, string> ParseWwwAuthenticateHeader(string header)
    {
        var details = new Dictionary<string, string>();
        var matches = Regex.Matches(header, @"(\w+)=""([^""]+)""");

        foreach (Match match in matches)
        {
            if (match.Groups.Count == 3)
            {
                details[match.Groups[1].Value] = match.Groups[2].Value;
            }
        }

        return details;
    }

    public static string ComputeSha256Hash(string input)
    {
        using (SHA256 sha256Hash = SHA256.Create())
        {
            byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(input));

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                builder.Append(bytes[i].ToString("x2"));
            }
            return builder.ToString();
        }
    }

    public static async Task<string> MakeAuthenticatedGetRequestAsync(string url, string username, string password)
    {
        var authDetails = await GetDigestAuthHeaderDetailsAsync(url);
        if (authDetails == null) return "Erro ao obter o cabeçalho WWW-Authenticate";

        Uri uri = new Uri(url);

        string method = "GET";
        string path = uri.AbsoluteUri.Substring(uri.GetLeftPart(UriPartial.Authority).Length);
        string ha1 = ComputeSha256Hash($"{username}:{authDetails["realm"]}:{password}");
        string ha2 = ComputeSha256Hash($"{method}:{path}");
        string response = ComputeSha256Hash($"{ha1}:{authDetails["nonce"]}:{ha2}");
        string authHeader = $"Digest username=\"{username}\", realm=\"{authDetails["realm"]}\", nonce=\"{authDetails["nonce"]}\", uri=\"{path}\", response=\"{response}\", algorithm=SHA-256";

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, url);
        requestMessage.Headers.Add("Authorization", authHeader);

        var responseMessage = await httpClient.SendAsync(requestMessage);
        if (responseMessage.IsSuccessStatusCode)
        {
            return await responseMessage.Content.ReadAsStringAsync();
        }
        else
        {
            return $"Erro na requisição com status code {responseMessage.StatusCode}";
        }
    }

}

// Example usage
class Program
{
    static async Task Main(string[] args)
    {
        string url = "https://httpbin.org/digest-auth/auth/user/passwd";
        string username = "user";
        string password = "passwd";

        string result = await DigestAuthHelper.MakeAuthenticatedGetRequestAsync(url, username, password);
        Console.WriteLine(result);
    }
}
