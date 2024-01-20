using System.Net;
using System.Text.Json.Serialization;
using Devil7Softwares.TeraboxDownloader.Utils;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using RestSharp;

namespace Devil7Softwares.TeraboxDownloader.Terabox;

internal class TeraboxDownloaderDotNetPayload
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

internal class TeraboxDownloaderDotNetResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

internal class TeraboxDownloaderDotNetResolver : IUrlResolver
{
    private readonly RestClient _client;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TeraboxDownloaderDotNetResolver> _logger;

    public TeraboxDownloaderDotNetResolver(ILogger<TeraboxDownloaderDotNetResolver> logger, IConfiguration configuration)
    {
        _client = new RestClient(new RestClientOptions()
        {
            BaseUrl = new Uri("https://teraboxdownloader.net/"),
            CookieContainer = new CookieContainer(),
        });
        _client.AddDefaultHeader("User-Agent", configuration.UserAgent);

        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ResolvedUrl> Resolve(string url, CancellationToken cancellationToken)
    {
        RestResponse<string> htmlResponse = await _client.ExecuteAsync<string>(new RestRequest("/"), cancellationToken);

        if (string.IsNullOrWhiteSpace(htmlResponse.Content))
        {
            throw new Exception("Invalid response from teraboxdownloader.net");
        }

        HtmlDocument document = new();
        document.LoadHtml(htmlResponse.Content);

        HtmlNode? tokenInput = document.DocumentNode.SelectSingleNode("//input[@id='token']");

        if (tokenInput is null)
        {
            throw new Exception("Failed to find token input");
        }

        string? token = tokenInput.GetAttributeValue("value", null);

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new Exception("Failed to get token");
        }

        TeraboxDownloaderDotNetPayload payload = new()
        {
            Token = token,
            Url = url,
        };

        TeraboxDownloaderDotNetResponse? resolveUrlResponse = await _client.PostAsync<TeraboxDownloaderDotNetResponse>(new RestRequest("/").AddJsonBody(payload), cancellationToken);

        if (resolveUrlResponse is null)
        {
            throw new Exception("Invalid response for url from teraboxdownloader.net");
        }

        if (resolveUrlResponse.Status != "success")
        {
            throw new Exception(resolveUrlResponse.Message ?? "Failed to resolve url from teraboxdownloader.net");
        }

        if (string.IsNullOrWhiteSpace(resolveUrlResponse.Message))
        {
            throw new Exception("Invalid response html for url from teraboxdownloader.net");
        }

        HtmlDocument downloadUrlDocument = new();
        downloadUrlDocument.LoadHtml(resolveUrlResponse.Message);

        HtmlNode? downloadFileAnchor = downloadUrlDocument.DocumentNode.SelectSingleNode("//a[@id='download_file']");

        if (downloadFileAnchor is null)
        {
            throw new Exception("Failed to find download link");
        }

        string? downloadUrl = downloadFileAnchor.GetAttributeValue("href", null);

        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            throw new Exception("Failed to get download link");
        }

        string fileName = downloadUrlDocument.DocumentNode.SelectSingleNode("//img")?.GetAttributeValue("alt", null) ?? Guid.NewGuid().ToString();

        string fileId = new Uri(downloadUrl).GetQueryValue("fid") ?? Guid.NewGuid().ToString();

        return new ResolvedUrl(downloadUrl, fileName, fileId);
    }
}
