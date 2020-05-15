using Surging.Core.CPlatform.Ioc;
using Surging.Core.Domain.Entities;
using System;

namespace Surging.Core.Dapper.Filters.Elastic
{
    public interface IElasticFilter<TEntity, TPrimaryKey> : ITransientDependency where TEntity : class, IEntity<TPrimaryKey>
    {
        bool ExecuteFilter(TEntity entity);

        Exception ElasticException { get; }
    }
}
