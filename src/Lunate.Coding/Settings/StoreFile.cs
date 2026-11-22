namespace Lunate.Coding;

/// <summary>
/// The one read the stores share. A store file may be replaced while another thread or process
/// reads it, and on Windows the default read share denies delete: a reader holding the file would
/// make the store's replace fail with access-denied. Reads therefore take
/// <see cref="FileShare.ReadWrite"/> | <see cref="FileShare.Delete"/>.
/// </summary>
internal static class StoreFile
{
    internal static string ReadAllText(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete
        );
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
