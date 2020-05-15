using Nest;
using Surging.Core.CPlatform.Utilities;
using System;

namespace Surging.Core.ElasticSearch.Provider
{
    public class ElasticSearchProvider : IElasticSearchProvider
    {
        private readonly ElasticSearchSetting _elasticSearchSetting;

        public ElasticSearchProvider()
        {
            _elasticSearchSetting = ElasticSearchSetting.Instance;
        }

        public ElasticClient GetElasticClient()
        {
            var settings = new ConnectionSettings(new Uri(_elasticSearchSetting.Address))
                .DefaultIndex(_elasticSearchSetting.DefaultIndexName)
                .RequestTimeout(TimeSpan.FromMilliseconds(_elasticSearchSetting.RequestTimeout));
            if (!string.IsNullOrEmpty( _elasticSearchSetting.UserName)) 
            {
                settings.BasicAuthentication(_elasticSearchSetting.UserName, _elasticSearchSetting.Password);
            }                   
            var client = new ElasticClient(settings);
            return client;
        }
    }
}
