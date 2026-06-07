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

    /// <summary>
    /// Asserts that the collection contains exactly one element.
    /// The <paramref name="userMessage"/> is shown on failure instead of the default message.
    ///
    /// This overload mirrors the xUnit v3 signature
    /// <c>Assert.Single&lt;T&gt;(IEnumerable&lt;T&gt;, string)</c>
    /// and is provided here for xUnit 2.x compatibility.
    /// </summary>
    public static T Single<T>(IEnumerable<T> collection, string userMessage)
    {
        NotNull(collection);

        List<T> items = collection.ToList();

        if (items.Count == 1)
            return items[0];

        throw new Xunit.Sdk.XunitException(
            $"{userMessage} — expected exactly 1 item, found {items.Count}.");
    }
}
