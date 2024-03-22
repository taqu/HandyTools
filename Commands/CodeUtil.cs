using EnvDTE;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.VCCodeModel;
using System.Linq;
using System.Threading.Tasks;

namespace HandyTools.Commands
{
	internal static class CodeUtil
	{
#if false
		private static readonly vsCMElement[] AcceptElements = {
			vsCMElement.vsCMElementOther,
			vsCMElement.vsCMElementClass,
			vsCMElement.vsCMElementFunction,
			vsCMElement.vsCMElementVariable,
			vsCMElement.vsCMElementNamespace,
			vsCMElement.vsCMElementParameter,
			vsCMElement.vsCMElementEnum,
			vsCMElement.vsCMElementStruct,
			vsCMElement.vsCMElementUnion,
			vsCMElement.vsCMElementLocalDeclStmt,
			vsCMElement.vsCMElementFunctionInvokeStmt,
			vsCMElement.vsCMElementAssignmentStmt,
			vsCMElement.vsCMElementDefineStmt,
			vsCMElement.vsCMElementTypeDef,
			vsCMElement.vsCMElementIncludeStmt,
			vsCMElement.vsCMElementMacro,
		};
#endif

		private static readonly vsCMElement[] IgnoredElements = {
			vsCMElement.vsCMElementVCBase,
		};

		public static async Task<string> GetDefinitionCodeAsync()
		{
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
			HandyToolsPackage package;
			if (!HandyToolsPackage.Package.TryGetTarget(out package))
			{
				return null;
			}

			DocumentView documentView = await VS.Documents.GetActiveDocumentViewAsync();
			SnapshotSpan selection = documentView.TextView.Selection.SelectedSpans.FirstOrDefault();
			ITextBuffer textBuffer = documentView.TextView.TextBuffer;
			ITextSnapshotLine line;
			if (selection.Length <= 0)
			{
				line = textBuffer.CurrentSnapshot.GetLineFromPosition(selection.Start.Position);
				SnapshotSpan snapshotSpan = new SnapshotSpan(line.Start, line.End);
				documentView.TextView.Selection.Select(snapshotSpan, false);
				selection = documentView.TextView.Selection.SelectedSpans.FirstOrDefault();
			}
			line = textBuffer.CurrentSnapshot.GetLineFromPosition(selection.Start.Position);
			if (line.Length <= 0)
			{
				return null;
			}
			ProjectItem projectItem = package.DTE.ActiveDocument.ProjectItem;
			if (null == projectItem)
			{
				return null;
			}
			FileCodeModel fileCodeModel = projectItem.FileCodeModel;
			CodeElement codeElement = FindCodeElement(fileCodeModel.CodeElements, selection);
			string lineString = line.GetText();
			if (null == codeElement)
			{
				return null;
			}
			else
			{
				VCCodeFunction codeFunction = codeElement as VCCodeFunction;
				bool needClose = false;
				if (null == codeFunction.ProjectItem.Document)
				{
					needClose = true;
					codeFunction.ProjectItem.Open(EnvDTE.Constants.vsViewKindCode);
				}
				Document document = codeFunction.ProjectItem.Document;
				TextDocument textDocument = document.Object("TextDocument") as EnvDTE.TextDocument;
				if (null == textDocument)
				{
					return null;
				}
				EditPoint startPoint = textDocument.CreateEditPoint(codeFunction.get_StartPointOf(vsCMPart.vsCMPartWholeWithAttributes, vsCMWhere.vsCMWhereDefinition));
				string textCode = startPoint.GetText(codeFunction.get_EndPointOf(vsCMPart.vsCMPartWholeWithAttributes, vsCMWhere.vsCMWhereDefinition));
				if (needClose)
				{
					document.Close();
				}
				return textCode;
			}
		}

		public static CodeElement FindCodeElement(CodeElements elements, SnapshotSpan selection)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			//EnvDTE.TextPoint has one base offset, so offset selection points.
			int selectionStart = selection.Start.Position + 1;
			int selectionEnd = selection.End.Position + 1;
			foreach (CodeElement codeElement in elements)
			{
				if (0 <= Array.IndexOf(IgnoredElements, codeElement.Kind))
				{
					continue;
				}
				if (codeElement.Kind != vsCMElement.vsCMElementFunction || !(codeElement is VCCodeFunction))
				{
					CodeElement recurse = FindCodeElementRecursive(codeElement.Children, selectionStart, selectionEnd);
					if (null != recurse)
					{
						return recurse;
					}
					continue;
				}
				VCCodeFunction codeFunction = codeElement as VCCodeFunction;
				TextPoint startPoint = codeFunction.get_StartPointOf(vsCMPart.vsCMPartWholeWithAttributes, vsCMWhere.vsCMWhereDeclaration);
				TextPoint endPoint = codeFunction.get_EndPointOf(vsCMPart.vsCMPartWholeWithAttributes, vsCMWhere.vsCMWhereDeclaration);
				if (endPoint.AbsoluteCharOffset < selectionStart)
				{
					continue;
				}
				if (selectionEnd < startPoint.AbsoluteCharOffset)
				{
					continue;
				}
				return codeElement;
			}
			return null;
		}

		public static CodeElement FindCodeElementRecursive(CodeElements elements, int selectionStart, int selectionEnd)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			if (null == elements)
			{
				return null;
			}
			foreach (CodeElement codeElement in elements)
			{
				if (0 <= Array.IndexOf(IgnoredElements, codeElement.Kind))
				{
					continue;
				}
				if (codeElement.Kind != vsCMElement.vsCMElementFunction || !(codeElement is VCCodeFunction))
				{
					CodeElement recurse = FindCodeElementRecursive(codeElement.Children, selectionStart, selectionEnd);
					if (null != recurse)
					{
						return recurse;
					}
					continue;
				}
				VCCodeFunction codeFunction = codeElement as VCCodeFunction;
				TextPoint startPoint = codeFunction.get_StartPointOf(vsCMPart.vsCMPartWholeWithAttributes, vsCMWhere.vsCMWhereDeclaration);
				TextPoint endPoint = codeFunction.get_EndPointOf(vsCMPart.vsCMPartWholeWithAttributes, vsCMWhere.vsCMWhereDeclaration);
				if (endPoint.AbsoluteCharOffset < selectionStart)
				{
					continue;
				}
				if (selectionEnd < startPoint.AbsoluteCharOffset)
				{
					continue;
				}
				return codeElement;
			}
			return null;
		}

		public static string ExtractDoxygenComment(string response)
		{
			int start;
			start = response.IndexOf("/**");
			if (start < 0)
			{
				start = response.IndexOf("/*!");
			}
			if (start < 0)
			{
				return string.Empty;
			}
			int end = response.IndexOf("*/", start);
			if (end < 0)
			{
				return string.Empty;
			}
			return response.Substring(start, end-start+"*/".Length);
		}
	}
}
