using Autofac;
using Microsoft.Extensions.Configuration;
using Surging.Core.CPlatform;
using Surging.Core.CPlatform.Exceptions;
using Surging.Core.CPlatform.Module;
using Surging.Core.CPlatform.Utilities;
using Surging.Core.ElasticSearch.Provider;

namespace Surging.Core.ElasticSearch
{
    public class ElasticSearchModule : EnginePartModule
    {
        protected override void RegisterBuilder(ContainerBuilderWrapper builder) 
        {
            var esSettingSection = AppConfig.GetSection("ElasticSearch");
            if (!esSettingSection.Exists())
            {
                throw new CPlatformException("未对ElasticSearch服务进行配置");
            }

            var esSetting = AppConfig.Configuration.GetSection("ElasticSearch").Get<ElasticSearchSetting>();
            ElasticSearchSetting.Instance = esSetting;

            builder.RegisterType<ElasticSearchProvider>().As<IElasticSearchProvider>().AsSelf().InstancePerDependency();
        }
    }
}
