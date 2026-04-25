namespace GT4.Core.Utils;

internal class SettingEditorsHolder : ISettingEditorsHolder
{
  private readonly List<ISettingEditor> _SettingEditors = new();

  public void AddSetting(ISettingEditor settingEditor)
  {
    _SettingEditors.Add(settingEditor);
  }

  public IEnumerable<ISettingEditor> GetSettingEditors()
  {
    return _SettingEditors;
  }
}
