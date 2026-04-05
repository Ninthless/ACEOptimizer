using System;
using System.Globalization;
using System.Windows;

namespace ACEOptimizer.Services
{
    internal sealed class LocalizationService
    {
        public void LoadApplicationResources(Application application)
        {
            ResourceDictionary dictionary = CreateDictionary(CultureInfo.CurrentUICulture);
            application.Resources.MergedDictionaries.Add(dictionary);
        }

        public string GetStringForCurrentCulture(string resourceKey, string fallback)
        {
            try
            {
                ResourceDictionary dictionary = CreateDictionary(CultureInfo.CurrentUICulture);
                return dictionary[resourceKey] as string ?? fallback;
            }
            catch
            {
                return fallback;
            }
        }

        public string GetStringFromApplicationResources(Application application, string resourceKey, string fallback)
        {
            return application.TryFindResource(resourceKey) as string ?? fallback;
        }

        private ResourceDictionary CreateDictionary(CultureInfo culture)
        {
            return new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/{GetResourcePath(culture)}", UriKind.Absolute)
            };
        }

        private static string GetResourcePath(CultureInfo culture)
        {
            return culture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
                ? "Resources/Strings.zh-CN.xaml"
                : "Resources/Strings.en-US.xaml";
        }
    }
}
