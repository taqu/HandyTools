using System.Collections.Generic;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace HandyTools.Options
{
	[ComVisible(true)]
	public class OptionPageHandyTools : Microsoft.VisualStudio.Shell.DialogPage
    {
        private bool loadSettingFile_ = true;
        private Types.TypeLineFeed[] lineFeeds_ = new Types.TypeLineFeed[Types.NumLanguages] { Types.TypeLineFeed.LF, Types.TypeLineFeed.LF, Types.TypeLineFeed.LF};
        private Types.TypeEncoding encoding_ = Types.TypeEncoding.UTF8;
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

        [Category("Debug")]
        [DisplayName("Debug Log")]
        [Description("Output debug logs")]
        [DefaultValue(false)]
        public bool OutputDebugLog
        {
            get { return outputDebugLog_; }
            set { outputDebugLog_ = value;}
        }
    }
}
