
using Microsoft.Build.Framework.XamlTypes;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Projection;
using Microsoft.VisualStudio.Utilities;
using System.IO;
using System.Windows.Controls;

namespace HandyTools.Completion
{
    public enum Language
    {
        None,
        Text,
        C_Cpp,
    }

    public record struct LanguageInfo(string name, Language language);

    internal class SupportedLanguage
    {
        public static LanguageInfo[] LanguageInfos { get; } = [
			new LanguageInfo("Unsupported", Language.None),
			new LanguageInfo("Plain Text", Language.Text),
            new LanguageInfo("C/C++", Language.C_Cpp),
        ];

        public static LanguageInfo GetLanguage(DocumentView documentView)
        {
            return GetLanguage(documentView.TextBuffer.ContentType);// Path.GetExtension(documentView.FilePath)?.Trim('.'));
        }

        public static LanguageInfo GetLanguage(IContentType contentType)
        {
            switch (contentType.TypeName) {
                case "Text":
					return LanguageInfos[1];
				case "C/C++":
					return LanguageInfos[2];
                default:
                    return LanguageInfos[0];
			}
        }
    }
}

