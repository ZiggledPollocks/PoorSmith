/// <summary>Text persistence mechanism. Callers retain schema, logging and save policy.</summary>
public interface ITextStore
{
    bool Exists { get; }
    string Read();
    void Write(string text);
    void Flush();
}
