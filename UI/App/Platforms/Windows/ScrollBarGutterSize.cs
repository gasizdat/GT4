namespace GT4.UI;

// ScrollBarSize is WinUI's own (undocumented) theme resource for the scrollbar's fully expanded
// width; Fallback only covers that key being renamed/removed by a future WinUI version. Shared by
// CollectionViewScrollBarGutter and ScrollViewScrollBarGutter -- both need the same OS-reported size.
static class ScrollBarGutterSize
{
  const double Fallback = 12;

  public static double Resolve() =>
      Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue("ScrollBarSize", out var size) && size is double d ? d : Fallback;
}
