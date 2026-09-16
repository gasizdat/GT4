using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GT4.Core.Utils;

public abstract partial class FlatJsonConfigurationProvider : ConfigurationProvider, IInteractiveConfiguration
{
  [JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
  [JsonSerializable(typeof(Dictionary<string, string?>))]
  private partial class JsonContext : JsonSerializerContext
  {
  }

  private static readonly TimeSpan SaveDebounce = TimeSpan.FromSeconds(2);

  private readonly IFileSystem _FileSystem;
  private bool _FlushRequested = false;

  protected FlatJsonConfigurationProvider(IFileSystem fileSystem)
  {
    _FileSystem = fileSystem;
  }

  public abstract string Name { get; }

  protected abstract FileDescription File { get; }

  protected void RequestFlush()
  {
    if (!Interlocked.Exchange(ref _FlushRequested, true))
    {
      async Task DelayAndUpdate()
      {
        try
        {
          await Task.Delay(SaveDebounce);
        }
        finally
        {
          Flush();
          OnReload();
        }
      }

      Task.Run(DelayAndUpdate);
    }
  }

  public override void Load()
  {
    base.Load();

    try
    {
      if (_FileSystem.FileExists(File))
      {
        using var stream = _FileSystem.OpenReadStream(File);
        var data = JsonSerializer.Deserialize(stream, JsonContext.Default.DictionaryStringString);
        if (data == null)
        {
          return;
        }

        lock (Data)
        {
          foreach (var item in data)
          {
            Data.Add(item);
          }
        }
      }
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
    {
      // A missing/locked/corrupt config file must not crash startup; the provider just stays empty.
      // Unexpected exceptions are left to propagate so real bugs are not hidden.
      System.Diagnostics.Debug.WriteLine($"{nameof(Load)}(): {ex}");
    }
  }

  public void SetKey(string key, string value)
  {
    lock (Data)
    {
      Data[key] = value;
      RequestFlush();
    }
  }

  public void RemoveKey(string key)
  {
    lock (Data)
    {
      Data.Remove(key);
      RequestFlush();
    }
  }

  public void Flush()
  {
    lock (Data)
    {
      if (Interlocked.Exchange(ref _FlushRequested, false))
      {
        try
        {
          using var stream = _FileSystem.OpenWriteStream(File);
          JsonSerializer.Serialize(stream, Data, JsonContext.Default.DictionaryStringString);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
          System.Diagnostics.Debug.WriteLine($"{nameof(Flush)}(): {ex}");
        }
      }
    }
  }
}
