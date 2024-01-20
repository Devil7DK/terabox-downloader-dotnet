using Devil7Softwares.TeraboxDownloader.Enums;

namespace Devil7Softwares.TeraboxDownloader.Terabox;

internal class ResolvedUrl
{
    public string Url { get; set; }

    public string FileName { get; set; }

    public string FileId { get; set; }

    public ResolvedUrl(string url, string fileName, string fileId)
    {
        Url = url;
        FileName = fileName;
        FileId = fileId;
    }
}

internal interface IUrlResolver
{
    /// <summary>
    /// Resolves the given Terabox share URL to a direct download URL.
    /// </summary>
    public Task<ResolvedUrl> Resolve(string url, CancellationToken cancellationToken);
}

internal delegate IUrlResolver UrlResolverFactory(DownloadMethod method);
