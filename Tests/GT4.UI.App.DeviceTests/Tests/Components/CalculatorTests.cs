using GT4.UI.Components;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers Calculator's own expression evaluation directly: it's a plain ContentView with no service
/// dependencies, constructed straight from the parameterless ctor. XN/Result/ThicknessResult/
/// Expression are all wired to the same OnArgChanged callback, which re-evaluates Expression against
/// the current XN values -- the substitution, DataTable.Compute cascade, and the Result-vs-Thickness
/// routing are the non-obvious logic worth pinning here.
/// </summary>
public class CalculatorTests
{
  private static async Task<Calculator> CreateCalculatorAsync()
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    return await MainThread.InvokeOnMainThreadAsync(() => new Calculator());
  }

  [Fact]
  public async Task Result_prefixed_expression_evaluates_using_the_current_arg_values()
  {
    var calculator = await CreateCalculatorAsync();

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      calculator.X1 = 2.0;
      calculator.X2 = 3.0;
      calculator.Expression = "Result=X1+X2";
    });

    Assert.Equal(5.0, calculator.Result);
  }

  [Fact]
  public async Task An_unprefixed_expression_also_evaluates_into_Result()
  {
    var calculator = await CreateCalculatorAsync();

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      calculator.X1 = 4.0;
      calculator.X2 = 5.0;
      calculator.Expression = "X1*X2";
    });

    Assert.Equal(20.0, calculator.Result);
  }

  [Fact]
  public async Task Spaces_in_the_expression_are_stripped_before_evaluation()
  {
    var calculator = await CreateCalculatorAsync();

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      calculator.X1 = 1.5;
      calculator.X2 = 2.5;
      calculator.Expression = "Result= X1 + X2 ";
    });

    Assert.Equal(4.0, calculator.Result);
  }

  [Fact]
  public async Task Changing_an_arg_after_the_expression_is_set_recomputes_Result()
  {
    var calculator = await CreateCalculatorAsync();
    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      calculator.X1 = 1.0;
      calculator.X2 = 1.0;
      calculator.Expression = "Result=X1+X2";
    });
    Assert.Equal(2.0, calculator.Result);

    await MainThread.InvokeOnMainThreadAsync(() => calculator.X1 = 10.0);

    Assert.Equal(11.0, calculator.Result);
  }

  [Fact]
  public async Task An_invalid_expression_leaves_Result_unchanged_and_does_not_throw()
  {
    var calculator = await CreateCalculatorAsync();
    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      calculator.X1 = 2.0;
      calculator.X2 = 3.0;
      calculator.Expression = "Result=X1+X2";
    });
    Assert.Equal(5.0, calculator.Result);

    var exception = await MainThread.InvokeOnMainThreadAsync(() => Record.Exception(() =>
      calculator.Expression = "Result=not an expression"));

    // EvaluateExpression's catch-all yields a null double?, but ResultProperty is declared as
    // typeof(double) -- a non-nullable value type -- so MAUI's BindableProperty rejects the null as
    // an invalid value and silently skips the SetValue call, leaving the last good Result in place.
    Assert.Null(exception);
    Assert.Equal(5.0, calculator.Result);
  }

  [Fact]
  public async Task Thickness_prefixed_expression_evaluates_each_side_independently()
  {
    var calculator = await CreateCalculatorAsync();

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      calculator.X1 = 1.0;
      calculator.X2 = 2.0;
      calculator.X3 = 3.0;
      calculator.X4 = 4.0;
      calculator.Expression = "Thickness=Left:X1;Top:X2;Right:X3;Bottom:X4";
    });

    Assert.Equal(new Thickness(left: 1, top: 2, right: 3, bottom: 4), calculator.ThicknessResult);
  }

  [Fact]
  public async Task Thickness_sides_missing_from_the_expression_default_to_zero()
  {
    var calculator = await CreateCalculatorAsync();

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      calculator.X1 = 5.0;
      calculator.X2 = 6.0;
      calculator.Expression = "Thickness=Left:X1;Bottom:X2";
    });

    Assert.Equal(new Thickness(left: 5, top: 0, right: 0, bottom: 6), calculator.ThicknessResult);
  }
}
