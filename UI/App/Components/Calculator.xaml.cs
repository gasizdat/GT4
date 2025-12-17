using System.Data;
using System.Globalization;
namespace GT4.UI.Components;

public partial class Calculator : ContentView
{
  public Calculator()
  {
    InitializeComponent();
  }

  private static BindableProperty CreateArg(int argNo)
  {
    return BindableProperty.Create(
      $"X{argNo}",
      typeof(double),
      typeof(Calculator),
      default,
      BindingMode.OneWay,
      null,
      OnArgChanged);
  }

  public static readonly BindableProperty X1Property = CreateArg(1);

  public static readonly BindableProperty X2Property = CreateArg(2);

  public static readonly BindableProperty X3Property = CreateArg(3);

  public static readonly BindableProperty X4Property = CreateArg(4);

  public static readonly BindableProperty X5Property = CreateArg(5);

  public static readonly BindableProperty ResultProperty = BindableProperty.Create(
      nameof(Result),
      typeof(double),
      typeof(Calculator),
      default,
      BindingMode.OneWay,
      null,
      OnArgChanged);

  public static readonly BindableProperty ExpressionProperty = BindableProperty.Create(
      nameof(Expression),
      typeof(string),
      typeof(Calculator),
      default,
      BindingMode.OneWay,
      null,
      OnArgChanged);

  public double? X1
  {
    get => (double?)GetValue(X1Property);
    set => SetValue(X1Property, value);
  }

  public double? X2
  {
    get => (double?)GetValue(X2Property);
    set => SetValue(X2Property, value);
  }

  public double? X3
  {
    get => (double?)GetValue(X3Property);
    set => SetValue(X3Property, value);
  }

  public double? X4
  {
    get => (double?)GetValue(X4Property);
    set => SetValue(X4Property, value);
  }

  public double? X5
  {
    get => (double?)GetValue(X5Property);
    set => SetValue(X5Property, value);
  }

  public double? Result
  {
    get => (double?)GetValue(ResultProperty);
    set => SetValue(ResultProperty, value);
  }

  public string? Expression
  {
    get => (string?)GetValue(ExpressionProperty);
    set => SetValue(ExpressionProperty, value);
  }

  private double EvaluateExpression()
  {
    try
    {
      const StringComparison ReplaceComparison = StringComparison.InvariantCulture;
      var table = new DataTable { Locale = CultureInfo.InvariantCulture };
      var expression = Expression?.Replace(nameof(X1), ToStringInvariant(X1), ReplaceComparison)
                                 .Replace(nameof(X2), ToStringInvariant(X2), ReplaceComparison)
                                 .Replace(nameof(X3), ToStringInvariant(X3), ReplaceComparison)
                                 .Replace(nameof(X4), ToStringInvariant(X4), ReplaceComparison)
                                 .Replace(nameof(X5), ToStringInvariant(X5), ReplaceComparison);

      var resultObj = table.Compute(expression, null);
      return Convert.ToDouble(resultObj, table.Locale);
    }
    catch
    {
      return double.NaN;
    }
  }

  private static string? ToStringInvariant(double? value)
  {
    return Convert.ToString(value, CultureInfo.InvariantCulture);
  }

  private static void OnArgChanged(BindableObject bindable, object oldValue, object newValue)
  {
    if (bindable is Calculator calculator && oldValue != newValue)
    {
      calculator.Result = calculator.EvaluateExpression();
    }
  }
}