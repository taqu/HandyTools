using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandyTools.Completion
{
	[Export(typeof(IViewTaggerProvider))]
	[TagType(typeof(CompletionTag))]
	[ContentType("text")]
	internal class CompletionTaggerProvider : IViewTaggerProvider
	{
		// Disable "Field is never assigned to..." and "Field is never used" compiler's warnings. Justification: the field is used by MEF.
#pragma warning disable 649, 169

		[Export(typeof(AdornmentLayerDefinition))]
		[Name("HandyToolsAdornmentLayer")]
		[Order(After = PredefinedAdornmentLayers.Caret)]
		private AdornmentLayerDefinition editorAdornmentLayer;

#pragma warning restore 649, 169

		public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
		{
			return buffer.Properties.GetOrCreateSingletonProperty<ITagger<T>>(typeof(CompletionTagger),
				() => { return new CompletionTagger((IWpfTextView)textView, buffer) as ITagger<T>;});
		}

	}
}
