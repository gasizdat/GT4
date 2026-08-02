namespace GT4.UI.Abstraction;

public interface IAlertService
{
  Task<bool> ShowConfirmationAsync(string confirmationText);
  Task ShowErrorAsync(Exception exception);
  Task ShowWarningAsync(string message);
}
