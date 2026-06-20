namespace GT4.Core.Utils;

public interface IInteractiveConfiguration
{
  public void SetKey(string key, string value);

  public void RemoveKey(string key);

  public void Flush();

  public string Name { get; }
}
