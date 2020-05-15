using Microsoft.Extensions.Logging;
using Surging.Core.CPlatform.Runtime.Session;
using Surging.Core.CPlatform.Utilities;
using Surging.Core.Domain.Entities;
using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using SurgingReflection = Surging.Core.CPlatform.Utilities;

namespace Surging.Core.Dapper.Filters.Action
{
    public abstract class DapperActionFilterBase 
    {
        protected const int Normal = 0;
        protected const int IsDeleted = 1;
        protected readonly ILogger<DapperActionFilterBase> _logger;
        protected readonly ISurgingSession _loginUser;
        public DapperActionFilterBase()
        {
            _logger = ServiceLocator.GetService<ILogger<DapperActionFilterBase>>();
            _loginUser = NullSurgingSession.Instance;
        }

        protected virtual void CheckAndSetId(object entityAsObj)
        {
            var entity1 = entityAsObj as IEntity<Guid>;
            if (entity1 != null && entity1.Id == Guid.Empty)
            {
                Type entityType = entityAsObj.GetType();
                PropertyInfo idProperty = entityType.GetProperty("Id");
                var dbGeneratedAttr = SurgingReflection.ReflectionHelper.GetSingleAttributeOrDefault<DatabaseGeneratedAttribute>(idProperty);
                if (dbGeneratedAttr == null || dbGeneratedAttr.DatabaseGeneratedOption == DatabaseGeneratedOption.None)
                {
                    entity1.Id = GuidGenerator.Create();
                }
            }
            var entity2 = entityAsObj as IEntity<string>;
            if (entity2 != null && string.IsNullOrEmpty(entity2.Id))
            {
                Type entityType = entityAsObj.GetType();
                PropertyInfo idProperty = entityType.GetProperty("Id");
                var dbGeneratedAttr = SurgingReflection.ReflectionHelper.GetSingleAttributeOrDefault<DatabaseGeneratedAttribute>(idProperty);
                if (dbGeneratedAttr == null || dbGeneratedAttr.DatabaseGeneratedOption == DatabaseGeneratedOption.None)
                {
                    entity2.Id = GuidGenerator.CreateGuidStrWithNoUnderline();
                }
            }

        }
    }
}
