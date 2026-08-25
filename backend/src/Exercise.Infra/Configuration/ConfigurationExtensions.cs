using Exercise.Infra.Common;
using Exercise.Infra.Exceptions;
using Microsoft.Extensions.Configuration;

namespace Exercise.Infra.Configuration
{
    public static class ConfigurationExtensions
    {
        public static T GetSectionValue<T>(
            this IConfiguration configuration,
            string sectionName,
            string key
        )
        {
            var value = configuration.GetOptionalSectionValue<T>(
                sectionName,
                key,
                default
            );

            if (value is not null)
                return value;

            #region Exception

            throw ExceptionConstructor.CreateParameterized(
                "Required configuration value is missing.",
                new
                {
                    SectionName = sectionName,
                    Key = key
                }
            );

            #endregion
        }

        public static T? GetOptionalSectionValue<T>(
            this IConfiguration configuration,
            string sectionName,
            string key,
            T? defaultValue = default
        )
        {
            var section = configuration.GetSection(sectionName);
            var rawValue = section[key];

            if (string.IsNullOrWhiteSpace(rawValue))
                return defaultValue;

            var value = section.GetValue<T>(key);

            return value;
        }

        public static T GetSectionObject<T>(
            this IConfiguration configuration,
            string sectionName
        )
        {
            var section = configuration.GetSection(sectionName);

            if (!section.Exists())
            {
                #region Exception

                throw ExceptionConstructor.CreateParameterized(
                    "Required configuration section is missing.",
                    new
                    {
                        SectionName = sectionName
                    }
                );

                #endregion
            }

            var value = section.Get<T>();

            if (value is not null)
                return value;

            #region Exception

            throw ExceptionConstructor.CreateParameterized(
                "Required configuration section could not be bound.",
                new
                {
                    SectionName = sectionName,
                    TargetType = typeof(T).FullName
                }
            );

            #endregion
        }

        public static string GetServiceName(this IConfiguration configuration)
        {
            var serviceName = configuration.GetSectionValue<string>(
                CommonConfigurationKeys.ServiceSection,
                CommonConfigurationKeys.Name
            );

            return serviceName;
        }
    }
}
