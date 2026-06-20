using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GT4.Core.Utils;

internal class AppConfigurationProvider : ConfigurationProvider, IInteractiveConfiguration
{
  private static readonly TimeSpan SaveDebounce = TimeSpan.FromSeconds(2);

  private readonly JsonSerializerOptions _SerializationOptions;
  private readonly IFileSystem _FileSystem;
  private readonly IStorage _Storage;
  private bool _FlushRequested = false;

  public string Name => WellKnownActiveConfigurations.AppConfig;

  public AppConfigurationProvider(IFileSystem fileSystem, IStorage storage)
  {
    _FileSystem = fileSystem;
    _Storage = storage;
    _SerializationOptions = new()
    {
      WriteIndented = true,
      PropertyNameCaseInsensitive = true,
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
      DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
  }

  protected FileDescription File =>
    new FileDescription(_Storage.AppConfig, "appconfig.json", System.Net.Mime.MediaTypeNames.Text.Plain);

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
        var data = JsonSerializer.Deserialize<Dictionary<string, string?>>(stream, _SerializationOptions);
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
          JsonSerializer.Serialize(stream, Data, _SerializationOptions);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
          System.Diagnostics.Debug.WriteLine($"{nameof(Flush)}(): {ex}");
        }
      }
    }
  }
}
