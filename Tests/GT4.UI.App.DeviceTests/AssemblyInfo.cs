using Xunit;

// Tests share a single UI thread (MainThread) and mutate Application.Current.Resources once;
// parallel test cases would race both.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
