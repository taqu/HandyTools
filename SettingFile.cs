using System.ComponentModel;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml;
using static HandyTools.Types;

namespace HandyTools
{
	public class SettingFile
	{
		public struct TextSettings
		{
			public TextSettings()
			{
				lineFeeds_ = new Types.TypeLineFeed[Types.NumLanguages] { Types.TypeLineFeed.LF, Types.TypeLineFeed.LF, Types.TypeLineFeed.LF };
				encoding_ = Types.TypeEncoding.UTF8;
			}

			public TextSettings(SettingFile settings)
			{
				lineFeeds_ = settings.lineFeeds_;
				encoding_ = settings.encoding_;
			}

			public Types.TypeLineFeed Get(Types.TypeLanguage language)
			{
				return lineFeeds_[(int)language];
			}

			public Types.TypeEncoding Encoding { get => encoding_; }

			private Types.TypeLineFeed[] lineFeeds_;
			private Types.TypeEncoding encoding_;
		};

		public struct AIModelSettings
		{
			public AIModelSettings()
			{
				lineFeeds_ = new Types.TypeLineFeed[Types.NumLanguages] { Types.TypeLineFeed.LF, Types.TypeLineFeed.LF, Types.TypeLineFeed.LF };
				encoding_ = Types.TypeEncoding.UTF8;
			}

			public AIModelSettings(SettingFile settings)
			{
				lineFeeds_ = settings.lineFeeds_;
				encoding_ = settings.encoding_;

				modelGeneral_ = settings.modelGeneral_;
				modelGeneration_ = settings.modelGeneration_;
				modelTranslation_ = settings.modelTranslation_;
				apiKey_ = settings.apiKey_;
				apiEndpoint_ = settings.apiEndpoint_;
				completionEndpoint_ = settings.completionEndpoint_;
				formatResponse_ = settings.formatResponse_;
				temperature_ = settings.temperature_;
				maxTextLength_ = settings.maxTextLength_;
				timeout_ = settings.timeout_;
				realTimeCompletion_ = settings.realTimeCompletion_;
				maxCompletionInputSize_ = settings.maxCompletionInputSize_;
				completionIntervalInMilliseconds_ = settings.completionIntervalInMilliseconds_;
				maxCompletionOutputSize_ = settings.maxCompletionOutputSize_;
				promptCompletion_ = settings.promptCompletion_;
				promptExplanation_ = settings.promptExplanation_;
				promptTranslation_ = settings.promptTranslation_;
				promptDocumentation_ = settings.promptDocumentation_;
			}

			public Types.TypeLineFeed Get(Types.TypeLanguage language)
			{
				return lineFeeds_[(int)language];
			}

			public Types.TypeEncoding Encoding { get => encoding_; }

			public string GetAPIEndpoint(Types.TypeModel type)
			{
				switch (type)
				{
					case TypeModel.General:
						return apiEndpoint_;
					case TypeModel.Generation:
						return completionEndpoint_;
					case TypeModel.Translation:
						return apiEndpoint_;
					default:
						return apiEndpoint_;
				}
			}

			public string GetModelName(Types.TypeModel type)
			{
				switch (type)
				{
					case TypeModel.General:
						return ModelGeneral;
					case TypeModel.Generation:
						return ModelGeneration;
					case TypeModel.Translation:
						return ModelTranslation;
					default:
						return ModelGeneral;
				}
			}

			public string ModelGeneral
			{
				get { return modelGeneral_; }
			}

			public string ModelGeneration
			{
				get { return modelGeneration_; }
			}

			public string ModelTranslation
			{
				get { return modelTranslation_; }
			}

			public string ApiKey
			{
				get { return apiKey_; }
			}

			public string ApiEndpoint
			{
				get { return apiEndpoint_; }
			}

			public string CompletionEndpoint
			{
				get { return completionEndpoint_; }
			}

			public bool FormatResponse
			{
				get { return formatResponse_; }
			}

			public float Temperature
			{
				get { return temperature_; }
			}

			public int MaxTextLength
			{
				get { return maxTextLength_; }
			}

			public int Timeout
			{
				get { return timeout_; }
			}

			public bool RealTimeCompletion
			{
				get { return realTimeCompletion_; }
			}

			public int MaxCompletionInputSize
			{
				get { return maxCompletionInputSize_; }
			}

			public long CompletionIntervalInMilliseconds
			{
				get { return completionIntervalInMilliseconds_; }
			}

			public int MaxCompletionOutputSize
			{
				get { return maxCompletionOutputSize_; }
			}

			public string PromptCompletion
			{
				get { return promptCompletion_; }
			}

			public string PromptExplanation
			{
				get { return promptExplanation_; }
			}

			public string PromptTranslation
			{
				get { return promptTranslation_; }
			}

			public string PromptDocumentation
			{
				get { return promptDocumentation_; }
			}

			private Types.TypeLineFeed[] lineFeeds_;
			private Types.TypeEncoding encoding_;

			private string modelGeneral_ = "llama2";
			private string modelGeneration_ = "llama2";
			private string modelTranslation_ = "llama2";
			private string apiKey_ = "XXX";
			private string apiEndpoint_ = string.Empty;
			private string completionEndpoint_ = string.Empty;
			private bool formatResponse_ = false;
			private float temperature_ = 0.1f;
			private int maxTextLength_ = 4096;
			private int timeout_ = 30;
			private bool realTimeCompletion_ = false;
			private int maxCompletionInputSize_ = 4096;
			private long completionIntervalInMilliseconds_ = 1000;
			private int maxCompletionOutputSize_ = 64;
			private string promptCompletion_ = DefaultPrompts.PromptCompletion;
			private string promptExplanation_ = DefaultPrompts.PromptExplanation;
			private string promptTranslation_ = DefaultPrompts.PromptTranslation;
			private string promptDocumentation_ = DefaultPrompts.PromptDocumentation;
		};

		public const string FileName = "_handytools.xml";

		public bool LoadSettingFile
		{
			get
			{
				return loadSettingFile_;
			}
		}

		public bool OutputDebugLog
		{
			get
			{
				return outputDebugLog_;
			}
		}
		public Types.TypeLineFeed Get(Types.TypeLanguage language)
		{
			return lineFeeds_[(int)language];
		}

		public Types.TypeEncoding Encoding { get => encoding_; }

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

		public string CompletionEndpoint
		{
			get { return completionEndpoint_; }
			set { completionEndpoint_ = value; }
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

		public bool RealTimeCompletion
		{
			get { return realTimeCompletion_; }
			set { realTimeCompletion_ = value; }
		}

		public long CompletionIntervalInMilliseconds
		{
			get { return completionIntervalInMilliseconds_; }
			set { completionIntervalInMilliseconds_ = value; }
		}

		public int MaxCompletionInputSize
		{
			get { return maxCompletionInputSize_; }
			set { maxCompletionInputSize_ = value;}
		}

		public int MaxCompletionOutputSize
		{
			get { return maxCompletionOutputSize_; }
			set { maxCompletionOutputSize_ = value; }
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

		public string GetModelName(Types.TypeModel type)
		{
			switch (type)
			{
				case TypeModel.General:
					return ModelGeneral;
				case TypeModel.Generation:
					return ModelGeneration;
				case TypeModel.Translation:
					return ModelTranslation;
				default:
					return ModelGeneral;
			}
		}

		private string modelGeneral_ = "llama2";
		private string modelGeneration_ = "llama2";
		private string modelTranslation_ = "llama2";
		private string apiKey_ = "XXX";
		private string apiEndpoint_ = string.Empty;
		private string completionEndpoint_ = string.Empty;
		private bool formatResponse_ = false;
		private float temperature_ = 0.1f;
		private int maxTextLength_ = 4096;
		private int timeout_ = 30;
		private bool realTimeCompletion_;
		private long completionIntervalInMilliseconds_ = 1000;
		private int maxCompletionInputSize_ = 4096;
		private int maxCompletionOutputSize_ = 64;
		private string promptCompletion_ = DefaultPrompts.PromptCompletion;
		private string promptExplanation_ = DefaultPrompts.PromptExplanation;
		private string promptTranslation_ = DefaultPrompts.PromptTranslation;
		private string promptDocumentation_ = DefaultPrompts.PromptDocumentation;

		private static XmlNode FindChild(XmlNode node, string name)
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

		public static string GetFilePath(string directoryPath)
		{
			System.Diagnostics.Debug.Assert(null != directoryPath);
			string filepath = System.IO.Path.Combine(directoryPath, FileName);
			if (System.IO.File.Exists(filepath))
			{
				return filepath;
			}
			DirectoryInfo directoryInfo = new DirectoryInfo(directoryPath);
			foreach (DirectoryInfo child in directoryInfo.GetDirectories())
			{
				if (RootDirectories.Contains(child.Name))
				{
					return null;
				}
			}
			if (null == directoryInfo.Parent)
			{
				return null;
			}
			return GetFilePath(directoryInfo.Parent.FullName);
		}

		/// <summary>
		/// Search and load a setting file
		/// </summary>
		/// <param name="package"></param>
		/// <param name="documentPath"></param>
		/// <returns></returns>
		public static SettingFile Load(WeakReference<HandyToolsPackage> package, string documentPath)
		{
			string directoryPath = System.IO.Path.GetDirectoryName(documentPath);
			SettingFile settingFile = SetFromSetting(package);
			if (!settingFile.LoadSettingFile || string.IsNullOrEmpty(directoryPath) || !System.IO.Directory.Exists(directoryPath))
			{
				return settingFile;
			}
			string filepath = GetFilePath(directoryPath);
			if (string.IsNullOrEmpty(filepath) || !System.IO.File.Exists(filepath))
			{
				return settingFile;
			}
			CacheEntry cacheEntry = new CacheEntry();
			FileInfo fileInfo = new FileInfo(filepath);
			if (fileToSettings_.TryGetValue(filepath, out cacheEntry))
			{
				if (fileInfo.LastWriteTime <= cacheEntry.lastWriteTime_)
				{
					return cacheEntry.settingFile_;
				}
				fileToSettings_.Remove(filepath);
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
							case "ModelGeneral":
								settingFile.ModelGeneral = child.InnerText.Trim();
								break;
							case "ModelGeneration":
								settingFile.ModelGeneration = child.InnerText.Trim();
								break;
							case "ModelTranslation":
								settingFile.ModelTranslation = child.InnerText.Trim();
								break;
							case "ApiKey":
								settingFile.ApiKey = child.InnerText.Trim();
								break;
							case "ApiEndpoint":
								settingFile.ApiEndpoint = child.InnerText.Trim();
								break;
							case "CompletionEndpoint":
								settingFile.CompletionEndpoint = child.InnerText.Trim();
								break;
							case "FormatResponse":
								{
									bool formatResponse = false;
									bool.TryParse(child.InnerText.Trim().ToLower(), out formatResponse);
									settingFile.FormatResponse = formatResponse;
								}
								break;
							case "Temperature":
								{
									float temperature = 0.0f;
									float.TryParse(child.InnerText.Trim().ToLower(), out temperature);
									settingFile.Temperature = temperature;
								}
								break;
							case "MaxTextLength":
								{
									int maxTextLength = 2000;
									int.TryParse(child.InnerText.Trim().ToLower(), out maxTextLength);
									settingFile.MaxTextLength = maxTextLength;
								}
								break;
							case "Timeout":
								{
									int timeout = 30;
									int.TryParse(child.InnerText.Trim().ToLower(), out timeout);
									settingFile.Timeout = timeout;
								}
								break;
							case "RealTimeCompletion":
								{
									bool realTimeCompletion = false;
									bool.TryParse(child.InnerText.Trim().ToLower(), out realTimeCompletion);
									settingFile.RealTimeCompletion = realTimeCompletion;
								}
								break;
							case "CompletionIntervalInMilliseconds":
								{
									long completionIntervalInMilliseconds = 1000;
									long.TryParse(child.InnerText.Trim().ToLower(), out completionIntervalInMilliseconds);
									settingFile.CompletionIntervalInMilliseconds = completionIntervalInMilliseconds;
								}
								break;
							case "MaxCompletionInputSize":
								{
									int maxCompletionInputSize = 4000;
									int.TryParse(child.InnerText.Trim().ToLower(), out maxCompletionInputSize);
									if (0 < maxCompletionInputSize)
									{
										settingFile.MaxCompletionInputSize = maxCompletionInputSize;
									}
								}
								break;
							case "MaxCompletionOutputSize":
								{
									int maxCompletionOutputSize = 64;
									int.TryParse(child.InnerText.Trim().ToLower(), out maxCompletionOutputSize);
									if (0 < maxCompletionOutputSize)
									{
										settingFile.MaxCompletionOutputSize = maxCompletionOutputSize;
									}
								}
								break;
							case "PromptCompletion":
								settingFile.PromptCompletion = ParseLineFeeds(child.InnerText.Trim());
								break;
							case "PromptExplanation":
								settingFile.PromptExplanation = ParseLineFeeds(child.InnerText.Trim());
								break;
							case "PromptTranslation":
								settingFile.PromptTranslation = ParseLineFeeds(child.InnerText.Trim());
								break;
							case "PromptDocumentation":
								settingFile.PromptDocumentation = ParseLineFeeds(child.InnerText.Trim());
								break;
						}
					}
				}
				settingFile.lineFeeds_ = lineFeeds;
				settingFile.encoding_ = codeEncoding;

				AddToCache(filepath, settingFile, fileInfo.LastWriteTime);
#if DEBUG
				for (int i = 0; i < Types.NumLanguages; ++i)
				{
					Log.Output(string.Format(" lang:{0} code:{1}\n", (Types.TypeLanguage)i, settingFile.lineFeeds_[i]));
				}
			}
			catch (Exception exception)
			{
				Log.Output(exception.ToString() + "\n");
#else
            } catch {
#endif
			}

			return settingFile;
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

		private static void AddToCache(string filepath, SettingFile settingFile, DateTime lastWriteTime)
		{
			if (fileToSettings_.ContainsKey(filepath))
			{
				fileToSettings_.Remove(filepath);
			}
			if (MaxCaches <= fileToSettings_.Count)
			{
				System.Random random = new Random();
				for (int i = 0; i < (MaxCaches / 2); ++i)
				{
					RemoveFromCache(random.Next() % fileToSettings_.Count);
				}
			}
			CacheEntry entry;
			entry.lastWriteTime_ = lastWriteTime;
			entry.settingFile_ = settingFile;
			fileToSettings_.Add(filepath, entry);
		}

		private static void RemoveFromCache(int index)
		{
			int count = 0;
			foreach (string key in fileToSettings_.Keys)
			{
				if (count == index)
				{
					fileToSettings_.Remove(key);
					return;
				}
				++count;
			}
		}

		private static SettingFile SetFromSetting(WeakReference<HandyToolsPackage> package_)
		{
			SettingFile settingFile = new SettingFile();
			HandyToolsPackage package;
			if (!package_.TryGetTarget(out package))
			{
				return settingFile;
			}
			Options.OptionPageHandyTools optionPage = package.Options;
			if (null != optionPage)
			{
				settingFile.loadSettingFile_ = optionPage.LoadSettingFile;
				settingFile.outputDebugLog_ = optionPage.OutputDebugLog;
				settingFile.lineFeeds_[(int)TypeLanguage.C_Cpp] = optionPage.LineFeedCpp;
				settingFile.lineFeeds_[(int)TypeLanguage.CSharp] = optionPage.LineFeedCSharp;
				settingFile.lineFeeds_[(int)TypeLanguage.C_Cpp] = optionPage.LineFeedOthers;
				settingFile.encoding_ = optionPage.Encoding;
			}
			Options.OptionPageHandyToolsAI optionPageAI = package.AIOptions;
			if (null != optionPageAI)
			{
				settingFile.ModelGeneral = optionPageAI.ModelGeneral;
				settingFile.ModelGeneration = optionPageAI.ModelGeneration;
				settingFile.ModelTranslation = optionPageAI.ModelTranslation;
				settingFile.ApiKey = optionPageAI.ApiKey;
				settingFile.ApiEndpoint = optionPageAI.ApiEndpoint;
				settingFile.CompletionEndpoint = optionPageAI.CompletionEndpoint;
				settingFile.FormatResponse = optionPageAI.FormatResponse;
				settingFile.Temperature = optionPageAI.Temperature;
				settingFile.MaxTextLength = optionPageAI.MaxTextLength;
				settingFile.Timeout = optionPageAI.Timeout;
				settingFile.RealTimeCompletion = optionPageAI.RealTimeCompletion;
				settingFile.CompletionIntervalInMilliseconds = optionPageAI.CompletionIntervalInMilliseconds;
				settingFile.MaxCompletionInputSize = optionPageAI.MaxCompletionInputSize;
				settingFile.MaxCompletionOutputSize = optionPageAI.MaxCompletionOutputSize;
				settingFile.PromptCompletion = optionPageAI.PromptCompletion;
				settingFile.PromptExplanation = optionPageAI.PromptExplanation;
				settingFile.PromptTranslation = optionPageAI.PromptTranslation;
				settingFile.PromptDocumentation = optionPageAI.PromptDocumentation;
			}
			return settingFile;
		}

		private static string ParseLineFeeds(string text)
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
		private const int MaxCaches = 16;

		private struct CacheEntry
		{
			public DateTime lastWriteTime_;
			public SettingFile settingFile_;
		};

		private static System.Collections.Generic.Dictionary<string, CacheEntry> fileToSettings_ = new System.Collections.Generic.Dictionary<string, CacheEntry>(MaxCaches);

		private bool loadSettingFile_ = true;
		private bool outputDebugLog_ = false;
		private Types.TypeLineFeed[] lineFeeds_ = new Types.TypeLineFeed[Types.NumLanguages] { Types.TypeLineFeed.LF, Types.TypeLineFeed.LF, Types.TypeLineFeed.LF };
		private Types.TypeEncoding encoding_ = Types.TypeEncoding.UTF8;
	}
}
