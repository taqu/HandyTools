
using Microsoft.VisualStudio.Utilities;

namespace HandyTools.Completion
{
    public enum Language
    {
        Text,
        C,
        Cpp,
        CSharp,
    }

    public record struct LanguageInfo(string name, Language language);

    internal class SupportedLanguage
    {
        public static LanguageInfo[] LanguageInfos { get; } = [
            new LanguageInfo("Plain Text", Language.Text),
            new LanguageInfo("C", Language.C),
            new LanguageInfo("C++", Language.Cpp),
            new LanguageInfo("C#", Language.CSharp),
        ];

        public static LanguageInfo GetLanguage(DocumentView documentView)
        {
            return LanguageInfos[0];
        }

        public static LanguageInfo GetLanguage(IContentType contentType, string ext)
        {
            return LanguageInfos[0];
        }
    }
}

