using Elasticsearch.Net;
using Nest;

namespace Surging.Core.ElasticSearch.Provider
{
    public interface IElasticSearchProvider
    {
        //ElasticLowLevelClient GetElasticLowLevelClient();

        ElasticClient GetElasticClient();
    }
}
