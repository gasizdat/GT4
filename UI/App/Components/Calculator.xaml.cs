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

  public static readonly BindableProperty ThicknessResultProperty = BindableProperty.Create(
      nameof(ThicknessResult),
      typeof(Thickness),
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

  public object? X1
  {
    get => GetValue(X1Property);
    set => SetValue(X1Property, value);
  }

  public object? X2
  {
    get => GetValue(X2Property);
    set => SetValue(X2Property, value);
  }

  public object? X3
  {
    get => GetValue(X3Property);
    set => SetValue(X3Property, value);
  }

  public object? X4
  {
    get => GetValue(X4Property);
    set => SetValue(X4Property, value);
  }

  public object? X5
  {
    get => GetValue(X5Property);
    set => SetValue(X5Property, value);
  }

  public object? Result
  {
    get => GetValue(ResultProperty);
    set => SetValue(ResultProperty, value);
  }

  public Thickness? ThicknessResult
  {
    get => (Thickness?)GetValue(ThicknessResultProperty);
    set => SetValue(ThicknessResultProperty, value);
  }

  public string? Expression
  {
    get => (string?)GetValue(ExpressionProperty);
    set => SetValue(ExpressionProperty, value);
  }

  private double? EvaluateExpression(string expression)
  {
    try
    {
      const StringComparison ReplaceComparison = StringComparison.InvariantCulture;
      var table = new DataTable { Locale = CultureInfo.InvariantCulture };
      expression = expression.Replace(nameof(X1), ToStringInvariant(X1), ReplaceComparison)
                                 .Replace(nameof(X2), ToStringInvariant(X2), ReplaceComparison)
                                 .Replace(nameof(X3), ToStringInvariant(X3), ReplaceComparison)
                                 .Replace(nameof(X4), ToStringInvariant(X4), ReplaceComparison)
                                 .Replace(nameof(X5), ToStringInvariant(X5), ReplaceComparison);

      var resultObj = table.Compute(expression, null);
      return Convert.ToDouble(resultObj, table.Locale);
    }
    catch
    {
      return null;
    }
  }

  private Thickness? EvaluateThicknessExpression(string expression)
  {
    const string Left = "Left:";
    const string Top = "Top:";
    const string Right = "Right:";
    const string Bottom = "Bottom:";
    double? left = null;
    double? top = null;
    double? right = null;
    double? bottom = null;
    foreach (var expr in expression.Split(';'))
    {
      switch (expr)
      {
        case string when expr.StartsWith(Left):
          left = EvaluateExpression(expr.Substring(Left.Length));
          break;
        case string when expr.StartsWith(Top):
          top = EvaluateExpression(expr.Substring(Top.Length));
          break;
        case string when expr.StartsWith(Right):
          right = EvaluateExpression(expr.Substring(Right.Length));
          break;
        case string when expr.StartsWith(Bottom):
          bottom = EvaluateExpression(expr.Substring(Bottom.Length));
          break;
      }

    }
    return new Thickness(left: left ?? 0, top: top ?? 0, right: right ?? 0, bottom: bottom ?? 0);
  }

  private void Evaluate()
  {
    const string ResultExpression = "Result=";
    const string ThicknessExpression = "Thickness=";
    switch (Expression?.Replace(" ", string.Empty))
    {
      case string expression when expression.StartsWith(ResultExpression):
        Result = EvaluateExpression(expression.Substring(ResultExpression.Length));
        break;
      case string expression when expression.StartsWith(ThicknessExpression):
        ThicknessResult = EvaluateThicknessExpression(expression.Substring(ThicknessExpression.Length));
        break;
      case string expression:
        Result = expression is null ? null : EvaluateExpression(expression);
        break;
    }
  }

  private static string? ToStringInvariant(object? value)
  {
    return Convert.ToString(value, CultureInfo.InvariantCulture);
  }

  private static void OnArgChanged(BindableObject bindable, object oldValue, object newValue)
  {
    if (bindable is Calculator calculator && oldValue != newValue)
    {
      calculator.Evaluate();
    }
  }
}