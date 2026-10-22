using Xunit;

// Disable parallel test execution across classes so performance benchmark tests
// are not subject to CPU thread-contention or GC interference from other tests.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
