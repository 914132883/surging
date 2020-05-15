using Dapper;
using DapperExtensions;
using Microsoft.Extensions.Logging;
using Surging.Core.CPlatform.Exceptions;
using Surging.Core.CPlatform.Utilities;
using Surging.Core.Dapper.Expressions;
using Surging.Core.Dapper.Filters.Action;
using Surging.Core.Dapper.Filters.Elastic;
using Surging.Core.Dapper.Filters.Query;
using Surging.Core.Domain.Entities;
using Surging.Core.Domain.PagedAndSorted;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Surging.Core.Dapper.Repositories
{
    public class DapperRepository<TEntity, TPrimaryKey> : DapperRepositoryBase, IDapperRepository<TEntity, TPrimaryKey> where TEntity : class, IEntity<TPrimaryKey>
    {
        private readonly ISoftDeleteQueryFilter _softDeleteQueryFilter;
        private readonly IAuditActionFilter<TEntity, TPrimaryKey> _creationActionFilter;
        private readonly IAuditActionFilter<TEntity, TPrimaryKey> _modificationActionFilter;
        private readonly IAuditActionFilter<TEntity, TPrimaryKey> _deletionAuditDapperActionFilter;

        private readonly IElasticFilter<TEntity, TPrimaryKey> _creationElasitcFilter;
        private readonly IElasticFilter<TEntity, TPrimaryKey> _modificationElasitcFilter;
        private readonly IElasticFilter<TEntity, TPrimaryKey> _deletionElasitcFilter;

        protected readonly bool isUserSearchElasitcModule = false;
        private readonly bool _elasiticClient;

        private readonly ILogger<DapperRepository<TEntity, TPrimaryKey>> _logger;
        public DapperRepository(ISoftDeleteQueryFilter softDeleteQueryFilter,
            ILogger<DapperRepository<TEntity, TPrimaryKey>> logger)
        {
            _softDeleteQueryFilter = softDeleteQueryFilter;
            _logger = logger;
            _creationActionFilter = ServiceLocator.GetService<IAuditActionFilter<TEntity, TPrimaryKey>>(typeof(CreationAuditDapperActionFilter<TEntity, TPrimaryKey>).Name);
            _modificationActionFilter = ServiceLocator.GetService<IAuditActionFilter<TEntity, TPrimaryKey>>(typeof(ModificationAuditDapperActionFilter<TEntity, TPrimaryKey>).Name);
            _deletionAuditDapperActionFilter = ServiceLocator.GetService<IAuditActionFilter<TEntity, TPrimaryKey>>(typeof(DeletionAuditDapperActionFilter<TEntity, TPrimaryKey>).Name);

            isUserSearchElasitcModule = DbSetting.Instance.UseElasicSearchModule;
            if (isUserSearchElasitcModule)
            {
                _creationElasitcFilter = ServiceLocator.GetService<IElasticFilter<TEntity, TPrimaryKey>>(typeof(CreationElasticFilter<TEntity, TPrimaryKey>).Name);
                _modificationElasitcFilter = ServiceLocator.GetService<IElasticFilter<TEntity, TPrimaryKey>>(typeof(ModificationElasticFilter<TEntity, TPrimaryKey>).Name);
                _deletionElasitcFilter = ServiceLocator.GetService<IElasticFilter<TEntity, TPrimaryKey>>(typeof(DeletionElasticFilter<TEntity, TPrimaryKey>).Name);
            }
        }

        public Task InsertAsync(TEntity entity)
        {
            try
            {
                using (var conn = GetDbConnection())
                {
                    conn.Open();
                    using (var trans = conn.BeginTransaction())
                    {
                        _creationActionFilter.ExecuteFilter(entity);
                        conn.Insert<TEntity>(entity, trans);
                        if (isUserSearchElasitcModule)
                        {
                            if (!_creationElasitcFilter.ExecuteFilter(entity))
                            {
                                trans.Rollback();
                                throw new DataAccessException("elasticsearch server error", _creationElasitcFilter.ElasticException);
                            }
                        }
                        trans.Commit();
                    }
                    conn.Close();
                    return Task.CompletedTask;
                }

            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex.Message, ex);
                }

                throw new DataAccessException(ex.Message, ex);
            }
        }



        public Task<TPrimaryKey> InsertAndGetIdAsync(TEntity entity)
        {
            try
            {
                using (var conn = GetDbConnection())
                {
                    conn.Open();
                    using (var trans = conn.BeginTransaction())
                    {

                        _creationActionFilter.ExecuteFilter(entity);
                      
                        conn.Insert(entity, trans);
                        if (isUserSearchElasitcModule)
                        {
                            if (!_creationElasitcFilter.ExecuteFilter(entity))
                            {
                                trans.Rollback();
                                throw new DataAccessException("elasticsearch server error", _creationElasitcFilter.ElasticException);
                            }
                        }
                        trans.Commit();

                    }
                    conn.Close();
                    return Task.FromResult(entity.Id);
                }
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex.Message, ex);
                }

                throw new DataAccessException(ex.Message, ex);
            }
        }

        public async Task InsertOrUpdateAsync(TEntity entity)
        {
            try
            {
                if (entity.Id == null)
                {
                    _creationActionFilter.ExecuteFilter(entity);
                    await InsertAsync(entity);
                }
                else
                {
                    var existEntity = await SingleOrDefaultAsync(p => p.Id.Equals(entity.Id));
                    if (existEntity == null)
                    {
                        _creationActionFilter.ExecuteFilter(entity);
                        await InsertAsync(entity);
                    }
                    else
                    {
                        _modificationActionFilter.ExecuteFilter(entity);
                        await UpdateAsync(entity);
                    }
                }
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex.Message, ex);
                }

                throw new DataAccessException(ex.Message, ex);
            }

        }

        public async Task<TPrimaryKey> InsertOrUpdateAndGetIdAsync(TEntity entity)
        {
            try
            {
                if (entity.Id == null)
                {
                    _creationActionFilter.ExecuteFilter(entity);
                    return await InsertAndGetIdAsync(entity);
                }
                else
                {
                    var existEntity = SingleAsync(CreateEqualityExpressionForId(entity.Id));
                    if (existEntity == null)
                    {
                        _creationActionFilter.ExecuteFilter(entity);
                        return await InsertAndGetIdAsync(entity);
                    }
                    else
                    {
                        _modificationActionFilter.ExecuteFilter(entity);
                        await UpdateAsync(entity);
                        return entity.Id;
                    }
                }
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex.Message, ex);
                }

                throw new DataAccessException(ex.Message, ex);
            }
        }

        public Task DeleteAsync(TEntity entity)
        {
            try
            {
                using (var conn = GetDbConnection())
                {
                    conn.Open();
                    using (var trans = conn.BeginTransaction())
                    {
                        if (entity is ISoftDelete)
                        {
                            _deletionAuditDapperActionFilter.ExecuteFilter(entity);
                            UpdateAsync(entity, conn, trans);
                        }
                        else
                        {
                            conn.Delete(entity, trans);
                        }
                        if (isUserSearchElasitcModule)
                        {
                            if (!_deletionElasitcFilter.ExecuteFilter(entity))
                            {
                                trans.Rollback();
                                throw new DataAccessException("elasticsearch server error", _deletionElasitcFilter.ElasticException);
                            }
                        }
                        trans.Commit();
                    }
                    conn.Close();
                    return Task.CompletedTask;
                }
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex.Message, ex);
                }

                throw new DataAccessException(ex.Message, ex);
            }

        }

        public async Task DeleteAsync(Expression<Func<TEntity, bool>> predicate)
        {
            IEnumerable<TEntity> items = await GetAllAsync(predicate);
            using (var conn = GetDbConnection())
            {
                if (conn.State != System.Data.ConnectionState.Open)
                {
                    conn.Open();
                }
                using (var trans = conn.BeginTransaction())
                {
                    foreach (TEntity entity in items)
                    {
                        await DeleteAsync(entity, conn, trans);
                    }
                    trans.Commit();
                }
                conn.Close();
            }


        }

        public Task UpdateAsync(TEntity entity)
        {
            try
            {
                using (var conn = GetDbConnection())
                {
                    conn.Open();
                    using (var trans = conn.BeginTransaction())
                    {
                        _modificationActionFilter.ExecuteFilter(entity);
                        conn.Update(entity, trans);
                        if (isUserSearchElasitcModule)
                        {
                            if (!_modificationElasitcFilter.ExecuteFilter(entity))
                            {
                                trans.Rollback();
                                throw new DataAccessException("elasticsearch server error", _modificationElasitcFilter.ElasticException);
                            }
                        }
                        trans.Commit();

                    }
                    conn.Close();
                    return Task.CompletedTask;
                }
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex.Message, ex);
                }

                throw new DataAccessException(ex.Message, ex);
            }
        }

        public Task<TEntity> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate)
        {
            try
            {
                using (var conn = GetDbConnection())
                {
                    conn.Open();
                    predicate = _softDeleteQueryFilter.ExecuteFilter<TEntity, TPrimaryKey>(predicate);
                    var pg = predicate.ToPredicateGroup<TEntity, TPrimaryKey>();
                    var result = conn.GetList<TEntity>(pg).FirstOrDefault();
                    conn.Close();
                    return Task.FromResult(result);
                }
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex.Message, ex);
                }

                throw new DataAccessException(ex.Message, ex);
            }
        }

        public Task<TEntity> FirstAsync(Expression<Func<TEntity, bool>> predicate)
        {
            try
            {
                using (var conn = GetDbConnection())
                {
                    conn.Open();
                    predicate = _softDeleteQueryFilter.ExecuteFilter<TEntity, TPrimaryKey>(predicate);
                    var pg = predicate.ToPredicateGroup<TEntity, TPrimaryKey>();
                    var result = conn.GetList<TEntity>(pg).First();
                    return Task.FromResult(result);
                }
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex.Message, ex);
                }

                throw new DataAccessException(ex.Message, ex);
            }
        }


        public Task<TEntity> SingleAsync(Expression<Func<TEntity, bool>> predicate)
        {
            try
            {
                using (var conn = GetDbConnection())
                {
                    conn.Open();
                    predicate = _softDeleteQueryFilter.ExecuteFilter<TEntity, TPrimaryKey>(predicate);
                    var pg = predicate.ToPredicateGroup<TEntity, TPrimaryKey>();
                    var result = conn.GetList<TEntity>(pg).Single();
                    conn.Close();
                    return Task.FromResult(result);
                }
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex.Message, ex);
                }

                throw new DataAccessException(ex.Message, ex);
            }
        }


        public Task<TEntity> SingleOrDefaultAsync(Expression<Func<TEntity, bool>> predicate)
        {
            try
            {
                using (var conn = GetDbConnection())
                {
                    conn.Open();
                    predicate = _softDeleteQueryFilter.ExecuteFilter<TEntity, TPrimaryKey>(predicate);
                    var pg = predicate.ToPredicateGroup<TEntity, TPrimaryKey>();
                    var result = conn.GetList<TEntity>(pg).SingleOrDefault();
                    conn.Close();
                    return Task.FromResult(result);
                }
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex.Message, ex);
                }

                throw new DataAccessException(ex.Message, ex);
            }
        }

        public Task<TEntity> GetAsync(TPrimaryKey id)
        {
            return SingleAsync(CreateEqualityExpressionForId(id));
        }


        public Task<IEnumerable<TEntity>> GetAllAsync()
        {
            try
            {
                using (var conn = GetDbConnection())
                {
                    conn.Open();
                    var predicate = _softDeleteQueryFilter.ExecuteFilter<TEntity, TPrimaryKey>();
                    var pg = predicate.ToPredicateGroup<TEntity, TPrimaryKey>();
                    var list = conn.GetList<TEntity>(pg);
                    conn.Close();
                    return Task.FromResult(list);
                }
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex.Message, ex);
                }

                throw new DataAccessException(ex.Message, ex);
            }
        }

        public Task<IEnumerable<TEntity>> GetAllAsync(Expression<Func<TEntity, bool>> predicate)
        {
            try
            {
                using (var conn = GetDbConnection())
                {
                    conn.Open();
                    predicate = _softDeleteQueryFilter.ExecuteFilter<TEntity, TPrimaryKey>(predicate);
                    var pg = predicate.ToPredicateGroup<TEntity, TPrimaryKey>();
                    var list = conn.GetList<TEntity>(pg);
                    conn.Close();
                    return Task.FromResult(list);
                }
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex.Message, ex);
                }

                throw new DataAccessException(ex.Message, ex);
            }
        }


        public async Task<IEnumerable<TEntity>> QueryAsync(string query, object parameters = null)
        {
            if (string.IsNullOrEmpty(query))
            {
                throw new ArgumentNullException("Sql语句不允许为空");
            }

            try
            {
                using (var conn = GetDbConnection())
                {
                    conn.Open();
                    var result = await conn.QueryAsync<TEntity>(query, parameters);
                    conn.Close();
                    return result;
                }
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex.Message, ex);
                }

                throw new DataAccessException(ex.Message, ex);
            }

        }

        public async Task<IEnumerable<TAny>> Query<TAny>(string query, object parameters = null) where TAny : class
        {
            if (string.IsNullOrEmpty(query))
            {
                throw new ArgumentNullException("Sql语句不允许为空");
            }

            try
            {
                using (var conn = GetDbConnection())
                {
                    conn.Open();
                    var result = await conn.QueryAsync<TAny>(query, parameters);
                    conn.Close();
                    return result;
                }
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex.Message, ex);
                }

                throw new DataAccessException(ex.Message, ex);
            }
        }


        public Task InsertAsync(TEntity entity, DbConnection conn, DbTransaction trans)
        {
            try
            {
                _creationActionFilter.ExecuteFilter(entity);
                conn.Insert<TEntity>(entity, trans);
                if (isUserSearchElasitcModule)
                {
                    if (!_creationElasitcFilter.ExecuteFilter(entity))
                    {
                        trans.Rollback();
                        throw new DataAccessException("elasticsearch server error", _creationElasitcFilter.ElasticException);
                    }
                }
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex.Message, ex);
                }

                throw new DataAccessException(ex.Message, ex);
            }

        }

        public Task<TPrimaryKey> InsertAndGetIdAsync(TEntity entity, DbConnection conn, DbTransaction trans)
        {
            try
            {
                _creationActionFilter.ExecuteFilter(entity);
                conn.Insert<TEntity>(entity, trans);
                if (isUserSearchElasitcModule)
                {
                    if (!_creationElasitcFilter.ExecuteFilter(entity))
                    {
                        trans.Rollback();
                        throw new DataAccessException("elasticsearch server error", _creationElasitcFilter.ElasticException);
                    }
                }
                return Task.FromResult(entity.Id);
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex.Message, ex);
                }

                throw new DataAccessException(ex.Message, ex);
            }

        }

        public async Task InsertOrUpdateAsync(TEntity entity, DbConnection conn, DbTransaction trans)
        {
            try
            {
                if (entity.Id == null)
                {
                    _creationActionFilter.ExecuteFilter(entity);
                    await InsertAsync(entity, conn, trans);
                }
                else
                {
                    var existEntity = await SingleOrDefaultAsync(p => p.Id.Equals(entity.Id));
                    if (existEntity == null)
                    {
                        _creationActionFilter.ExecuteFilter(entity);
                        await InsertAsync(entity, conn, trans);
                    }
                    else
                    {
                        _modificationActionFilter.ExecuteFilter(entity);
                        await UpdateAsync(entity, conn, trans);
                    }
                }
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex.Message, ex);
                }

                throw new DataAccessException(ex.Message, ex);
            }


        }

        public async Task<TPrimaryKey> InsertOrUpdateAndGetIdAsync(TEntity entity, DbConnection conn, DbTransaction trans)
        {

            try
            {
                if (entity.Id == null)
                {
                    _creationActionFilter.ExecuteFilter(entity);
                    return await InsertAndGetIdAsync(entity, conn, trans);
                }
                else
                {
                    var existEntity = SingleAsync(CreateEqualityExpressionForId(entity.Id));
                    if (existEntity == null)
                    {
                        _creationActionFilter.ExecuteFilter(entity);
                        return await InsertAndGetIdAsync(entity, conn, trans);
                    }
                    else
                    {
                        _modificationActionFilter.ExecuteFilter(entity);
                        await UpdateAsync(entity, conn, trans);
                        return entity.Id;
                    }
                }

            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex.Message, ex);
                }

                throw new DataAccessException(ex.Message, ex);
            }
        }

        public Task UpdateAsync(TEntity entity, DbConnection conn, DbTransaction trans)
        {
            try
            {
                _modificationActionFilter.ExecuteFilter(entity);
                conn.Update(entity, trans);
                if (isUserSearchElasitcModule)
                {
                    if (!_modificationElasitcFilter.ExecuteFilter(entity))
                    {
                        trans.Rollback();
                        throw new DataAccessException("elasticsearch server error", _modificationElasitcFilter.ElasticException);
                    }
                }
                return Task.CompletedTask;

            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex.Message, ex);
                }

                throw new DataAccessException(ex.Message, ex);
            }


        }

        public Task DeleteAsync(TEntity entity, DbConnection conn, DbTransaction trans)
        {
            try
            {

                if (entity is ISoftDelete)
                {
                    _deletionAuditDapperActionFilter.ExecuteFilter(entity);
                    UpdateAsync(entity, conn, trans);
                }
                else
                {
                    conn.Delete(entity, trans);
                }
                if (isUserSearchElasitcModule)
                {
                    if (!_deletionElasitcFilter.ExecuteFilter(entity))
                    {
                        trans.Rollback();
                        throw new DataAccessException("elasticsearch server error", _deletionElasitcFilter.ElasticException);
                    }
                }

                return Task.CompletedTask;

            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex.Message, ex);
                }

                throw new DataAccessException(ex.Message, ex);
            }

        }

        public async Task DeleteAsync(Expression<Func<TEntity, bool>> predicate, DbConnection conn, DbTransaction trans)
        {
            IEnumerable<TEntity> items = await GetAllAsync(predicate);
            foreach (TEntity entity in items)
            {
                await DeleteAsync(entity, conn, trans);
            }

        }

        protected static Expression<Func<TEntity, bool>> CreateEqualityExpressionForId(TPrimaryKey id)
        {
            ParameterExpression lambdaParam = Expression.Parameter(typeof(TEntity));

            BinaryExpression lambdaBody = Expression.Equal(
                Expression.PropertyOrField(lambdaParam, "Id"),
                Expression.Constant(id, typeof(TPrimaryKey))
            );

            return Expression.Lambda<Func<TEntity, bool>>(lambdaBody, lambdaParam);
        }

        public async Task<int> GetCountAsync()
        {
            try
            {
                using (var conn = GetDbConnection())
                {
                    conn.Open();
                    var predicate = _softDeleteQueryFilter.ExecuteFilter<TEntity, TPrimaryKey>();
                    var pg = predicate.ToPredicateGroup<TEntity, TPrimaryKey>();
                    var count = conn.Count<TEntity>(pg);
                    conn.Close();
                    return count;
                }
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex.Message, ex);
                }

                throw new DataAccessException(ex.Message, ex);
            }
        }

        public async Task<int> GetCountAsync(Expression<Func<TEntity, bool>> predicate)
        {
            try
            {
                using (var conn = GetDbConnection())
                {
                    conn.Open();
                    predicate = _softDeleteQueryFilter.ExecuteFilter<TEntity, TPrimaryKey>(predicate);
                    var pg = predicate.ToPredicateGroup<TEntity, TPrimaryKey>();
                    var count = conn.Count<TEntity>(pg);
                    conn.Close();
                    return count;
                }
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex.Message, ex);
                }

                throw new DataAccessException(ex.Message, ex);
            }
        }

        public Task<Tuple<IEnumerable<TEntity>, int>> GetPageAsync(Expression<Func<TEntity, bool>> predicate, int index, int count, IDictionary<string, SortType> sortProps)
        {
            try
            {
                using (var conn = GetDbConnection())
                {
                    IList<ISort> sorts = new List<ISort>();

                    if (sortProps != null && sortProps.Any())
                    {
                        foreach (var sortProp in sortProps)
                        {
                            var sort = new Sort()
                            {
                                PropertyName = sortProp.Key,
                                Ascending = sortProp.Value == SortType.Asc ? true : false
                            };
                            sorts.Add(sort);
                        };
                    }
                    else
                    {
                        sorts.Add(new Sort()
                        {
                            PropertyName = "Id",
                            Ascending = false
                        });
                    }

                    predicate = _softDeleteQueryFilter.ExecuteFilter<TEntity, TPrimaryKey>(predicate);
                    var pg = predicate.ToPredicateGroup<TEntity, TPrimaryKey>();
                    var pageList = conn.GetPage<TEntity>(pg, sorts, index - 1, count).ToList();
                    var totalCount = conn.Count<TEntity>(pg);
                    return Task.FromResult(new Tuple<IEnumerable<TEntity>, int>(pageList, totalCount));
                }
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex.Message, ex);
                }

                throw new DataAccessException(ex.Message, ex);
            }

        }

        public Task<Tuple<IEnumerable<TEntity>, int>> GetPageAsync(Expression<Func<TEntity, bool>> predicate, int index, int count)
        {
            return GetPageAsync(predicate, index, count, null);
        }

        public Task<Tuple<IEnumerable<TEntity>, int>> GetPageAsync(int index, int count, IDictionary<string, SortType> sortProps)
        {
            try
            {
                using (var conn = GetDbConnection())
                {
                    IList<ISort> sorts = new List<ISort>();

                    if (sortProps != null && sortProps.Any())
                    {
                        foreach (var sortProp in sortProps)
                        {
                            var sort = new Sort()
                            {
                                PropertyName = sortProp.Key,
                                Ascending = sortProp.Value == SortType.Asc ? true : false
                            };
                            sorts.Add(sort);
                        };
                    }
                    else
                    {
                        var sort = new Sort()
                        {
                            PropertyName = "Id",
                            Ascending = true
                        };
                        sorts.Add(sort);
                    }
                    var predicate = _softDeleteQueryFilter.ExecuteFilter<TEntity, TPrimaryKey>();
                    var pg = predicate.ToPredicateGroup<TEntity, TPrimaryKey>();
                    var pageList = conn.GetPage<TEntity>(pg, sorts, index - 1, count).ToList();
                    var totalCount = conn.Count<TEntity>(pg);
                    return Task.FromResult(new Tuple<IEnumerable<TEntity>, int>(pageList, totalCount));
                }
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex.Message, ex);
                }

                throw new DataAccessException(ex.Message, ex);
            }

        }

        public Task<Tuple<IEnumerable<TEntity>, int>> GetPageAsync(int index, int count)
        {
            return GetPageAsync(index, count, null);
        }

        public Task<int> GetCountAsync(DbConnection conn, DbTransaction trans)
        {
            try
            {

                var predicate = _softDeleteQueryFilter.ExecuteFilter<TEntity, TPrimaryKey>();
                var pg = predicate.ToPredicateGroup<TEntity, TPrimaryKey>();
                var count = conn.Count<TEntity>(pg, transaction: trans);
                return Task.FromResult(count);
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex.Message, ex);
                }

                throw new DataAccessException(ex.Message, ex);
            }
        }

        public Task<TEntity> SingleAsync(Expression<Func<TEntity, bool>> predicate, DbConnection conn, DbTransaction trans)
        {
            predicate = _softDeleteQueryFilter.ExecuteFilter<TEntity, TPrimaryKey>(predicate);
            var pg = predicate.ToPredicateGroup<TEntity, TPrimaryKey>();
            var result = conn.GetList<TEntity>(pg, transaction: trans).Single();
            return Task.FromResult(result);
        }

        public Task<TEntity> SingleOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, DbConnection conn, DbTransaction trans)
        {
            predicate = _softDeleteQueryFilter.ExecuteFilter<TEntity, TPrimaryKey>(predicate);
            var pg = predicate.ToPredicateGroup<TEntity, TPrimaryKey>();
            var result = conn.GetList<TEntity>(pg, transaction: trans).SingleOrDefault();
            return Task.FromResult(result);
        }

        public Task<TEntity> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, DbConnection conn, DbTransaction trans)
        {
            predicate = _softDeleteQueryFilter.ExecuteFilter<TEntity, TPrimaryKey>(predicate);
            var pg = predicate.ToPredicateGroup<TEntity, TPrimaryKey>();
            var result = conn.GetList<TEntity>(pg, transaction: trans).FirstOrDefault();
            return Task.FromResult(result);
        }

        public Task<TEntity> FirstAsync(Expression<Func<TEntity, bool>> predicate, DbConnection conn, DbTransaction trans)
        {
            predicate = _softDeleteQueryFilter.ExecuteFilter<TEntity, TPrimaryKey>(predicate);
            var pg = predicate.ToPredicateGroup<TEntity, TPrimaryKey>();
            var result = conn.GetList<TEntity>(pg, transaction: trans).FirstOrDefault();
            return Task.FromResult(result);
        }

        public Task<TEntity> GetAsync(TPrimaryKey id, DbConnection conn, DbTransaction trans)
        {
            return SingleAsync(CreateEqualityExpressionForId(id), conn, trans);
        }

        public Task<IEnumerable<TEntity>> GetAllAsync(Expression<Func<TEntity, bool>> predicate, DbConnection conn, DbTransaction trans)
        {
            predicate = _softDeleteQueryFilter.ExecuteFilter<TEntity, TPrimaryKey>(predicate);
            var pg = predicate.ToPredicateGroup<TEntity, TPrimaryKey>();
            var list = conn.GetList<TEntity>(pg, transaction: trans);
            return Task.FromResult(list);
        }

        public Task<IEnumerable<TEntity>> GetAllAsync(DbConnection conn, DbTransaction trans)
        {
            var predicate = _softDeleteQueryFilter.ExecuteFilter<TEntity, TPrimaryKey>();
            var pg = predicate.ToPredicateGroup<TEntity, TPrimaryKey>();
            var list = conn.GetList<TEntity>(pg, transaction: trans);
            return Task.FromResult(list);
        }

        public Task<IEnumerable<TEntity>> GetAllIncludeSoftDeleteAsync(Expression<Func<TEntity, bool>> predicate)
        {
            try
            {
                using (var conn = GetDbConnection())
                {
                    conn.Open();
                    var pg = predicate.ToPredicateGroup<TEntity, TPrimaryKey>();
                    var list = conn.GetList<TEntity>(pg);
                    conn.Close();
                    return Task.FromResult(list);
                }
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex.Message, ex);
                }

                throw new DataAccessException(ex.Message, ex);
            }
        }

        public Task<IEnumerable<TEntity>> GetAllIncludeSoftDeleteAsync()
        {
            try
            {
                using (var conn = GetDbConnection())
                {
                    conn.Open();
                    var list = conn.GetList<TEntity>();
                    conn.Close();
                    return Task.FromResult(list);
                }
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex.Message, ex);
                }

                throw new DataAccessException(ex.Message, ex);
            }
        }

        public Task<IEnumerable<TEntity>> GetAllIncludeSoftDeleteAsync(Expression<Func<TEntity, bool>> predicate, DbConnection conn, DbTransaction trans)
        {

            var pg = predicate.ToPredicateGroup<TEntity, TPrimaryKey>();
            var list = conn.GetList<TEntity>(pg, transaction: trans);
            return Task.FromResult(list);
        }

        public Task<IEnumerable<TEntity>> GetAllIncludeSoftDeleteAsync(DbConnection conn, DbTransaction trans)
        {
            var list = conn.GetList<TEntity>();
            return Task.FromResult(list);
        }
    }
}
