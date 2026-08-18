namespace GT4.UI.Utils.Formatters.Detailed;

/// <summary>
/// Raised when a table row carries gendered terms but no unknown-sex one, meaning the language has
/// no single word for the relationship and the label has to be built from both gendered forms.
/// </summary>
internal sealed class NeutralTermMissingException : Exception
{
}
