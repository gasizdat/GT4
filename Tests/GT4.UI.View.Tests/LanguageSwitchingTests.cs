using Xunit;

namespace GT4.UI.View.Tests;

// Language.Current writes CultureInfo.DefaultThreadCurrentUICulture, which is process-global: it
// changes what every other thread resolves from UIStrings, even between two statements of a test
// already in flight. Tests that just need a language use TestLanguage instead, so this collection
// holds only the one class that has to exercise the production setter itself.
[CollectionDefinition(DisableParallelization = true)]
public sealed class LanguageSwitchingTests;
