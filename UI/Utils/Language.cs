using System.Globalization;

namespace GT4.UI.Utils;

public record class Language(string Code, string Name)
{
  public static readonly Language EN = new Language("en", "English");
  public static readonly Language RU = new Language("ru", "Русский");
  public static readonly Language Default = EN;
  public static readonly Language[] Languages = [EN, RU];

  public static Language Current
  {
    get
    {
      var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
      var currentLanguage = Languages.SingleOrDefault(l => l.Code == culture, Default);
      return currentLanguage;
    }

    set
    {
      var culture = new CultureInfo(value.Code);
      Thread.CurrentThread.CurrentCulture = culture;
      Thread.CurrentThread.CurrentUICulture = culture;
      CultureInfo.DefaultThreadCurrentCulture = culture;
      CultureInfo.DefaultThreadCurrentUICulture = culture;
      CultureInfo.CurrentUICulture = culture;
    }
  }

}