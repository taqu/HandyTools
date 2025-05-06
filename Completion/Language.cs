
using Microsoft.Build.Framework.XamlTypes;
using Microsoft.VisualStudio.Utilities;
using System.IO;

namespace HandyTools.Completion
{
    public enum Language
    {
        None,
        Text,
        C_Cpp,
        CSharp,
    }

    public record struct LanguageInfo(string name, Language language);

    internal class SupportedLanguage
    {
        public static LanguageInfo[] LanguageInfos { get; } = [
			new LanguageInfo("Unsupported", Language.None),
			new LanguageInfo("Plain Text", Language.Text),
            new LanguageInfo("C/C++", Language.C_Cpp),
            new LanguageInfo("C#", Language.CSharp),
        ];

        public static LanguageInfo GetLanguage(DocumentView documentView)
        {
			return GetLanguage(documentView.TextBuffer.ContentType, Path.GetExtension(documentView.FilePath)?.Trim('.'));
        }

        public static LanguageInfo GetLanguage(IContentType contentType, string ext)
        {
            switch (contentType.TypeName) {
                case "Text":
					return LanguageInfos[1];
				case "C/C++":
					return LanguageInfos[2];
				case "CSharp":
					return LanguageInfos[3];
                default:
                    return LanguageInfos[0];
			}
        }
    }
}

