using Xunit;

namespace GT4.UI.DeviceTests;

// Temporary: prints DeviceInfo.Idiom as reported by the CI agent, to settle whether OnIdiom's
// Desktop=False result there (issue #398) is the CI agent genuinely not reporting Desktop, or a
// timing defect in OnIdiom's own evaluation. Delete once answered.
public class DiagnosticIdiomTests
{
  [Fact]
  public void Diagnostic_prints_the_device_idiom() =>
    Assert.Fail($"DeviceInfo.Idiom on this runner = {DeviceInfo.Idiom}");
}
