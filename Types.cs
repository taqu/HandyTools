using System.ComponentModel;

namespace HandyTools
{
    public static class Types
    {
        public enum TypeLineFeed
        {
            LF =0,
            CR,
            CRLF,
        };

        public enum TypeLanguage
        {
            [Description("C/C++")]
            C_Cpp =0,
            [Description("CSharp")]
            CSharp,
            [Description("Others")]
            Others,
        };
        public const int NumLanguages = 3;

        public enum TypeEncoding
        {
            UTF8 =0,
            UTF8BOM,
        };

        public enum TypeAIAPI
        {
            OpenAI = 0,
            Ollama,
        }

        public enum TypeResponse
        {
            Append = 0,
            Replace,
            Message,
        }

        public enum  TypeModel
        {
            General=0,
            Generation,
            Translation,
        }
    }
}
