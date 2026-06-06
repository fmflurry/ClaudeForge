namespace Xunit;

/// <summary>
/// Partial class extension for <see cref="Assert"/> that adds the
/// <c>Contains(IEnumerable&lt;T&gt;, Predicate&lt;T&gt;, string)</c> overload.
///
/// xUnit 2.x does not ship this 3-argument form (the third <paramref name="userMessage"/>
/// was added in xUnit 3.x). This partial extension makes the test project compile under
/// xUnit 2.9.x while the test file uses the newer overload signature.
/// </summary>
public partial class Assert
{
    /// <summary>
    /// Asserts that a collection contains at least one element that satisfies <paramref name="filter"/>.
    /// Fails with <paramref name="userMessage"/> when no element matches.
    /// </summary>
    public static void Contains<T>(
        IEnumerable<T> collection,
        Predicate<T> filter,
        string userMessage)
    {
        NotNull(collection);
        NotNull(filter);

        foreach (T item in collection)
        {
            if (filter(item))
                return;
        }

        throw Xunit.Sdk.ContainsException.ForCollectionFilterNotMatched(userMessage);
    }
}
