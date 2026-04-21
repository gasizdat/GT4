namespace GT4.Core.Utils;

public interface IActiveConfiguration
{
  public void SetKey(string key, string value);

  public void RemoveKey(string key);
}
