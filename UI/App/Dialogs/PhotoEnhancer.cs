using GT4.Core.Project.Dto;
using GT4.Core.Project.Extensions;
using GT4.UI.Abstraction;
using GT4.UI.Items;
using GT4.UI.Resources;
using GT4.UI.Utils.Converters;
using GT4.UI.Utils.Extensions;

namespace GT4.UI.Dialogs;

public class PhotoEnhancer
{
  private readonly IEnumerable<IPhotoEnhancement> _Enhancements;
  private readonly IModelDownloader _ModelDownloader;
  private readonly IAlertService _AlertService;
  private readonly DataConverterResolver _DataConverterResolver;

  public PhotoEnhancer(
    IEnumerable<IPhotoEnhancement> enhancements,
    IModelDownloader modelDownloader,
    IAlertService alertService,
    DataConverterResolver dataConverterResolver)
  {
    _Enhancements = enhancements;
    _ModelDownloader = modelDownloader;
    _AlertService = alertService;
    _DataConverterResolver = dataConverterResolver;
  }

  public bool CanEnhance => _Enhancements.Any();

  public async Task<Data?> EnhanceAsync(Page page, PersonDataItem photo)
  {
    var labels = _Enhancements.Select(enhancement => enhancement.MenuLabel).ToArray();
    var choice = await ChooseEnhancementAsync(page, labels);
    var enhancement = _Enhancements.FirstOrDefault(enhancement => enhancement.MenuLabel == choice);
    if (enhancement is null || !await DownloadMissingModelsAsync(page.Navigation, enhancement))
    {
      return null;
    }

    var converter = _DataConverterResolver(photo.Info.Category.AsTaggedPhoto());
    var dialog = new ProgressDialog(enhancement.DisplayName, enhancement.DisplayName, UIStrings.HintPhotoEnhancementInProgress, _AlertService);
    await page.Navigation.PushModalAsync(dialog);
    try
    {
      // An unsaved caption edit carries over to the copy.
      var current = await photo.ToDataAsync();
      var source = await converter.ToObjectAsync(current, dialog.Token);
      var enhanced = await Task.Run(() => enhancement.EnhanceAsync((PhotoInfo)source!, dialog.Token));
      // A new photo, so it must not carry the source's stored Id.
      var data = await converter.FromObjectAsync(enhanced, dialog.Token);
      // The copy's caption mark makes it Tagged, but person or family is the source's, not the converter's.
      var category = photo.Info.Category.AsAdditionalPhoto().AsTaggedPhoto();
      // The converter labels everything it encodes image/bmp; unlabelled, the JPEG bytes are sniffed instead.
      return data! with { MimeType = null, Category = category };
    }
    catch (OperationCanceledException)
    {
      return null;
    }
    finally
    {
      await page.Navigation.PopModalAsync();
    }
  }

  // Virtual only so a device test can answer the sheet, which a test cannot drive.
  protected virtual Task<string> ChooseEnhancementAsync(Page page, string[] names) =>
    page.DisplayActionSheetAsync(UIStrings.TitleEnhancePhoto, UIStrings.BtnNameCancel, null, names);

  private async Task<bool> DownloadMissingModelsAsync(INavigation navigation, IPhotoEnhancement enhancement)
  {
    const double BytesPerMegabyte = 1024 * 1024;

    var missing = enhancement.Models
      .Where(model => !_ModelDownloader.IsDownloaded(model))
      .ToArray();
    if (missing.Length == 0)
    {
      return true;
    }

    var entries = missing.Select(model =>
      string.Format(UIStrings.ModelDownloadEntry_3, model.DisplayName, model.License, model.Size / BytesPerMegabyte));
    var list = string.Join(Environment.NewLine, entries);
    var confirmationText = string.Format(UIStrings.AlertDownloadModelsConfirm_2, enhancement.DisplayName, list);
    if (!await _AlertService.ShowConfirmationAsync(confirmationText))
    {
      return false;
    }

    var names = missing.Select(model => model.DisplayName);
    var subject = string.Join(", ", names);
    var dialog = new ProgressDialog(enhancement.DisplayName, subject, UIStrings.HintModelDownloadInProgress, _AlertService);
    await navigation.PushModalAsync(dialog);
    try
    {
      foreach (var model in missing)
      {
        await _ModelDownloader.EnsureModelAsync(model, dialog.Token);
      }
      return true;
    }
    catch (OperationCanceledException)
    {
      return false;
    }
    finally
    {
      await navigation.PopModalAsync();
    }
  }
}
