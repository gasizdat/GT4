using System.Reflection;

namespace GT4.UI.Utils.Extensions;

public static class ViewUtils
{
  public static void RefreshView<TDeclaring>(this TDeclaring element)
    where TDeclaring : Microsoft.Maui.Controls.BindableObject
  {
    // Filter by the compile-time generic argument, not element.GetType(): a Testable* test subclass
    // narrowed to its base type via an `is BaseType view` pattern (e.g. RelativeInfoView's own
    // OnRelativeInfoChanged) must still notify the base class's own properties. Using GetType() here
    // would resolve to the subclass at runtime and filter every base property out, since none of them
    // are declared directly on the subclass.
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
