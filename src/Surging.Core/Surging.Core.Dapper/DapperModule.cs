using Autofac;
using DapperExtensions.Mapper;
using DapperExtensions.Sql;
using Microsoft.Extensions.Configuration;
using Surging.Core.CPlatform;
using Surging.Core.CPlatform.Exceptions;
using Surging.Core.CPlatform.Module;
using Surging.Core.CPlatform.Utilities;
using Surging.Core.Dapper.Filters.Action;
using Surging.Core.Dapper.Filters.Elastic;
using Surging.Core.Dapper.Filters.Query;
using Surging.Core.Dapper.Repositories;
using System;
using System.Linq;

namespace Surging.Core.Dapper
{
    public class DapperModule : EnginePartModule
    {
        bool isUseElasicSearchModule = false;
        public override void Initialize(AppModuleContext context)
        {
            base.Initialize(context);
            if (context.Modules.Any(p => p.Enable && p.ModuleName == "ElasticSearchModule")) {
                isUseElasicSearchModule = true;
                if (DbSetting.Instance != null) {
                    DbSetting.Instance.UseElasicSearchModule = isUseElasicSearchModule;
                }
            }
        }

        protected override void RegisterBuilder(ContainerBuilderWrapper builder)
        {
            base.RegisterBuilder(builder);
            builder.RegisterGeneric(typeof(DapperRepository<,>)).As(typeof(IDapperRepository<,>)).InstancePerDependency();
            builder.RegisterGeneric(typeof(CreationAuditDapperActionFilter<,>)).Named(typeof(CreationAuditDapperActionFilter<,>).Name, typeof(IAuditActionFilter<,>)).InstancePerDependency();
            builder.RegisterGeneric(typeof(ModificationAuditDapperActionFilter<,>)).Named(typeof(ModificationAuditDapperActionFilter<,>).Name, typeof(IAuditActionFilter<,>)).InstancePerDependency();
            builder.RegisterGeneric(typeof(DeletionAuditDapperActionFilter<,>)).Named(typeof(DeletionAuditDapperActionFilter<,>).Name, typeof(IAuditActionFilter<,>)).InstancePerDependency();
            builder.RegisterType<SoftDeleteQueryFilter>().As<ISoftDeleteQueryFilter>().AsSelf().InstancePerDependency();
            DapperExtensions.DapperExtensions.DefaultMapper = typeof(ClassMapper<>);

            builder.RegisterGeneric(typeof(CreationElasticFilter<,>)).Named(typeof(CreationElasticFilter<,>).Name, typeof(IElasticFilter<,>)).InstancePerDependency();
            builder.RegisterGeneric(typeof(ModificationElasticFilter<,>)).Named(typeof(ModificationElasticFilter<,>).Name, typeof(IElasticFilter<,>)).InstancePerDependency();
            builder.RegisterGeneric(typeof(DeletionElasticFilter<,>)).Named(typeof(DeletionElasticFilter<,>).Name, typeof(IElasticFilter<,>)).InstancePerDependency();

            var dbSettingSection = AppConfig.GetSection("DbSetting");
            if (!dbSettingSection.Exists())
            {
                throw new DataAccessException("未对数据库进行配置");
            }

            var dbSetting = new DbSetting()
            {
                DbType = Enum.Parse<DbType>(EnvironmentHelper.GetEnvironmentVariable(AppConfig.GetSection("DbSetting:DbType").Get<string>())),
                ConnectionString = EnvironmentHelper.GetEnvironmentVariable(AppConfig.GetSection("DbSetting:ConnectionString").Get<string>()),
                UseElasicSearchModule = isUseElasicSearchModule
            };

            DbSetting.Instance = dbSetting;
            switch (dbSetting.DbType)
            {
                case DbType.MySql:
                    DapperExtensions.DapperExtensions.SqlDialect = new MySqlDialect();
                    break;
                case DbType.Oracle:
                    DapperExtensions.DapperExtensions.SqlDialect = new OracleDialect();
                    break;
                case DbType.SqlServer:
                    DapperExtensions.DapperExtensions.SqlDialect = new SqlServerDialect();
                    break;

            }

       
        }
    }
}
