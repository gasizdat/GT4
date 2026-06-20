using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GT4.Core.Utils;

internal class AppConfigurationProvider : ConfigurationProvider, IInteractiveConfiguration
{
  private readonly JsonSerializerOptions _SerializationOptions;
  private readonly IFileSystem _FileSystem;
  private readonly IStorage _Storage;
  private bool _UpdateRequested = false;

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

  protected void Save()
  {
    using var stream = _FileSystem.OpenWriteStream(File);
    JsonSerializer.Serialize(stream, Data, _SerializationOptions);
  }

  protected void Update()
  {
    if (!Interlocked.Exchange(ref _UpdateRequested, true))
    {
      async Task DelayAndUpdate()
      {
        await Task.Delay(TimeSpan.FromSeconds(2));
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
          try
          {
            Save();
            OnReload();
          }
          finally
          {
            Interlocked.Exchange(ref _UpdateRequested, false);
          }
        });
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
    }
  }

  public void SetKey(string key, string value)
  {
    lock (Data)
    {
      Data[key] = value;
      Update();
    }
  }

  public void RemoveKey(string key)
  {
    lock (Data)
    {
      Data.Remove(key);
      Update();
    }
  }
}
