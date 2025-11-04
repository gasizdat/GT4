using GT4.Core.Project;
using GT4.Core.Utils;
using GT4.UI.App.Dialogs;
using GT4.UI.App.Items;
using GT4.UI.Resources;


namespace GT4.UI.App.Pages;

public partial class OpenOrCreateDialog : ContentPage
{
  public OpenOrCreateDialog()
  {
    InitializeComponent();
    BindingContext = this;
  }

#if ANDROID

  public async Task RequestFileAccessPermissionsAsync()
  {
    if (!await RequestStoragePermissionsAsync())
    {
      return;
    }

    string? documentFolder = Android.OS.Environment.DirectoryDocuments;
    string? path = Android.OS.Environment.GetExternalStoragePublicDirectory(documentFolder)?.AbsolutePath;
    if (path is not null)
    {
      string filePath = Path.Combine(path, "GT4", "myfile.txt");
      Directory.CreateDirectory(Path.GetDirectoryName(filePath));
      File.WriteAllText(filePath, "test");
    }
  }

  public async void OnLoaded(object sender, EventArgs e)
  {
    await RequestFileAccessPermissionsAsync();
  }

  public async Task<bool> RequestStoragePermissionsAsync()
  {
    var statusRead = await Permissions.CheckStatusAsync<Permissions.StorageRead>();
    var statusWrite = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();

    if (statusRead != PermissionStatus.Granted)
    {
      statusRead = await Permissions.RequestAsync<Permissions.StorageRead>();
    }

    if (statusWrite != PermissionStatus.Granted)
    {
      statusWrite = await Permissions.RequestAsync<Permissions.StorageWrite>();
    }

    return statusRead == PermissionStatus.Granted && statusWrite == PermissionStatus.Granted;
  }
#else
  public void RequestFileAccessPermissions()
  {
  }
#endif


  public ServiceProvider Services { get; set; } = ServiceBuilder.DefaultServices;

  public ICollection<ProjectItem> Projects
  {
    get
    {
      using var token = Services.GetRequiredService<ICancellationTokenProvider>().CreateDbCancellationToken();
      var ret = Services.GetRequiredService<IProjectList>()
        .GetItemsAsync(token)
        .Result
        .Select(projectInfo => new ProjectItem(projectInfo))
        .OrderBy(item => item, Services.GetRequiredService<IComparer<ProjectItem>>())
        .ToList();

      ret.Add(new ProjectItemCreate());

      return ret;
    }
  }

  public async void OnProjectSelected(object sender, SelectionChangedEventArgs e)
  {
    switch (e.CurrentSelection.FirstOrDefault())
    {
      case ProjectItemCreate:
        await OnCreateProject();
        break;

      case ProjectItem projectItem:
        {
          using var token = Services.GetRequiredService<ICancellationTokenProvider>().CreateDbCancellationToken();
          await Services.GetRequiredService<ICurrentProjectProvider>().OpenAsync(projectItem.Info, token);
          await Shell.Current.GoToAsync(UIRoutes.GetRoute<FamiliesPage>());

          // TODO not so good approach
          if (sender is SelectableItemsView view)
          {
            view.SelectedItem = null;
          }
          break;
        }
    }
  }

  public async void OnSaveProjectSelected(object sender, EventArgs e)
  {
    var fileTypes =
new Dictionary<DevicePlatform, IEnumerable<string>>
{
    { DevicePlatform.iOS, new[] { "public.image" } }, // UTType
    { DevicePlatform.Android, new[] { "image/*","*", ".gt4" , ".jpg", ".png"} },   // MIME type
    { DevicePlatform.WinUI, new[] { ".jpg", ".png" } }, // File extensions
    { DevicePlatform.macOS, new[] { "jpg", "png" } },
};
    var result = await FilePicker.Default.PickAsync(new PickOptions
    {
      PickerTitle = "Please select a file",
      FileTypes = new FilePickerFileType(fileTypes)
    });

  }

  public async void OnDeleteProjectSelected(object sender, EventArgs e)
  {
    var item = (sender as BindableObject)?.BindingContext as ProjectItem;
    if (item is null or ProjectItemCreate)
      return;

    try
    {
      var result = await DisplayAlert(UIStrings.AlertTitleConfirmation,
        string.Format(UIStrings.AlertTextDeleteConfirmationText_1, item.Name), UIStrings.BtnNameYes, UIStrings.BtnNameNo);

      if (result == false)
        return;

      using var token = Services.GetRequiredService<ICancellationTokenProvider>().CreateDbCancellationToken();
      await Services.GetRequiredService<IProjectList>().RemoveAsync(item.Name, token);
    }
    catch (Exception ex)
    {
      await DisplayAlert(UIStrings.AlertTitleError, ex.Message, UIStrings.BtnNameOk);
    }
    finally
    {
      OnPropertyChanged(nameof(Projects));
    }
  }

  internal async Task OnCreateProject()
  {
    var dialog = new CreateNewProjectDialog();

    await Navigation.PushModalAsync(dialog);
    var projectInfo = await dialog.ProjectInfo;
    await Navigation.PopModalAsync();

    try
    {
      if (projectInfo.Name == string.Empty)
        return;

      using var token = Services.GetRequiredService<ICancellationTokenProvider>().CreateDbCancellationToken();
      await Services.GetRequiredService<IProjectList>().CreateAsync(projectInfo, token);
    }
    catch (Exception ex)
    {
      await DisplayAlert(UIStrings.AlertTitleError, ex.Message, UIStrings.BtnNameOk);
    }
    finally
    {
      OnPropertyChanged(nameof(Projects));
    }
  }
}