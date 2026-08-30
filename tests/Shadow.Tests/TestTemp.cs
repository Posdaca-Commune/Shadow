namespace Shadow.Tests;

/// <summary>
/// Creates a unique temporary directory (or file) that is removed on Dispose.
/// </summary>
internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "shadow-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string CreateFile(string fileName, byte[] contents)
    {
        var fullPath = System.IO.Path.Combine(Path, fileName);
        File.WriteAllBytes(fullPath, contents);
        return fullPath;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
        }
    }
}
