using FluentAssertions;
using GT4.UI.Utils;
using Xunit;

namespace GT4.UI.View.Tests;

public class PageZoomTests
{
  [Fact]
  public void Scale_StartsAtOne()
  {
    new PageZoom().Scale.Should().Be(1.0);
  }

  [Fact]
  public void ZoomInAndOut_StepAQuarterEachWay()
  {
    var zoom = new PageZoom();

    zoom.ZoomIn().Should().BeTrue();
    zoom.Scale.Should().Be(1.25);
    zoom.ZoomOut().Should().BeTrue();
    zoom.ZoomOut().Should().BeTrue();
    zoom.Scale.Should().Be(0.75);
  }

  // A change costs the page a rebuild, so a step against a clamp must report none.
  [Fact]
  public void ZoomIn_StopsAtTheMaximumAndReportsNoChangeThere()
  {
    var zoom = new PageZoom();

    while (zoom.ZoomIn())
    {
    }

    zoom.Scale.Should().Be(2.5);
    zoom.ZoomIn().Should().BeFalse();
  }

  [Fact]
  public void ZoomOut_StopsAtTheMinimumAndReportsNoChangeThere()
  {
    var zoom = new PageZoom();

    while (zoom.ZoomOut())
    {
    }

    zoom.Scale.Should().Be(0.4);
    zoom.ZoomOut().Should().BeFalse();
  }

  [Fact]
  public void Reset_ReturnsToOneAndReportsNoChangeWhenAlreadyThere()
  {
    var zoom = new PageZoom();
    zoom.ZoomIn();

    zoom.Reset().Should().BeTrue();
    zoom.Scale.Should().Be(1.0);
    zoom.Reset().Should().BeFalse();
  }

  // A pinch reports a stream of deltas; only the first in each interval takes a step, whatever its size.
  [Fact]
  public void Pinch_TakesOneStepInTheDeltasDirectionAndIgnoresTheNextUntilTheIntervalPasses()
  {
    var zoom = new PageZoom();

    zoom.Pinch(-0.01).Should().BeTrue();
    zoom.Scale.Should().Be(0.75);
    zoom.Pinch(-5).Should().BeFalse();
    zoom.Scale.Should().Be(0.75);
  }

  [Fact]
  public void Pinch_StepsAgainOnceTheIntervalHasPassed()
  {
    var zoom = new PageZoom();
    zoom.Pinch(1);

    Thread.Sleep(350);

    zoom.Pinch(1).Should().BeTrue();
    zoom.Scale.Should().Be(1.5);
  }
}
