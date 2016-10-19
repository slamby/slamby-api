using Slamby.Elastic.Queries;

namespace Slamby.Elastic.Factories.Interfaces
{
    public interface IQueryFactory
    {
        AnalyzeQuery GetAnalyzeQuery(string name);
        IDocumentQuery GetDocumentQuery(string name);
        IDocumentQuery GetDocumentQuery();
        IndexQuery GetIndexQuery(string name);
        IndexQuery GetIndexQuery();
        OptimizeQuery GetOptimizeQuery(string name);
        TagQuery GetTagQuery(string name);
        WordQuery GetWordQuery(string name);
    }
}