using FluentAssertions;
using GT4.Core.Project.Dto;
using GT4.UI.Utils.Dto;
using Xunit;

namespace GT4.UI.Utils.Tests;

public class ImageDataWithMaxSizeTests
{
  [Fact]
  public void A_copy_replacing_the_content_still_asks_for_a_thumbnail()
  {
    // PhotoTagDataConverter unwraps a tagged photo's residue envelope as "data with { Content = ... }"
    // before handing it on to ImageDataConverter, and the photo it is given may already be wrapped as a
    // thumbnail request -- the family tree and the names list both wrap theirs. Were the copy to come
    // back a plain Data, the max size would be lost and those screens would silently decode every
    // portrait at full resolution.
    var photo = new Data(7, [1, 2, 3], "image/jpeg", DataCategory.PersonMainPhotoTagged);
    Data thumbnail = new ImageDataWithMaxSize(photo, 100);

    var unwrapped = thumbnail with { Content = new byte[] { 4, 5, 6 } };

    var kept = unwrapped.Should().BeOfType<ImageDataWithMaxSize>().Which;
    kept.MaxSize.Should().Be(100);
    kept.Content.Should().Equal((byte)4, (byte)5, (byte)6);
    kept.Category.Should().Be(DataCategory.PersonMainPhotoTagged);
  }
}
