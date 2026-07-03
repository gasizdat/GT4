using GT4.Core.Project.Dto;
using GT4.UI.Pages;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Exposes NamesPage's protected seams for testing. Awaiting OnDeleteCommandAsync directly bypasses
/// SafeCommand's error routing (which needs Shell.Current, absent in this host); RequestUpdateNames
/// is the only way to set the getter-only CurrentName selection.
/// </summary>
internal sealed class TestableNamesPage(IServiceProvider services) : NamesPage(services)
{
  public Task InvokeDeleteAsync(object parameter) => OnDeleteCommandAsync(parameter);

  public void InvokeRequestUpdateNames(Name? selected = null) => RequestUpdateNames(selected);
}
