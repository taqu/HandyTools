using Community.VisualStudio.Toolkit;
using EnvDTE;
using HandyTools.Commands;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HandyTools.Completion
{
	[Export(typeof(IVsTextViewCreationListener))]
	[Name("AICompletionHandler")]
	[ContentType("text")]
	[TextViewRole(PredefinedTextViewRoles.Document)]
	internal class AICompletionCommandHandlerProvider : IVsTextViewCreationListener
	{
		[Import]
		internal IVsEditorAdaptersFactoryService AdapterService = null;
		[Import]
		internal ICompletionBroker CompletionBroker { get; set; }
		[Import]
		internal SVsServiceProvider ServiceProvider { get; set; }

		[Import]
		internal ITextDocumentFactoryService documentFactory = null;

		public void VsTextViewCreated(IVsTextView textViewAdapter)
		{
			ITextView textView = AdapterService.GetWpfTextView(textViewAdapter);
			if(null == textView)
			{
				return;
			}
			Func<AICompletionCommandHandler> createCommandHandler = delegate () { return new AICompletionCommandHandler(textViewAdapter, textView, this); };
			textView.Properties.GetOrCreateSingletonProperty(createCommandHandler);

			//SetEncoding(textView);
		}

		private void SetEncoding(ITextView textView)
		{
			HandyToolsPackage package;
			if (!HandyToolsPackage.Package.TryGetTarget(out package))
			{
				return;
			}

			ITextDocument textDocument;
			if (!textView.TextBuffer.Properties.TryGetProperty(typeof(ITextDocument), out textDocument))
			{
				textView.TextDataModel.DocumentBuffer.Properties.TryGetProperty(typeof(ITextDocument), out textDocument);
			}
			if (null == textDocument)
			{
				return;
			}

			string documentPath = textDocument.FilePath;
			SettingFile.TextSettings textSettings = package.GetTextSettings(documentPath);
			EnvDTE80.DTE2 dte2 = package.DTE;
			foreach(EnvDTE.Document document in dte2.Documents)
			{
				Log.Output(document.Path + "\n");
			}
			//Check current encoding
			UtfUnknown.DetectionDetail charsetResult;
			try
			{
				charsetResult = UtfUnknown.CharsetDetector.DetectFromFile(documentPath).Detected;
			}
			catch
			{
				return;
			}

			//Overwrite if needed
			bool write = false;
			if (0.5f <= charsetResult.Confidence)
			{
				switch (textSettings.Encoding)
				{
					case Types.TypeEncoding.UTF8:
						if (charsetResult.HasBOM || (System.Text.Encoding.UTF8 != charsetResult.Encoding && System.Text.Encoding.ASCII != charsetResult.Encoding))
						{
							write = true;
						}
						break;
					case Types.TypeEncoding.UTF8BOM:
						if (!charsetResult.HasBOM || (System.Text.Encoding.UTF8 != charsetResult.Encoding && System.Text.Encoding.ASCII != charsetResult.Encoding))
						{
							write = true;
						}
						break;
				}
			}
			else
			{
				write = true;
			}

#if false
			if (write)
			{
				Types.TypeLanguage language = CodeUtil.GetLanguageFromDocument(document);
				try
				{
					EnvDTE.EditPoint editPoint = textDocument.StartPoint.CreateEditPoint();
					string text = editPoint.GetText(textDocument.EndPoint);
					UTF8Encoding utf8Encoding = new UTF8Encoding(encoding == Types.TypeEncoding.UTF8BOM);
					File.WriteAllText(document.FullName, text, utf8Encoding);

				}
				catch
				{
				}
			}
#endif
		}
	}
}
