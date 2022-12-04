using System.Collections.Generic;
using System;
using System.ComponentModel;

namespace HandyTools
{
    public class OptionPageHandyTools : Microsoft.VisualStudio.Shell.DialogPage
    {
        private bool loadSettingFile_ = true;
        private Types.TypeLineFeed[] lineFeeds_ = new Types.TypeLineFeed[Types.NumLanguages] { Types.TypeLineFeed.LF, Types.TypeLineFeed.LF, Types.TypeLineFeed.LF};
        private Types.TypeEncoding encoding_ = Types.TypeEncoding.UTF8;
        private bool enableSearch_ = false;
        private string extensions_ =
                "alg as asm asp awk bas bat c cfg cgi cmd cpp css cs cxx csv clj coffee def dic dlg exp f for go h hpp hs lhs htm html inf ini inl java js latex log lsp lua sh tcl tex text txt xml xsl php vs pl ps ps1 py rb ush usm hlsl glsl";
        private HashSet<string> extensionSet_;
        private int maxSearchItems_ = 1000;
        private int updateMinInterval_ = 60*60;
        private bool selectLineWhenJumping_ = false;
        private int fuzzyMinSimilarity_ = 4;
        private bool outputDebugLog_ = false;

        [Category("General")]
        [DisplayName("Load Setting File")]
        [Description("Load \"_handytools.xml\".")]
        public bool LoadSettingFile 
        {
            get { return loadSettingFile_; }
            set { loadSettingFile_ = value; }
        }

        [Category("Unify Line Feed")]
        [DisplayName("C/C++")]
        [Description("Line feed for C/C++")]
        public Types.TypeLineFeed LineFeedCpp
        {
            get { return lineFeeds_[(int)Types.TypeLanguage.C_Cpp]; }
            set { lineFeeds_[(int)Types.TypeLanguage.C_Cpp] = value; }
        }

        [Category("Unify Line Feed")]
        [DisplayName("CSharp")]
        [Description("Line feed for CSharp")]
        public Types.TypeLineFeed LineFeedCSharp
        {
            get { return lineFeeds_[(int)Types.TypeLanguage.CSharp]; }
            set { lineFeeds_[(int)Types.TypeLanguage.CSharp] = value; }
        }

        [Category("Unify Line Feed")]
        [DisplayName("Others")]
        [Description("Line feed for Others")]
        public Types.TypeLineFeed LineFeedOthers
        {
            get { return lineFeeds_[(int)Types.TypeLanguage.Others]; }
            set { lineFeeds_[(int)Types.TypeLanguage.Others] = value; }
        }

        [Category("Unify Encoding")]
        [DisplayName("Encoding")]
        [Description("Encoding to unify")]
        public Types.TypeEncoding Encoding 
        {
            get { return encoding_; }
            set { encoding_ = value; }
        }

        [Category("Search")]
        [DisplayName("Enable Search")]
        [Description("Enable search")]
        [DefaultValue(false)]
        public bool EnableSearch
        {
            get { return enableSearch_; }
            set { enableSearch_ = value;}
        }

        [Category("Search")]
        [DisplayName("Extensions")]
        [Description("Extensions to index")]
        [DefaultValue(true)]
        public string Extensions
        {
            get { return extensions_; }
            set { extensions_ = value; extensionSet_ = null;}
        }

        [Category("Search")]
        [DisplayName("Max Search Items")]
        [Description("Max search items at a time")]
        [DefaultValue(true)]
        public int MaxSearchItems
        {
            get { return maxSearchItems_; }
            set { maxSearchItems_ = Math.Max(10, Math.Min(100000, value));}
        }

        [Category("Search")]
        [DisplayName("Index Update Minimal Interval")]
        [Description("Index update minimal interval in seconds. The index will not be updated in this interval.")]
        [DefaultValue(true)]
        public int UpdateMinInterval
        {
            get { return updateMinInterval_; }
            set { updateMinInterval_ = Math.Max(60, value);}
        }

        [Category("Search")]
        [DisplayName("Select Line When Jumping")]
        [Description("Select the line, when jumping")]
        [DefaultValue(false)]
        public bool SelectLineWhenJumping
        {
            get { return selectLineWhenJumping_; }
            set { selectLineWhenJumping_ = value;}
        }

        [Category("Search")]
        [DisplayName("Fuzzy Min Similarity")]
        [Description("Min similarity for fuzzy searching")]
        [DefaultValue(false)]
        public int FuzzyMinSimilarity
        {
            get { return fuzzyMinSimilarity_; }
            set { fuzzyMinSimilarity_ = Math.Min(Math.Max(0, value), 32);}
        }

        [Category("Debug")]
        [DisplayName("Debug Log")]
        [Description("Output debug logs")]
        [DefaultValue(false)]
        public bool OutputDebugLog
        {
            get { return outputDebugLog_; }
            set { outputDebugLog_ = value;}
        }

        [Browsable(false)]
        public HashSet<string> ExtensionSet
        {
            get
            {
                if(null == extensionSet_) {
                    Rebuild();
                }
                return extensionSet_;
            }
        }

        private void Rebuild()
        {
            extensionSet_ = new HashSet<string>(128);
            string[] tokens = extensions_.Split(' ', ',', '|', '　', '\t', ':');
            if(null == tokens) {
                return;
            }
            foreach(string token in tokens) {
                if(string.IsNullOrEmpty(token)) {
                    continue;
                }
                extensionSet_.Add(token);
            }
        }
    }
}
