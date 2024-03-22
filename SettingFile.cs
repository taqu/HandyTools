using System.ComponentModel;
using System.IO;
using System.Xml;
using static HandyTools.Types;

namespace HandyTools
{
    public class SettingFile
    {
        public const string FileName = "_handytools.xml";

        public Types.TypeLineFeed Get(Types.TypeLanguage language)
        {
            return lineFeeds_[(int)language];
        }

        public Types.TypeEncoding Encoding { get => encoding_; }

        public TypeAIAPI APIType
        {
            get { return typeAIAPI_; }
            set { typeAIAPI_ = value; }
        }

        public TypeAIModel AIModel
        {
            get { return typeAIModel_; }
            set { typeAIModel_ = value; }
        }

        public string ModelGeneral
        {
            get { return modelGeneral_; }
            set { modelGeneral_ = value; }
        }

        public string ModelGeneration
        {
            get { return modelGeneration_; }
            set { modelGeneration_ = value; }
        }

        public string ModelTranslation
        {
            get { return modelTranslation_; }
            set { modelTranslation_ = value; }
        }

        public string ApiKey
        {
            get { return apiKey_; }
            set { apiKey_ = value; }
        }

        public string ApiEndpoint
        {
            get { return apiEndpoint_; }
            set { apiEndpoint_ = value; }
        }

        public bool FormatResponse
        {
            get { return formatResponse_; }
            set { formatResponse_ = value; }
        }

        public float Temperature
        {
            get { return temperature_; }
            set { temperature_ = value; }
        }

        public int MaxTextLength
        {
            get { return maxTextLength_; }
            set { maxTextLength_ = value; }
        }

        public int Timeout
        {
            get { return timeout_; }
            set { timeout_ = value; }
        }

        public string PromptCompletion
        {
            get { return promptCompletion_; }
            set { promptCompletion_ = value; }
        }

        public string PromptExplanation
        {
            get { return promptExplanation_; }
            set { promptExplanation_ = value; }
        }

        public string PromptTranslation
        {
            get { return promptTranslation_; }
            set { promptTranslation_ = value; }
        }

        public string PromptDocumentation
        {
            get { return promptDocumentation_; }
            set { promptDocumentation_ = value; }
        }

        public string GetModelName(Types.TypeOllamaModel type)
        {
            switch (type)
            {
            case TypeOllamaModel.General:
                return ModelGeneral;
            case TypeOllamaModel.Generation:
                return ModelGeneration;
            case TypeOllamaModel.Translation:
                return ModelTranslation;
            default:
                return ModelGeneral;
            }
        }

        private TypeAIAPI typeAIAPI_;
        private TypeAIModel typeAIModel_;
        private string modelGeneral_ = "llama2";
        private string modelGeneration_ = "llama2";
        private string modelTranslation_ = "llama2";
        private string apiKey_ = string.Empty;
        private string apiEndpoint_ = string.Empty;
        private bool formatResponse_ = false;
        private float temperature_ = 0.1f;
        private int maxTextLength_ = 4096;
        private int timeout_ = 30;
        private string promptCompletion_ = DefaultPrompts.PromptCompletion;
        private string promptExplanation_ = DefaultPrompts.PromptExplanation;
        private string promptTranslation_ = DefaultPrompts.PromptTranslation;
        private string promptDocumentation_ = DefaultPrompts.PromptDocumentation;

        private XmlNode FindChild(XmlNode node, string name)
        {
            foreach (XmlNode child in node.ChildNodes)
            {
                if (child.Name == name)
                {
                    return child;
                }
            }
            return null;
        }

        /// <summary>
        /// Search and load a setting file
        /// </summary>
        /// <param name="package"></param>
        /// <param name="documentPath"></param>
        /// <returns></returns>
        public bool Load(WeakReference<HandyToolsPackage> package, string documentPath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            string directoryPath = System.IO.Path.GetDirectoryName(documentPath);
            if (string.IsNullOrEmpty(directoryPath) || !System.IO.Directory.Exists(directoryPath))
            {
                SetFromSetting(package);
                return false;
            }
            string filepath = string.Empty;
            if (!directoryToFile_.TryGetValue(directoryPath, out filepath) || !System.IO.File.Exists(filepath))
            {
                Log.Output(string.Format("search from {0}\n", directoryPath));
                filepath = FindFile(directoryPath);
                if (string.IsNullOrEmpty(filepath) || !System.IO.File.Exists(filepath))
                {
                    SetFromSetting(package);
                    return false;
                }
                AddToCache(directoryPath, filepath);
            }

            DateTime lastWriteTime = System.IO.File.GetLastWriteTime(filepath);
            Log.Output(string.Format("{0}: last write time {1}<={2}\n", filepath, lastWriteTime, lastWriteTime_));
            if (lastWriteTime <= lastWriteTime_)
            {
                return true;
            }

            XmlReaderSettings settings = new XmlReaderSettings();
            settings.IgnoreWhitespace = true;
            settings.IgnoreComments = true;
            try
            {
                Types.TypeLineFeed[] lineFeeds = new Types.TypeLineFeed[Types.NumLanguages] { Types.TypeLineFeed.LF, Types.TypeLineFeed.LF, Types.TypeLineFeed.LF };
                Types.TypeEncoding codeEncoding = Types.TypeEncoding.UTF8;
                XmlDocument document = new XmlDocument();
                document.Load(filepath);
                XmlNode root = FindChild(document, "HandyTools");
                XmlNode node;
                node = FindChild(root, "UnifyLineFeed");
                if (null != node)
                {
                    Log.Output("ReadCode\n");
                    foreach (XmlNode child in node.ChildNodes)
                    {
                        if ("Code" != child.Name)
                        {
                            continue;
                        }
                        if (child.Attributes.Count <= 0)
                        {
                            continue;
                        }
                        XmlAttribute attribute = child.Attributes["lang"];
                        if (null == attribute)
                        {
                            continue;
                        }
                        string lang = attribute.Value;
                        if (string.IsNullOrEmpty(lang))
                        {
                            continue;
                        }
                        lang = lang.Trim();
                        Log.Output("  Language " + lang + "\n");
                        Types.TypeLanguage typeLanguage = Types.TypeLanguage.C_Cpp;
                        switch (lang)
                        {
                        case "C/C++":
                            typeLanguage = Types.TypeLanguage.C_Cpp;
                            break;
                        case "CSharp":
                            typeLanguage = Types.TypeLanguage.CSharp;
                            break;
                        case "Others":
                            typeLanguage = Types.TypeLanguage.Others;
                            break;
                        default:
                            continue;
                        }
                        string code = child.InnerText.Trim();
                        Log.Output("  code " + code + "\n");
                        Types.TypeLineFeed typeLineFeed = Types.TypeLineFeed.LF;
                        switch (code)
                        {
                        case "LF":
                            typeLineFeed = Types.TypeLineFeed.LF;
                            break;
                        case "CR":
                            typeLineFeed = Types.TypeLineFeed.CR;
                            break;
                        case "CRLF":
                            typeLineFeed = Types.TypeLineFeed.CRLF;
                            break;
                        default:
                            continue;
                        }

                        lineFeeds[(int)typeLanguage] = typeLineFeed;
                    }
                } //if(null != node){

                // Unify Encoding
                node = FindChild(root, "UnifyEncoding");
                if (null != node)
                {
                    Log.Output("ReadEncoding\n");
                    foreach (XmlNode child in node.ChildNodes)
                    {
                        if ("Encoding" != child.Name)
                        {
                            continue;
                        }
                        string encoding = child.InnerText.Trim();
                        Log.Output("  encoding " + encoding + "\n");
                        switch (encoding)
                        {
                        case "UTF8":
                            codeEncoding = Types.TypeEncoding.UTF8;
                            break;
                        case "UTF8BOM":
                            codeEncoding = Types.TypeEncoding.UTF8BOM;
                            break;
                        default:
                            continue;
                        }
                        break;
                    }
                }

                // AI settings
                node = FindChild(root, "AI");
                if (null != node)
                {
                    Log.Output("Read AI settings\n");
                    foreach (XmlNode child in node.ChildNodes)
                    {
                        switch (child.Name)
                        {
                        case "APIType":
                        {
                            string type = child.InnerText.Trim();
                            switch (type)
                            {
                            case "OpenAI":
                                APIType = TypeAIAPI.OpenAI;
                                break;
                            case "Ollama":
                                APIType = TypeAIAPI.Ollama;
                                break;
                            default:
                                continue;
                            }
                        }
                        break;
                        case "AIModel":
                        {
                            string model = child.InnerText.Trim();
                            switch (model)
                            {
                            case "gpt-3.5-turbo":
                                AIModel = TypeAIModel.GPT_3_5_Turbo;
                                break;
                            case "gpt-35-turbo-16k":
                                AIModel = TypeAIModel.GPT_3_5_Turbo_16k;
                                break;
                            case "gpt-4":
                                AIModel = TypeAIModel.GPT_4;
                                break;
                            default:
                                continue;
                            }
                        }
                        break;
                        case "ModelGeneral":
                            ModelGeneral = child.InnerText.Trim();
                            break;
                        case "ModelGeneration":
                            ModelGeneration = child.InnerText.Trim();
                            break;
                        case "ModelTranslation":
                            ModelTranslation = child.InnerText.Trim();
                            break;
                        case "ApiKey":
                            ApiKey = child.InnerText.Trim();
                            break;
                        case "ApiEndpoint":
                            ApiEndpoint = child.InnerText.Trim();
                            break;
                        case "FormatResponse":
                        {
                            bool formatResponse = false;
                            bool.TryParse(child.InnerText.Trim().ToLower(), out formatResponse);
                            FormatResponse = formatResponse;
                        }
                        break;
                        case "Temperature":
                        {
                            float temperature = 0.0f;
                            float.TryParse(child.InnerText.Trim().ToLower(), out temperature);
                            Temperature = temperature;
                        }
                        break;
                        case "MaxTextLength":
                        {
                            int maxTextLength = 0;
                            int.TryParse(child.InnerText.Trim().ToLower(), out maxTextLength);
                            MaxTextLength = maxTextLength;
                        }
                        break;
                        case "Timeout":
                        {
                            int timeout = 0;
                            int.TryParse(child.InnerText.Trim().ToLower(), out timeout);
                            Timeout = timeout;
                        }
                        break;
                        case "PromptCompletion":
                            PromptCompletion = ParseLineFeeds(child.InnerText.Trim());
                            break;
                        case "PromptExplanation":
                            PromptExplanation = ParseLineFeeds(child.InnerText.Trim());
                            break;
                        case "PromptTranslation":
                            PromptTranslation = ParseLineFeeds(child.InnerText.Trim());
                            break;
                        case "PromptDocumentation":
                            PromptDocumentation = ParseLineFeeds(child.InnerText.Trim());
                            break;
                        }
                    }
                }
                lineFeeds_ = lineFeeds;
                encoding_ = codeEncoding;
                lastWriteTime_ = lastWriteTime;

#if DEBUG
                for (int i = 0; i < Types.NumLanguages; ++i)
                {
                    Log.Output(string.Format(" lang:{0} code:{1}\n", (Types.TypeLanguage)i, lineFeeds_[i]));
                }
            }
            catch (Exception exception)
            {
                Log.Output(exception.ToString() + "\n");
#else
            } catch {
#endif
                SetFromSetting(package);
                return false;
            }

            return true;
        }

        private string FindFile(string directory)
        {
            if (!System.IO.Directory.Exists(directory))
            {
                return null;
            }
            string filepath = directory + '\\' + FileName;
            if (System.IO.File.Exists(filepath))
            {
                return filepath;
            }
            DirectoryInfo directoryInfo = new DirectoryInfo(directory);
            foreach (DirectoryInfo child in directoryInfo.GetDirectories())
            {
                if (Array.Exists<string>(RootDirectories, element => child.Name == element))
                {
                    return null;
                }
            }
            DirectoryInfo parent = directoryInfo.Parent;
            if (null == parent)
            {
                return null;
            }
            return FindFile(parent.FullName);
        }

        private void AddToCache(string directory, string file)
        {
            if (directoryToFile_.ContainsKey(directory))
            {
                directoryToFile_.Remove(directory);
            }
            if (MaxCaches <= directoryToFile_.Count)
            {
                System.Random random = new Random();
                for (int i = 0; i < (MaxCaches / 2); ++i)
                {
                    RemoveFromCache(random.Next() % directoryToFile_.Count);
                }
            }
            directoryToFile_.Add(directory, file);
        }

        private void RemoveFromCache(int index)
        {
            int count = 0;
            foreach (string key in directoryToFile_.Keys)
            {
                if (count == index)
                {
                    directoryToFile_.Remove(key);
                    return;
                }
                ++count;
            }
        }

        private void SetFromSetting(WeakReference<HandyToolsPackage> package_)
        {
            HandyToolsPackage package;
            if (!package_.TryGetTarget(out package))
            {
                return;
            }
            Options.OptionPageHandyTools optionPage = package.Options;
            if (null != optionPage)
            {
                lineFeeds_[(int)TypeLanguage.C_Cpp] = optionPage.LineFeedCpp;
                lineFeeds_[(int)TypeLanguage.CSharp] = optionPage.LineFeedCSharp;
                lineFeeds_[(int)TypeLanguage.C_Cpp] = optionPage.LineFeedOthers;
                encoding_ = optionPage.Encoding;
            }
            Options.OptionPageHandyToolsAI optionPageAI = package.AIOptions;
            if (null != optionPageAI)
            {
                APIType = optionPageAI.APIType;
                AIModel = optionPageAI.AIModel;
                ModelGeneral = optionPageAI.ModelGeneral;
                ModelGeneration = optionPageAI.ModelGeneration;
                ModelTranslation = optionPageAI.ModelTranslation;
                ApiKey = optionPageAI.ApiKey;
                ApiEndpoint = optionPageAI.ApiEndpoint;
                FormatResponse = optionPageAI.FormatResponse;
                Temperature = optionPageAI.Temperature;
                MaxTextLength = optionPageAI.MaxTextLength;
                Timeout = optionPageAI.Timeout;
                PromptCompletion = optionPageAI.PromptCompletion;
                PromptExplanation = optionPageAI.PromptExplanation;
                PromptTranslation = optionPageAI.PromptTranslation;
            }
        }

        private string ParseLineFeeds(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }
            text = text.Replace("\\r\\n", "\n");
            text = text.Replace("\\r", "\n");
            text = text.Replace("\\n", "\n");
            return text;
        }

        private static readonly string[] RootDirectories = new string[] { ".git", ".svn" };
        private const int MaxCaches = 32;

        private Types.TypeLineFeed[] lineFeeds_ = new Types.TypeLineFeed[Types.NumLanguages] { Types.TypeLineFeed.LF, Types.TypeLineFeed.LF, Types.TypeLineFeed.LF };
        private Types.TypeEncoding encoding_ = Types.TypeEncoding.UTF8;
        private System.Collections.Generic.Dictionary<string, string> directoryToFile_ = new System.Collections.Generic.Dictionary<string, string>(MaxCaches);
        private DateTime lastWriteTime_ = new DateTime();
    }
}
