using System.Reflection;
using Windows.UI.ViewManagement;

namespace GT4.UI;

public static class Utils
{
  public static void RefreshView(Microsoft.Maui.Controls.BindableObject element)
  {
    var elementType = element.GetType();
    var onPropertyChanged = TryGetMethod(elementType, "OnPropertyChanged", [typeof(string)]);
    if (onPropertyChanged is null)
    {
      return;
    }

    var propertyNames = elementType
      .GetProperties()
      .Where(p => p.GetGetMethod()?.IsPublic == true && p.GetGetMethod(false)?.DeclaringType == elementType)
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
