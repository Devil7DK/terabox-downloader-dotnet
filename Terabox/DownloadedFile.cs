namespace Devil7Softwares.TeraboxDownloader.Terabox;

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
