using Microsoft.Extensions.Logging;
using Surging.Core.CPlatform.Utilities;
using Surging.Core.Domain.Entities;
using Surging.Core.Domain.Entities.Auditing;
using System;
using System.Collections.Generic;
using System.Text;

namespace Surging.Core.Dapper.Filters.Action
{
    public class CreationAuditDapperActionFilter<TEntity, TPrimaryKey> : DapperActionFilterBase, IAuditActionFilter<TEntity, TPrimaryKey> where TEntity : class, IEntity<TPrimaryKey>
    {
       
        public void ExecuteFilter(TEntity entity)
        {
            if (typeof(ISoftDelete).IsAssignableFrom(entity.GetType()))
            {
                ((ISoftDelete)entity).IsDeleted = Normal;
            }
            if (typeof(IHasCreationTime).IsAssignableFrom(entity.GetType()))
            {
                ((IHasCreationTime)entity).CreationTime = DateTime.Now;
            }
            CheckAndSetId(entity);
           
            if (_loginUser == null) 
            {
                _logger.LogDebug($"未获取到登录的用户信息");
            }
            if (typeof(ICreationAudited).IsAssignableFrom(typeof(TEntity)) && _loginUser != null)
            {
                _logger.LogDebug($"当前操作数据的用户为:{_loginUser.UserId} - {_loginUser.UserName}");
                var record = entity as ICreationAudited;
                record.CreatorUserId = _loginUser.UserId;
            }
            if (typeof(IElasticSearch).IsAssignableFrom(typeof(TEntity))) 
            {
                
            }
        }
    }
}
