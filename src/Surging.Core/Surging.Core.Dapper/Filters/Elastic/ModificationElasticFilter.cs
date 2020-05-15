using Surging.Core.Domain.Entities;
using System;

namespace Surging.Core.Dapper.Filters.Elastic
{
    public class ModificationElasticFilter<TEntity, TPrimaryKey> : ElasticFilterBase, IElasticFilter<TEntity, TPrimaryKey> where TEntity : class, IEntity<TPrimaryKey>
    {
        public Exception ElasticException { get; private set; }

        public bool ExecuteFilter(TEntity entity)
        {
            if (_isUseElasticSearchModule && typeof(IElasticSearch).IsAssignableFrom(typeof(TEntity)))
            {
                var indexName = typeof(TEntity).Name.ToLower();
                var indexResponse = _elasticClient.Index(entity, idx => idx.Index(indexName));
                if (indexResponse.IsValid)
                {
                    return true;
                }
                else
                {
                    ElasticException = indexResponse.OriginalException;
                    return false;
                }
            }
            return true;
        }
    }
}
