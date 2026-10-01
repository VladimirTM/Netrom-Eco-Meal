namespace NetromEcoMeal.Tests.Api;

// Program.cs sets Serilog's process-global Log.Logger on every host startup (the bootstrap-logger
// pattern) — two Api hosts booting concurrently in the same test process (xUnit runs different
// test classes in parallel by default) race that static assignment and can intermittently fail
// DI validation. Every test class that boots an ApiFactory belongs in this collection so xUnit
// runs them sequentially relative to each other, while still running in parallel with the rest of
// the (non-Api) test suite.
[CollectionDefinition(nameof(ApiCollection), DisableParallelization = true)]
public class ApiCollection;
