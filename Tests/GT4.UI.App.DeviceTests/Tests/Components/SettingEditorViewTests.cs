using GT4.Core.Utils;
using GT4.UI.Abstraction;
using Moq;
using System.Globalization;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers SettingEditorView's own logic: the Editor bindable property cascades PropertyChanged for
/// its derived properties, the Editor's Kind selects exactly one of the four value controls,
/// Boolean/BoundedNumeric/Choice values round-trip through the Value string, Value's setter change-detects
/// before writing through to the Editor, and ResetCommand delegates to Editor.ResetToDefault while
/// routing failures through IAlertService (SafeCommand's own behavior, exercised here via the view's
/// real command instance).
/// </summary>
public class SettingEditorViewTests
{
  private static async Task<TestableSettingEditorView> CreateViewAsync(TestServices services)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    return await MainThread.InvokeOnMainThreadAsync(() => new TestableSettingEditorView(services.Provider));
  }

  private static Mock<ISettingEditor> MakeEditor(string value = "current", SettingKind? kind = null)
  {
    var editor = new Mock<ISettingEditor>();
    editor.SetupGet(e => e.DisplayName).Returns("Display Name");
    editor.SetupGet(e => e.Description).Returns("Description");
    editor.SetupGet(e => e.Example).Returns("Example");
    editor.SetupGet(e => e.Kind).Returns(kind ?? new SettingKind.Text());
    editor.SetupProperty(e => e.Value, value);
    return editor;
  }

  private static Mock<ISettingEditor> MakePercentEditor(string value) =>
    MakeEditor(value, new SettingKind.BoundedNumeric(75, 200, 5, "%"));

  private static Mock<ISettingEditor> MakeChoiceEditor(string value) =>
    MakeEditor(value, new SettingKind.Choice([new("Unspecified", "System"), new("Dark", "Night")]));

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
  public async Task Setting_Editor_raises_PropertyChanged_for_itself_and_all_derived_properties()
  {
    var view = await CreateViewAsync(new TestServices());
    var editor = MakeEditor();
    var raised = new List<string?>();
    view.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

    await MainThread.InvokeOnMainThreadAsync(() => SetEditor(view, editor.Object));

    // BindableObject raises PropertyChanged for the bindable property itself ("Editor") before
    // OnEditorPropertyChanged cascades the derived ones. ValueMetadata has to precede NumericValue
    // and ChoiceOptions has to precede ChoiceValue: the slider coerces its value against the range
    // it currently holds, and the picker its selection against the options it currently lists.
    Assert.Equal(
      [
        "Editor", "Caption", "Description",
        "IsText", "IsBoolean", "IsBoundedNumeric", "IsChoice", "ValueMetadata", "ChoiceOptions",
        "Value", "BooleanValue", "NumericValue", "ChoiceValue", "Example"
      ],
      raised);
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
    Assert.Equal(["Value", "BooleanValue", "NumericValue", "ChoiceValue", "Example"], raised);
  }

  [Fact]
  public async Task Consecutive_writes_each_reach_the_Editor()
  {
    // The controls' echo is suppressed only while the view pushes state into them: a write that
    // follows that cascade must still land.
    var view = await CreateViewAsync(new TestServices());
    var editor = MakeEditor("first");
    await MainThread.InvokeOnMainThreadAsync(() => SetEditor(view, editor.Object));

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      view.Value = "second";
      view.Value = "third";
    });

    Assert.Equal("third", editor.Object.Value);
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
  public async Task ResetCommand_resets_the_Editor_and_raises_PropertyChanged_for_the_value_properties()
  {
    var services = new TestServices();
    var view = await CreateViewAsync(services);
    var editor = MakeEditor();
    await MainThread.InvokeOnMainThreadAsync(() => SetEditor(view, editor.Object));
    var raised = new List<string?>();
    view.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

    await MainThread.InvokeOnMainThreadAsync(() => view.ResetCommand.Execute(null));

    editor.Verify(e => e.ResetToDefault(), Times.Once());
    Assert.Equal(["Value", "BooleanValue", "NumericValue", "ChoiceValue", "Example"], raised);
    services.AlertService.Verify(a => a.ShowErrorAsync(It.IsAny<Exception>()), Times.Never());
  }

  public static TheoryData<SettingKind, string> KindsAndTheirControls => new()
  {
    { new SettingKind.Text(), "ValueEntry" },
    { new SettingKind.Boolean(), "ValueSwitch" },
    { new SettingKind.BoundedNumeric(75, 200, 5, "%"), "ValueSlider" },
    { new SettingKind.Choice([new("100%", "One hundred percent")]), "ValuePicker" },
  };

  [Theory]
  [MemberData(nameof(KindsAndTheirControls))]
  public async Task The_Editor_Kind_shows_exactly_one_value_control(SettingKind kind, string visibleControl)
  {
    var view = await CreateViewAsync(new TestServices());
    var editor = MakeEditor("100%", kind);

    await MainThread.InvokeOnMainThreadAsync(() => SetEditor(view, editor.Object));

    var slider = view.FindByName<Slider>("ValueSlider");
    var controls = new Dictionary<string, View>
    {
      ["ValueEntry"] = view.FindByName<Entry>("ValueEntry"),
      ["ValueSwitch"] = view.FindByName<Switch>("ValueSwitch"),
      // The slider shares its visibility with the value label wrapping it, so read the parent.
      ["ValueSlider"] = (View)slider.Parent,
      ["ValuePicker"] = view.FindByName<Picker>("ValuePicker"),
    };
    var visible = controls.Where(control => control.Value.IsVisible).Select(control => control.Key);

    Assert.Equal([visibleControl], visible);
  }

  [Theory]
  [InlineData("True", true)]
  [InlineData("False", false)]
  [InlineData("nonsense", false)]
  public async Task A_Boolean_Editor_reads_its_Value_as_the_switch_state(string value, bool expected)
  {
    var view = await CreateViewAsync(new TestServices());
    var editor = MakeEditor(value, new SettingKind.Boolean());

    await MainThread.InvokeOnMainThreadAsync(() => SetEditor(view, editor.Object));

    Assert.Equal(expected, view.BooleanValue);
    Assert.Equal(expected, view.FindByName<Switch>("ValueSwitch").IsToggled);
  }

  [Fact]
  public async Task Toggling_the_switch_writes_the_Boolean_Editor_Value_back_as_True_or_False()
  {
    var view = await CreateViewAsync(new TestServices());
    var editor = MakeEditor("False", new SettingKind.Boolean());
    await MainThread.InvokeOnMainThreadAsync(() => SetEditor(view, editor.Object));

    await MainThread.InvokeOnMainThreadAsync(() => view.FindByName<Switch>("ValueSwitch").IsToggled = true);

    Assert.Equal("True", editor.Object.Value);
  }

  [Fact]
  public async Task A_BoundedNumeric_Editor_drives_the_slider_range_and_position_from_its_Value()
  {
    var view = await CreateViewAsync(new TestServices());
    var editor = MakePercentEditor("150%");

    await MainThread.InvokeOnMainThreadAsync(() => SetEditor(view, editor.Object));

    var slider = view.FindByName<Slider>("ValueSlider");
    Assert.Equal(75, slider.Minimum);
    Assert.Equal(200, slider.Maximum);
    Assert.Equal(150, slider.Value);
  }

  [Fact]
  public async Task An_unparseable_BoundedNumeric_Value_falls_back_to_the_minimum()
  {
    var view = await CreateViewAsync(new TestServices());
    var editor = MakePercentEditor("nonsense");

    await MainThread.InvokeOnMainThreadAsync(() => SetEditor(view, editor.Object));

    Assert.Equal(75, view.NumericValue);
  }

  [Theory]
  [InlineData(137, "135%")]
  [InlineData(138, "140%")]
  [InlineData(200, "200%")]
  public async Task Moving_the_slider_snaps_to_the_step_and_writes_the_Value_back_with_its_unit(
    double dragged,
    string expected)
  {
    var view = await CreateViewAsync(new TestServices());
    var editor = MakePercentEditor("100%");
    await MainThread.InvokeOnMainThreadAsync(() => SetEditor(view, editor.Object));

    await MainThread.InvokeOnMainThreadAsync(() => view.FindByName<Slider>("ValueSlider").Value = dragged);

    Assert.Equal(expected, editor.Object.Value);
  }

  [Fact]
  public async Task A_fractional_BoundedNumeric_Value_round_trips_under_a_comma_decimal_culture()
  {
    var view = await CreateViewAsync(new TestServices());
    var editor = MakeEditor("1.5x", new SettingKind.BoundedNumeric(0.5, 2.5, 0.5, "x"));
    await MainThread.InvokeOnMainThreadAsync(() => SetEditor(view, editor.Object));

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      var culture = CultureInfo.CurrentCulture;
      CultureInfo.CurrentCulture = new CultureInfo("ru-RU");
      try
      {
        Assert.Equal(1.5, view.NumericValue);
        view.NumericValue = 2.5;
      }
      finally
      {
        CultureInfo.CurrentCulture = culture;
      }
    });

    Assert.Equal("2.5x", editor.Object.Value);
  }

  [Fact]
  public async Task A_Choice_Editor_fills_the_picker_with_the_option_labels_and_selects_its_Value()
  {
    var view = await CreateViewAsync(new TestServices());
    var editor = MakeChoiceEditor("Dark");

    await MainThread.InvokeOnMainThreadAsync(() => SetEditor(view, editor.Object));

    var picker = view.FindByName<Picker>("ValuePicker");
    Assert.Equal(["System", "Night"], picker.ItemsSource.Cast<SettingKind.Option>().Select(o => o.Label));
    Assert.Equal(1, picker.SelectedIndex);
  }

  [Fact]
  public async Task Picking_an_option_writes_its_Value_not_its_label_back_to_the_Editor()
  {
    var view = await CreateViewAsync(new TestServices());
    var editor = MakeChoiceEditor("Dark");
    await MainThread.InvokeOnMainThreadAsync(() => SetEditor(view, editor.Object));

    await MainThread.InvokeOnMainThreadAsync(() => view.FindByName<Picker>("ValuePicker").SelectedIndex = 0);

    Assert.Equal("Unspecified", editor.Object.Value);
  }

  [Fact]
  public async Task A_Choice_Value_naming_no_option_leaves_the_picker_unselected_without_rewriting_it()
  {
    // A hand-edited config can name an option the setting no longer offers. The picker then echoes
    // its empty selection back, which must not be mistaken for the user choosing something.
    var view = await CreateViewAsync(new TestServices());
    var editor = MakeChoiceEditor("Sepia");

    await MainThread.InvokeOnMainThreadAsync(() => SetEditor(view, editor.Object));

    Assert.Null(view.ChoiceValue);
    Assert.Equal(-1, view.FindByName<Picker>("ValuePicker").SelectedIndex);
    Assert.Equal("Sepia", editor.Object.Value);
  }

  [Fact]
  public async Task Resetting_a_Choice_Editor_moves_the_picker_onto_the_restored_option()
  {
    var view = await CreateViewAsync(new TestServices());
    var editor = MakeChoiceEditor("Dark");
    editor.Setup(e => e.ResetToDefault()).Callback(() => editor.Object.Value = "Unspecified");
    await MainThread.InvokeOnMainThreadAsync(() => SetEditor(view, editor.Object));

    await MainThread.InvokeOnMainThreadAsync(() => view.ResetCommand.Execute(null));

    Assert.Equal(0, view.FindByName<Picker>("ValuePicker").SelectedIndex);
  }

  [Fact]
  public async Task A_non_Choice_Editor_leaves_the_picker_empty_without_rewriting_its_Value()
  {
    // The picker is bound even while hidden: an empty option list pushes a null selection back, and
    // a Text setting's Value has to survive it.
    var view = await CreateViewAsync(new TestServices());
    var editor = MakeEditor("DD MM YYYY");

    await MainThread.InvokeOnMainThreadAsync(() => SetEditor(view, editor.Object));

    Assert.Empty(view.ChoiceOptions);
    Assert.Equal("DD MM YYYY", editor.Object.Value);
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
