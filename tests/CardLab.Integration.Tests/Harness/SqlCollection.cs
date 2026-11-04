namespace CardLab.Integration.Tests.Harness;

/// <summary>
/// All integration test classes join this collection (<c>[Collection("sql")]</c>) so they run one class at a time
/// against the shared SQL Server instance (each class still gets its own database). Complete in PR-00.
/// </summary>
[CollectionDefinition("sql")]
public sealed class SqlCollection
{
}
