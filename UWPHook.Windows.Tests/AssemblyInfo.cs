using Xunit;

// These tests start real processes and look for leftover temp scripts,
// so they run one at a time to keep those checks from seeing each other's files.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
