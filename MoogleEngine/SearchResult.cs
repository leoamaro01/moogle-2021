namespace MoogleEngine;

public class SearchResult
{
    private readonly SearchItem[] items;

    public SearchResult(SearchItem[] items, string suggestion = "")
    {
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.Suggestion = suggestion;
    }

    public SearchResult() : this(Array.Empty<SearchItem>())
    {

    }

    public string Suggestion { get; private set; }

    public IEnumerable<SearchItem> Items()
    {
        return this.items;
    }

    public int Count { get { return this.items.Length; } }
}
