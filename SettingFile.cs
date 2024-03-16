using System.IO;
using System.Xml;

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

        private XmlNode FindChild(XmlNode node, string name)
        {
            foreach(XmlNode child in node.ChildNodes) {
                if(child.Name == name) {
                    return child;
                }
            }
            return null;
        }

        /// <summary>
        /// Search and load a setting file
        /// </summary>
        /// <param name="dte"></param>
        /// <param name="documentPath"></param>
        /// <returns></returns>
        public bool Load(EnvDTE80.DTE2 dte, string documentPath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            string directoryPath = System.IO.Path.GetDirectoryName(documentPath);
            if(string.IsNullOrEmpty(directoryPath) || !System.IO.Directory.Exists(directoryPath)) {
                return false;
            }
            string filepath = string.Empty;
            if(!directoryToFile_.TryGetValue(directoryPath, out filepath) || !System.IO.File.Exists(filepath)) {
                if(null == dte) {
                    return false;
                }
                Log.Output(string.Format("search from {0}\n", directoryPath));
                filepath = FindFile(directoryPath);
                if(string.IsNullOrEmpty(filepath) || !System.IO.File.Exists(filepath)) {
                    return false;
                }
                AddToCache(directoryPath, filepath);
            }

            DateTime lastWriteTime = System.IO.File.GetLastWriteTime(filepath);
            Log.Output(string.Format("{0}: last write time {1}<={2}\n", filepath, lastWriteTime, lastWriteTime_));
            if(lastWriteTime <= lastWriteTime_) {
                return true;
            }

            XmlReaderSettings settings = new XmlReaderSettings();
            settings.IgnoreWhitespace = true;
            settings.IgnoreComments = true;
            try {
                Types.TypeLineFeed[] lineFeeds = new Types.TypeLineFeed[Types.NumLanguages] { Types.TypeLineFeed.LF, Types.TypeLineFeed.LF, Types.TypeLineFeed.LF };
                Types.TypeEncoding codeEncoding = Types.TypeEncoding.UTF8;
                XmlDocument document = new XmlDocument();
                document.Load(filepath);
                XmlNode root = FindChild(document, "HandyTools");
                XmlNode node;
                node = FindChild(root, "UnifyLineFeed");
                if(null != node) {
                    Log.Output("ReadCode\n");
                    foreach(XmlNode child in node.ChildNodes) {
                        if("Code" != child.Name) {
                            continue;
                        }
                        if(child.Attributes.Count <= 0) {
                            continue;
                        }
                        XmlAttribute attribute = child.Attributes["lang"];
                        if(null == attribute) {
                            continue;
                        }
                        string lang = attribute.Value;
                        if(string.IsNullOrEmpty(lang)) {
                            continue;
                        }
                        lang = lang.Trim();
                        Log.Output("  Language " + lang + "\n");
                        Types.TypeLanguage typeLanguage = Types.TypeLanguage.C_Cpp;
                        switch(lang) {
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
                        switch(code) {
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

                node = FindChild(root, "UnifyEncoding");
                if(null != node) {
                    Log.Output("ReadEncoding\n");
                    foreach(XmlNode child in node.ChildNodes) {
                        if("Encoding" != child.Name) {
                            continue;
                        }
                        string encoding = child.InnerText.Trim();
                        Log.Output("  encoding " + encoding + "\n");
                        switch(encoding) {
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
                lineFeeds_ = lineFeeds;
                encoding_ = codeEncoding;
                lastWriteTime_ = lastWriteTime;

#if DEBUG
                for(int i = 0; i < Types.NumLanguages; ++i) {
                    Log.Output(string.Format(" lang:{0} code:{1}\n", (Types.TypeLanguage)i, lineFeeds_[i]));
                }
            } catch(Exception exception) {
                Log.Output(exception.ToString() + "\n");
#else
            } catch {
#endif

                return false;
            }

            return true;
        }

        private string FindFile(string directory)
        {
            if(!System.IO.Directory.Exists(directory)) {
                return null;
            }
            string filepath = directory + '\\' + FileName;
            if(System.IO.File.Exists(filepath)) {
                return filepath;
            }
            DirectoryInfo directoryInfo = new DirectoryInfo(directory);
            foreach(DirectoryInfo child in directoryInfo.GetDirectories()) {
                if(Array.Exists<string>(RootDirectories, element => child.Name == element)) {
                    return null;
                }
            }
            DirectoryInfo parent = directoryInfo.Parent;
            if(null == parent) {
                return null;
            }
            return FindFile(parent.FullName);
        }

        private void AddToCache(string directory, string file)
        {
            if(directoryToFile_.ContainsKey(directory)) {
                directoryToFile_.Remove(directory);
            }
            if(MaxCaches <= directoryToFile_.Count) {
                System.Random random = new Random();
                for(int i = 0; i < 64; ++i) {
                    RemoveFromCache(random.Next() % directoryToFile_.Count);
                }
            }
            directoryToFile_.Add(directory, file);
        }

        private void RemoveFromCache(int index)
        {
            int count = 0;
            foreach(string key in directoryToFile_.Keys) {
                if(count == index) {
                    directoryToFile_.Remove(key);
                    return;
                }
                ++count;
            }
        }

        private static readonly string[] RootDirectories = new string[] { ".git", ".svn" };
        private const int MaxCaches = 128;

        private Types.TypeLineFeed[] lineFeeds_ = new Types.TypeLineFeed[Types.NumLanguages] { Types.TypeLineFeed.LF, Types.TypeLineFeed.LF, Types.TypeLineFeed.LF };
        private Types.TypeEncoding encoding_ = Types.TypeEncoding.UTF8;
        private System.Collections.Generic.Dictionary<string, string> directoryToFile_ = new System.Collections.Generic.Dictionary<string, string>(MaxCaches);
        private DateTime lastWriteTime_ = new DateTime();
    }
}
