using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

namespace HandyTools.Completion
{
	internal class CompletionTag : SpaceNegotiatingAdornmentTag
	{
		public CompletionTag(double width, double topSpace, double baseline, double textHeight, double bottomSpace, PositionAffinity affinity, object identityTag, object providerTag)
			: base(width, topSpace, baseline, textHeight, bottomSpace, affinity, identityTag, providerTag) { }
	}
}
