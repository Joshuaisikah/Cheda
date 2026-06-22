using Cheda.Core.Models;

namespace Cheda.Core.Parsing;

public sealed class ParserEngine : IParserEngine
{
    private readonly List<ISourceParser> _parsers = [];

    public void Register(ISourceParser parser) => _parsers.Add(parser);

    public ParseResult Parse(string sender, string body, DateTimeOffset timestamp)
    {
        foreach (var parser in _parsers)
        {
            if (parser.CanHandle(sender, body))
                return parser.Parse(sender, body, timestamp);
        }
        return ParseResult.Fail();
    }

    public IReadOnlyList<Transaction> ParseBatch(
        IEnumerable<(string sender, string body, DateTimeOffset timestamp)> messages)
    {
        var results = new List<Transaction>();
        foreach (var (sender, body, timestamp) in messages)
        {
            var result = Parse(sender, body, timestamp);
            if (result.Success && result.Transaction is not null)
                results.Add(result.Transaction);
        }
        return results;
    }
}
