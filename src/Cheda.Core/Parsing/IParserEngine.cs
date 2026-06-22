using Cheda.Core.Models;

namespace Cheda.Core.Parsing;

public interface IParserEngine
{
    void Register(ISourceParser parser);
    ParseResult Parse(string sender, string body, DateTimeOffset timestamp);
    IReadOnlyList<Transaction> ParseBatch(IEnumerable<(string sender, string body, DateTimeOffset timestamp)> messages);
}
