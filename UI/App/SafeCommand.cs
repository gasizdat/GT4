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

  public SafeCommand(Action<T> execute)
    : base(GetSafeAction(execute))
  {
    if (execute is null)
    {
      throw new ArgumentNullException(nameof(execute));
    }
  }

  public SafeCommand(Action<T> execute, Func<T, bool> canExecute)
    : base(GetSafeAction(execute), GetSafeCanExecuteAction(canExecute))
  {
    if (execute is null)
      throw new ArgumentNullException(nameof(execute));
    if (canExecute is null)
      throw new ArgumentNullException(nameof(canExecute));
  }

  public SafeCommand(Func<T, Task> execute)
    : base(GetSafeAction(execute))
  {
    if (execute is null)
    {
      throw new ArgumentNullException(nameof(execute));
    }
  }

  public SafeCommand(Func<T, Task> execute, Func<T, bool> canExecute)
    : base(GetSafeAction(execute), GetSafeCanExecuteAction(canExecute))
  {
    if (execute is null)
      throw new ArgumentNullException(nameof(execute));
    if (canExecute is null)
      throw new ArgumentNullException(nameof(canExecute));
  }
}

public class SafeCommand : Command
{
  private static Action GetSafeAction(Action action)
  {
    return () =>
    {
      try
      {
        action();
      }
      catch (Exception ex)
      {
        _ = PageAlert.ShowErrorAsync(ex);
      }
    };
  }

  private static Action<object> GetSafeAction(Action<object> action)
  {
    return (object obj) =>
    {
      try
      {
        action(obj);
      }
      catch (Exception ex)
      {
        _ = PageAlert.ShowErrorAsync(ex);
      }
    };
  }

  private static Action GetSafeAction(Func<Task> actionAsync)
  {
    return async () =>
    {
      try
      {
        await actionAsync().ConfigureAwait(true);
      }
      catch (Exception ex)
      {
        await PageAlert.ShowErrorAsync(ex);
      }
    };
  }

  private static Action<object> GetSafeAction(Func<object, Task> actionAsync)
  {
    return async (object obj) =>
    {
      try
      {
        await actionAsync(obj).ConfigureAwait(true);
      }
      catch (Exception ex)
      {
        await PageAlert.ShowErrorAsync(ex);
      }
    };
  }

  private static Func<object, bool> GetSafeCanExecuteAction(Func<object, bool> canExecute)
  {
    return (object obj) =>
    {
      try
      {
        return canExecute(obj);
      }
      catch (Exception ex)
      {
        _ = PageAlert.ShowErrorAsync(ex);
      }

      return false;
    };
  }

  public SafeCommand(Action execute)
  : base(GetSafeAction(execute))
  {
    if (execute is null)
    {
      throw new ArgumentNullException(nameof(execute));
    }
  }

  public SafeCommand(Action<object> execute)
    : base(GetSafeAction(execute))
  {
    if (execute is null)
    {
      throw new ArgumentNullException(nameof(execute));
    }
  }

  public SafeCommand(Action<object> execute, Func<object, bool> canExecute)
    : base(GetSafeAction(execute), GetSafeCanExecuteAction(canExecute))
  {
    if (execute is null)
      throw new ArgumentNullException(nameof(execute));
    if (canExecute is null)
      throw new ArgumentNullException(nameof(canExecute));
  }

  public SafeCommand(Func<Task> executeAsync)
      : base(GetSafeAction(executeAsync))
  {
    ArgumentNullException.ThrowIfNull(executeAsync);
  }

  public SafeCommand(Func<object, Task> executeAsync)
      : base(GetSafeAction(executeAsync))
  {
    ArgumentNullException.ThrowIfNull(executeAsync);
  }

  public SafeCommand(Func<object, Task> executeAsync, Func<object, bool> canExecute)
      : base(GetSafeAction(executeAsync), GetSafeCanExecuteAction(canExecute))
  {
    ArgumentNullException.ThrowIfNull(executeAsync);
    ArgumentNullException.ThrowIfNull(canExecute);
  }
}