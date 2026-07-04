using GT4.Core.Utils;
using Moq;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers SettingEditorView's own logic: the Editor bindable property cascades PropertyChanged for
/// its four derived properties, Value's setter change-detects before writing through to the Editor,
/// and ResetCommand delegates to Editor.ResetToDefault while routing failures through IAlertService
/// (SafeCommand's own behavior, exercised here via the view's real command instance).
/// </summary>
public class SettingEditorViewTests
{
  private static async Task<TestableSettingEditorView> CreateViewAsync(TestServices services)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    return await MainThread.InvokeOnMainThreadAsync(() => new TestableSettingEditorView(services.Provider));
  }

  private static Mock<ISettingEditor> MakeEditor(string value = "current")
  {
    var editor = new Mock<ISettingEditor>();
    editor.SetupGet(e => e.DisplayName).Returns("Display Name");
    editor.SetupGet(e => e.Description).Returns("Description");
    editor.SetupGet(e => e.Example).Returns("Example");
    editor.SetupProperty(e => e.Value, value);
    return editor;
  }

  // SettingEditorView.Editor only exposes a getter over its BindableProperty, so tests assign
  // through SetValue directly instead of through the (nonexistent) property setter.
  private static void SetEditor(TestableSettingEditorView view, ISettingEditor editor) =>
    view.SetValue(TestableSettingEditorView.EditorProperty, editor);

  [Fact]
  public async Task With_no_Editor_the_display_properties_default_to_empty_strings()
  {
    var view = await CreateViewAsync(new TestServices());

    Assert.Equal(string.Empty, view.Caption);
    Assert.Equal(string.Empty, view.Description);
    Assert.Equal(string.Empty, view.Example);
    Assert.Equal(string.Empty, view.Value);
  }

  [Fact]
  public async Task Setting_Editor_raises_PropertyChanged_for_itself_and_all_four_derived_properties()
  {
    var view = await CreateViewAsync(new TestServices());
    var editor = MakeEditor();
    var raised = new List<string?>();
    view.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

    await MainThread.InvokeOnMainThreadAsync(() => SetEditor(view, editor.Object));

    // BindableObject raises PropertyChanged for the bindable property itself ("Editor") before
    // OnEditorPropertyChanged cascades the four derived ones.
    Assert.Equal(["Editor", "Caption", "Description", "Example", "Value"], raised);
  }

  [Fact]
  public async Task Setting_Editor_to_the_same_instance_raises_nothing()
  {
    var view = await CreateViewAsync(new TestServices());
    var editor = MakeEditor();
    await MainThread.InvokeOnMainThreadAsync(() => SetEditor(view, editor.Object));
    var raised = new List<string?>();
    view.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

    await MainThread.InvokeOnMainThreadAsync(() => SetEditor(view, editor.Object));

    Assert.Empty(raised);
  }

  [Fact]
  public async Task Setting_Value_to_a_new_string_updates_the_Editor_and_raises_PropertyChanged()
  {
    var view = await CreateViewAsync(new TestServices());
    var editor = MakeEditor("old");
    await MainThread.InvokeOnMainThreadAsync(() => SetEditor(view, editor.Object));
    var raised = new List<string?>();
    view.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

    await MainThread.InvokeOnMainThreadAsync(() => view.Value = "new");

    Assert.Equal("new", editor.Object.Value);
    Assert.Equal(["Value", "Example"], raised);
  }

  [Fact]
  public async Task Setting_Value_to_its_current_value_raises_nothing()
  {
    var view = await CreateViewAsync(new TestServices());
    var editor = MakeEditor("same");
    await MainThread.InvokeOnMainThreadAsync(() => SetEditor(view, editor.Object));
    var raised = new List<string?>();
    view.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

    await MainThread.InvokeOnMainThreadAsync(() => view.Value = "same");

    Assert.Empty(raised);
  }

  [Fact]
  public async Task ResetCommand_resets_the_Editor_and_raises_PropertyChanged_for_Value_and_Example()
  {
    var services = new TestServices();
    var view = await CreateViewAsync(services);
    var editor = MakeEditor();
    await MainThread.InvokeOnMainThreadAsync(() => SetEditor(view, editor.Object));
    var raised = new List<string?>();
    view.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

    await MainThread.InvokeOnMainThreadAsync(() => view.ResetCommand.Execute(null));

    editor.Verify(e => e.ResetToDefault(), Times.Once());
    Assert.Equal(["Value", "Example"], raised);
    services.AlertService.Verify(a => a.ShowErrorAsync(It.IsAny<Exception>()), Times.Never());
  }

  [Fact]
  public async Task ResetCommand_routes_a_throwing_ResetToDefault_through_IAlertService()
  {
    var services = new TestServices();
    var view = await CreateViewAsync(services);
    var editor = MakeEditor();
    editor.Setup(e => e.ResetToDefault()).Throws(new InvalidOperationException("boom"));
    await MainThread.InvokeOnMainThreadAsync(() => SetEditor(view, editor.Object));

    await MainThread.InvokeOnMainThreadAsync(() => view.ResetCommand.Execute(null));

    services.AlertService.Verify(a => a.ShowErrorAsync(It.IsAny<Exception>()), Times.Once());
  }
}
