using System.IO;
using System.Text;

/// <summary>Stores text in files for the ITextStore contract.</summary>
public sealed class FileTextStore : ITextStore
{
    private readonly string path;
    public FileTextStore(string path) => this.path = path;
    public bool Exists => File.Exists(path);
    public string Read() => File.ReadAllText(path, Encoding.UTF8);
    public void Write(string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, text, new UTF8Encoding(false));
        if (File.Exists(path)) File.Replace(temporaryPath, path, path + ".bak");
        else File.Move(temporaryPath, path);
    }
    public void Flush() { }
}
