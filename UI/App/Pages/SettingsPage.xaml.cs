namespace GT4.UI.Pages;

public partial class SettingsPage : ContentPage
{
  private string _DateExample;
  private string _DateFormat;

  public SettingsPage()
	{
		InitializeComponent();
  }

  public string DateExample
  {
    get => _DateExample;
    set
    {
      if (_DateExample != value)
      {
        _DateExample = value;
        OnPropertyChanged(nameof(DateExample));
      }
    }
  }

  public string DateFormat
  {
    get => _DateFormat;
    set
    {
      if (_DateFormat != value)
      {
        _DateFormat = value;
        OnPropertyChanged(nameof(DateFormat));
      }
    }
  }
}