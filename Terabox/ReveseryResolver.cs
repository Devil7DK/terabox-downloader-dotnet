using System.Net;
using System.Text.Json.Serialization;
using Devil7Softwares.TeraboxDownloader.Utils;
using Microsoft.Extensions.Logging;
using RestSharp;
using RestSharp.Serializers.Json;

namespace Devil7Softwares.TeraboxDownloader.Terabox;

internal class TeraBoxFile
{
    [JsonPropertyName("filename")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("fs_id")]
    public string FsId { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public string Size { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("children")]
    public List<TeraBoxFile> Children { get; set; } = new();

    [JsonPropertyName("create_time")]
    public string CreateTime { get; set; } = string.Empty;

    [JsonPropertyName("is_dir")]
    public string IsDir { get; set; } = string.Empty;
}

internal class TeraboxShareInfo
{
    [JsonPropertyName("shareid")]
    public long ShareId { get; set; }

    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("uk")]
    public long Uk { get; set; }

    [JsonPropertyName("sign")]
    public string Sign { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    [JsonPropertyName("list")]
    public List<TeraBoxFile> List { get; set; } = new();
}

internal class GetDownloadUrlPayload
{
    [JsonPropertyName("shareid")]
    public long ShareId { get; set; }

    [JsonPropertyName("uk")]
    public long Uk { get; set; }

    [JsonPropertyName("sign")]
    public string Sign { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    [JsonPropertyName("fs_id")]
    public string FsId { get; set; } = string.Empty;
}


internal class GetDownloadUrlResponse
{
    [JsonPropertyName("downloadLink")]
    public string DownloadLink { get; set; } = string.Empty;
}

internal class ReveseryResolver : IUrlResolver
{
    private readonly RestClient _client;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ReveseryResolver> _logger;

    public ReveseryResolver(ILogger<ReveseryResolver> logger, IConfiguration configuration)
    {
        _client = new RestClient(new RestClientOptions()
        {
            BaseUrl = new Uri("https://terabox-dl.qtcloud.workers.dev/"),
            CookieContainer = new CookieContainer(),
        },
        configureSerialization: options =>
        {
            options.UseSystemTextJson();
        });
        _client.AddDefaultHeader("User-Agent", configuration.UserAgent);
        _client.AddDefaultHeader("Accept-Language", "en-US,en;q=0.5");
        _client.AddDefaultHeader("Sec-Fetch-Dest", "empty");
        _client.AddDefaultHeader("Sec-Fetch-Mode", "cors");
        _client.AddDefaultHeader("Sec-Fetch-Site", "same-origin");
        _client.AddDefaultHeader("Referer", "https://terabox-dl.qtcloud.workers.dev/");

        _configuration = configuration;
        _logger = logger;
    }

    private string GetShareCode(Uri uri)
    {
        if (uri.Query.Contains("surl"))
        {
            return "1" + uri.GetQueryValue("surl");
        }
        else
        {
            string[] paths = uri.AbsolutePath.Split('/');
            if (paths.Length > 1 && paths[^1][0] == '1')
            {
                return paths[^1];
            }
        }

        throw new Exception("Failed to get share id from URL!");
    }

    private async Task<TeraboxShareInfo> GetShareInfo(Uri uri, CancellationToken cancellationToken)
    {
        string shareCode = GetShareCode(uri);

        RestResponse<TeraboxShareInfo> shareInfoResponse = await _client.ExecuteAsync<TeraboxShareInfo>(new RestRequest($"/api/get-info?shorturl={shareCode}&pwd="), cancellationToken);

        if (shareInfoResponse.Data is null)
        {
            throw new Exception("Invalid response from terabox-dl.qtcloud.workers.dev");
        }

        return shareInfoResponse.Data;
    }

    private async Task<string> GetDownloadUrl(TeraboxShareInfo shareInfo, TeraBoxFile file, CancellationToken cancellationToken)
    {
        GetDownloadUrlPayload payload = new GetDownloadUrlPayload()
        {
            ShareId = shareInfo.ShareId,
            Uk = shareInfo.Uk,
            Sign = shareInfo.Sign,
            Timestamp = shareInfo.Timestamp,
            FsId = file.FsId,
        };

        try
        {
            RestResponse<GetDownloadUrlResponse> downloadUrlResponse = await _client.ExecuteAsync<GetDownloadUrlResponse>(new RestRequest("/api/get-download", method: Method.Post).AddJsonBody(payload), cancellationToken);

            if (downloadUrlResponse.Data is null)
            {
                throw new Exception("Invalid response for download url from terabox-dl.qtcloud.workers.dev");
            }

            return downloadUrlResponse.Data.DownloadLink;
        }
        catch (Exception ex)
        {
            throw new Exception("Failed to get download url from terabox-dl.qtcloud.workers.dev", ex);
        }
    }

    public async Task<ResolvedUrl[]> Resolve(string url, CancellationToken cancellationToken)
    {
        List<ResolvedUrl> resolvedUrls = new();

        Uri uri = new(url);

        TeraboxShareInfo shareInfo = await GetShareInfo(uri, cancellationToken);

        foreach (TeraBoxFile file in shareInfo.List)
        {
            string downloadUrl = await GetDownloadUrl(shareInfo, file, cancellationToken);
            string fileName = file.FileName;
            string fileId = file.FsId;

            resolvedUrls.Add(new ResolvedUrl(downloadUrl, fileName, fileId));
        }

        return resolvedUrls.ToArray();
    }
}
