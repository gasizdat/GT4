namespace GT4.Core.Utils;

public interface ISettingEditorsHolder
{
  void AddSetting(ISettingEditor settingEditor);

  IEnumerable<ISettingEditor> GetSettingEditors();
}
