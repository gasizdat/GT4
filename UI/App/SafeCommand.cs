using System.Diagnostics.CodeAnalysis;

namespace GT4.UI;

public sealed class SafeCommand<T> : SafeCommand
{
  private static Action<object> GetSafeAction(Action<T> action)
  {
    return (object obj) =>
    {
      if (IsValidParameter(obj))
      {
        action((T)obj);
      }
    };
  }

  private static Func<object, Task> GetSafeAction(Func<T, Task> actionAsync)
  {
    return (object obj) =>
    {
      if (IsValidParameter(obj))
        return actionAsync((T)obj);

      return Task.CompletedTask;
    };
  }

  private static Func<object, bool> GetSafeCanExecuteAction(Func<T, bool> canExecute)
  {
    return (object obj) =>
    {
      if (IsValidParameter(obj))
      {
        return canExecute((T)obj);
      }

      return false;
    };
  }

  private static bool IsValidParameter([NotNullWhen(true)] object? o)
  {
    if (o != null)
    {
      // The parameter isn't null, so we don't have to worry whether null is a valid option
      return o is T;
    }

    var t = typeof(T);

    // The parameter is null. Is T Nullable?
    if (Nullable.GetUnderlyingType(t) != null)
    {
      return true;
    }

    // Not a Nullable, if it's a value type then null is not valid
    return !t.IsValueType;
  }

  public SafeCommand(Action<T> execute, IPageAlertService pageAlertService)
    : base(GetSafeAction(execute), pageAlertService)
  {
    if (execute is null)
    {
      throw new ArgumentNullException(nameof(execute));
    }
  }

  public SafeCommand(Action<T> execute, Func<T, bool> canExecute, IPageAlertService pageAlertService)
    : base(GetSafeAction(execute), GetSafeCanExecuteAction(canExecute), pageAlertService)
  {
    if (execute is null)
      throw new ArgumentNullException(nameof(execute));
    if (canExecute is null)
      throw new ArgumentNullException(nameof(canExecute));
  }

  public SafeCommand(Func<T, Task> execute, IPageAlertService pageAlertService)
    : base(GetSafeAction(execute), pageAlertService)
  {
    if (execute is null)
    {
      throw new ArgumentNullException(nameof(execute));
    }
  }

  public SafeCommand(Func<T, Task> execute, Func<T, bool> canExecute, IPageAlertService pageAlertService)
    : base(GetSafeAction(execute), GetSafeCanExecuteAction(canExecute), pageAlertService)
  {
    if (execute is null)
      throw new ArgumentNullException(nameof(execute));
    if (canExecute is null)
      throw new ArgumentNullException(nameof(canExecute));
  }
}

public class SafeCommand : Command
{
  private static Action GetSafeAction(Action action, IPageAlertService pageAlertService)
  {
    return () =>
    {
      try
      {
        action();
      }
      catch (Exception ex)
      {
        _ = pageAlertService.ShowErrorAsync(ex);
      }
    };
  }

  private static Action<object> GetSafeAction(Action<object> action, IPageAlertService pageAlertService)
  {
    return (object obj) =>
    {
      try
      {
        action(obj);
      }
      catch (Exception ex)
      {
        _ = pageAlertService.ShowErrorAsync(ex);
      }
    };
  }

  private static Action GetSafeAction(Func<Task> actionAsync, IPageAlertService pageAlertService)
  {
    return async () =>
    {
      try
      {
        await actionAsync().ConfigureAwait(true);
      }
      catch (Exception ex)
      {
        await pageAlertService.ShowErrorAsync(ex);
      }
    };
  }

  private static Action<object> GetSafeAction(Func<object, Task> actionAsync, IPageAlertService pageAlertService)
  {
    return async (object obj) =>
    {
      try
      {
        await actionAsync(obj).ConfigureAwait(true);
      }
      catch (Exception ex)
      {
        await pageAlertService.ShowErrorAsync(ex);
      }
    };
  }

  private static Func<object, bool> GetSafeCanExecuteAction(Func<object, bool> canExecute, IPageAlertService pageAlertService)
  {
    return (object obj) =>
    {
      try
      {
        return canExecute(obj);
      }
      catch (Exception ex)
      {
        _ = pageAlertService.ShowErrorAsync(ex);
      }

      return false;
    };
  }

  public SafeCommand(Action execute, IPageAlertService pageAlertService)
  : base(GetSafeAction(execute, pageAlertService))
  {
    if (execute is null)
    {
      throw new ArgumentNullException(nameof(execute));
    }
  }

  public SafeCommand(Action<object> execute, IPageAlertService pageAlertService)
    : base(GetSafeAction(execute, pageAlertService))
  {
    if (execute is null)
    {
      throw new ArgumentNullException(nameof(execute));
    }
  }

  public SafeCommand(Action<object> execute, Func<object, bool> canExecute, IPageAlertService pageAlertService)
    : base(GetSafeAction(execute, pageAlertService), GetSafeCanExecuteAction(canExecute, pageAlertService))
  {
    if (execute is null)
      throw new ArgumentNullException(nameof(execute));
    if (canExecute is null)
      throw new ArgumentNullException(nameof(canExecute));
  }

  public SafeCommand(Func<Task> executeAsync, IPageAlertService pageAlertService)
      : base(GetSafeAction(executeAsync, pageAlertService))
  {
    ArgumentNullException.ThrowIfNull(executeAsync);
  }

  public SafeCommand(Func<object, Task> executeAsync, IPageAlertService pageAlertService)
      : base(GetSafeAction(executeAsync, pageAlertService))
  {
    ArgumentNullException.ThrowIfNull(executeAsync);
  }

  public SafeCommand(Func<object, Task> executeAsync, Func<object, bool> canExecute, IPageAlertService pageAlertService)
      : base(GetSafeAction(executeAsync, pageAlertService), GetSafeCanExecuteAction(canExecute, pageAlertService))
  {
    ArgumentNullException.ThrowIfNull(executeAsync);
    ArgumentNullException.ThrowIfNull(canExecute);
  }
}
