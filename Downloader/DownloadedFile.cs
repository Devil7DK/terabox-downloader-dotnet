namespace Devil7Softwares.TeraboxDownloader.Downloader;

internal class DownloadedFile
{
    public string FileName { get; set; }

    public string FilePath { get; set; }

    public DownloadedFile(string fileName, string filePath)
    {
        FileName = fileName;
        FilePath = filePath;
    }
}
