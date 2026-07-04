using System.Reflection;

namespace GT4.UI.Utils;

public static class ViewUtils
{
  public static void RefreshView<TDeclaring>(this TDeclaring element)
    where TDeclaring : Microsoft.Maui.Controls.BindableObject
  {
    var declaringType = typeof(TDeclaring);
    var onPropertyChanged = TryGetMethod(declaringType, "OnPropertyChanged", [typeof(string)]);
    if (onPropertyChanged is null)
    {
      return;
    }

    var propertyNames = declaringType
      .GetProperties()
      .Where(p => p.GetGetMethod()?.IsPublic == true && p.GetGetMethod(false)?.DeclaringType == declaringType)
      .Select(p => p.Name);
    foreach (var propertyName in propertyNames)
    {
      onPropertyChanged.Invoke(element, [propertyName]);
    }
  }

  private static MethodInfo? TryGetMethod(Type type, string methodName, Type[] argumentTypes)
  {
    var specs =
      BindingFlags.Public
      | BindingFlags.NonPublic
      | BindingFlags.Instance
      | BindingFlags.DeclaredOnly;

    var methodInfo = type.GetMethod(methodName, specs, argumentTypes);
    if (methodInfo is not null)
    {
      return methodInfo;
    }

    if (type.BaseType is null)
    {
      return null;
    }

    return TryGetMethod(type.BaseType, methodName, argumentTypes);
  }
}
