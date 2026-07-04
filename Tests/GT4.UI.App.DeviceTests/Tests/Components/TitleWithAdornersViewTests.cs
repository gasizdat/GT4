using Moq;
using System.Windows.Input;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers TitleWithAdornersView's own logic directly: IsSubtitleTextVisible and the PropertyChanged
/// it raises off SubtitleText's change-detection (compared on the raw string, not on the derived
/// visibility -- see the null-vs-empty case below), and the two adorner click handlers, which always
/// raise their event and then conditionally run the bound command based on its own CanExecute.
/// </summary>
public class TitleWithAdornersViewTests
{
  private static async Task<TestableTitleWithAdornersView> CreateViewAsync()
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    return await MainThread.InvokeOnMainThreadAsync(() => new TestableTitleWithAdornersView());
  }

  [Fact]
  public async Task IsSubtitleTextVisible_is_false_when_SubtitleText_is_null_or_whitespace()
  {
    var view = await CreateViewAsync();

    Assert.False(view.IsSubtitleTextVisible);

    await MainThread.InvokeOnMainThreadAsync(() => view.SubtitleText = "   ");

    Assert.False(view.IsSubtitleTextVisible);
  }

  [Fact]
  public async Task IsSubtitleTextVisible_is_true_once_SubtitleText_holds_real_text()
  {
    var view = await CreateViewAsync();

    await MainThread.InvokeOnMainThreadAsync(() => view.SubtitleText = "Subtitle");

    Assert.True(view.IsSubtitleTextVisible);
  }

  [Fact]
  public async Task Changing_SubtitleText_raises_PropertyChanged_for_IsSubtitleTextVisible()
  {
    var view = await CreateViewAsync();
    var raised = new List<string?>();
    view.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

    await MainThread.InvokeOnMainThreadAsync(() => view.SubtitleText = "Subtitle");

    Assert.Contains(nameof(view.IsSubtitleTextVisible), raised);
  }

  [Fact]
  public async Task Setting_SubtitleText_to_its_current_value_raises_nothing()
  {
    var view = await CreateViewAsync();
    await MainThread.InvokeOnMainThreadAsync(() => view.SubtitleText = "Subtitle");
    var raised = new List<string?>();
    view.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

    await MainThread.InvokeOnMainThreadAsync(() => view.SubtitleText = "Subtitle");

    Assert.Empty(raised);
  }

  [Fact]
  public async Task Null_to_empty_SubtitleText_still_raises_PropertyChanged_despite_visibility_staying_false()
  {
    var view = await CreateViewAsync();
    var raised = new List<string?>();
    view.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

    await MainThread.InvokeOnMainThreadAsync(() => view.SubtitleText = "");

    Assert.False(view.IsSubtitleTextVisible);
    Assert.Contains(nameof(view.IsSubtitleTextVisible), raised);
  }

  [Fact]
  public async Task InvokeEditAdornerClicked_raises_the_event_and_runs_an_executable_command()
  {
    var view = await CreateViewAsync();
    var command = new Mock<ICommand>();
    command.Setup(c => c.CanExecute(It.IsAny<object?>())).Returns(true);
    var parameter = new object();
    var eventRaised = false;
    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      view.EditAdornerCommand = command.Object;
      view.AdornerCommandParameter = parameter;
      view.EditAdornerClicked += (_, _) => eventRaised = true;
    });

    await MainThread.InvokeOnMainThreadAsync(view.InvokeEditAdornerClicked);

    Assert.True(eventRaised);
    command.Verify(c => c.Execute(parameter), Times.Once());
  }

  [Fact]
  public async Task InvokeEditAdornerClicked_does_not_run_a_command_that_cannot_execute()
  {
    var view = await CreateViewAsync();
    var command = new Mock<ICommand>();
    command.Setup(c => c.CanExecute(It.IsAny<object?>())).Returns(false);
    await MainThread.InvokeOnMainThreadAsync(() => view.EditAdornerCommand = command.Object);

    await MainThread.InvokeOnMainThreadAsync(view.InvokeEditAdornerClicked);

    command.Verify(c => c.Execute(It.IsAny<object?>()), Times.Never());
  }

  [Fact]
  public async Task InvokeEditAdornerClicked_with_no_bound_command_only_raises_the_event()
  {
    var view = await CreateViewAsync();
    var eventRaised = false;
    await MainThread.InvokeOnMainThreadAsync(() => view.EditAdornerClicked += (_, _) => eventRaised = true);

    var exception = await MainThread.InvokeOnMainThreadAsync(() => Record.Exception(view.InvokeEditAdornerClicked));

    Assert.Null(exception);
    Assert.True(eventRaised);
  }

  [Fact]
  public async Task InvokeDeleteAdornerClicked_raises_the_event_and_runs_an_executable_command()
  {
    var view = await CreateViewAsync();
    var command = new Mock<ICommand>();
    command.Setup(c => c.CanExecute(It.IsAny<object?>())).Returns(true);
    var parameter = new object();
    var eventRaised = false;
    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      view.DeleteAdornerCommand = command.Object;
      view.AdornerCommandParameter = parameter;
      view.DeleteAdornerClicked += (_, _) => eventRaised = true;
    });

    await MainThread.InvokeOnMainThreadAsync(view.InvokeDeleteAdornerClicked);

    Assert.True(eventRaised);
    command.Verify(c => c.Execute(parameter), Times.Once());
  }

  [Fact]
  public async Task InvokeDeleteAdornerClicked_does_not_run_a_command_that_cannot_execute()
  {
    var view = await CreateViewAsync();
    var command = new Mock<ICommand>();
    command.Setup(c => c.CanExecute(It.IsAny<object?>())).Returns(false);
    await MainThread.InvokeOnMainThreadAsync(() => view.DeleteAdornerCommand = command.Object);

    await MainThread.InvokeOnMainThreadAsync(view.InvokeDeleteAdornerClicked);

    command.Verify(c => c.Execute(It.IsAny<object?>()), Times.Never());
  }
}
