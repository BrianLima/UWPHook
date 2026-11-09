using Xunit;

// These tests start real processes and use real temp folders,
// so they run one at a time to keep them from seeing each other's files.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
