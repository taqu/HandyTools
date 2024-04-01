using EnvDTE;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.VCCodeModel;
using System.IO;
using System.Linq;
using System.Text;
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

		public static Types.TypeLanguage GetLanguageFromDocument(EnvDTE.Document document)
		{
            ThreadHelper.ThrowIfNotOnUIThread();
            switch (document.Language) {
            case "C/C++":
                return Types.TypeLanguage.C_Cpp;
            case "CSharp":
                return Types.TypeLanguage.CSharp;
            default:
                return Types.TypeLanguage.Others;
            }
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="documentView"></param>
		/// <param name="selection"></param>
		/// <returns></returns>
		public static async Task<(string, string, int)> GetDefinitionCodeAsync(DocumentView documentView, SnapshotSpan selection)
		{
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
			HandyToolsPackage package;
			if (!HandyToolsPackage.Package.TryGetTarget(out package))
			{
				return (null, null, 0);
			}
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
				return (null, null, 0);
			}
			ProjectItem projectItem = package.DTE.ActiveDocument.ProjectItem;
			if (null == projectItem)
			{
				return (null, null, 0);
			}
			FileCodeModel fileCodeModel = projectItem.FileCodeModel;
			CodeElement codeElement = FindCodeElement(fileCodeModel.CodeElements, selection);
			if (null == codeElement)
			{
				return (null, null, 0);
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
					return (null, null, 0);
				}
				EditPoint startPoint = textDocument.CreateEditPoint(codeFunction.get_StartPointOf(vsCMPart.vsCMPartWholeWithAttributes, vsCMWhere.vsCMWhereDefinition));
				string textCode = startPoint.GetText(codeFunction.get_EndPointOf(vsCMPart.vsCMPartWholeWithAttributes, vsCMWhere.vsCMWhereDefinition));
				TextPoint declStartPoint = codeFunction.get_StartPointOf(vsCMPart.vsCMPartWholeWithAttributes, vsCMWhere.vsCMWhereDeclaration);
				//TextPoint declEndPoint = codeFunction.get_EndPointOf(vsCMPart.vsCMPartWholeWithAttributes, vsCMWhere.vsCMWhereDeclaration);
				string indent = string.Empty;
				int lineNumber = 0;
                if (null != declStartPoint && 0<declStartPoint.LineCharOffset)
                {
                    EditPoint editPoint = declStartPoint.CreateEditPoint();
                    string lineString = editPoint.GetLines(declStartPoint.Line, declStartPoint.Line + 1);
					indent = lineString.Substring(0, declStartPoint.LineCharOffset-1);
					lineNumber = textBuffer.CurrentSnapshot.GetLineNumberFromPosition(declStartPoint.AbsoluteCharOffset+1);

				}
				if (needClose)
				{
					document.Close();
				}
				return (textCode, indent, lineNumber);
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

		public static string AddIndent(string text, string indent, Types.TypeLineFeed typeLineFeed)
		{
			StringBuilder stringBuilder = new StringBuilder();
			using(TextReader reader = new StringReader(text))
			{
				int count = 0;
				while (true) {
				string line = reader.ReadLine();
					if(null == line)
					{
						break;
					}
					if (0 < count)
					{
                        switch (typeLineFeed)
                        {
                        case Types.TypeLineFeed.LF:
                            stringBuilder.Append('\n');
                            break;
                        case Types.TypeLineFeed.CR:
                            stringBuilder.Append('\r');
                            break;
                        case Types.TypeLineFeed.CRLF:
                            stringBuilder.Append("\r\n");
                            break;
                        }
                    }
					++count;
                    line = line.TrimStart();
					stringBuilder.Append(indent);
					stringBuilder.Append(line);
				}
			}
			return stringBuilder.ToString();
		}

		public static string ExtractDoxygenComment(string response, string indent, Types.TypeLineFeed typeLineFeed)
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
			return AddIndent(response.Substring(start, end-start+"*/".Length), indent, typeLineFeed);
		}

		private const string UnrealMacro0 = "UFUNCTION";
		public static ITextSnapshotLine GetCommentInsertionLineFromPosition(DocumentView documentView, int declStartLine)
		{
			ITextBuffer textBuffer = documentView.TextView.TextBuffer;
			ITextSnapshotLine line = textBuffer.CurrentSnapshot.GetLineFromLineNumber(declStartLine);
			if (line.LineNumber <= 0)
			{
				return line;
			}
			ITextSnapshotLine upperLine = textBuffer.CurrentSnapshot.GetLineFromLineNumber(line.LineNumber - 1);
			if (upperLine.GetText().Contains(UnrealMacro0))
			{
				return upperLine;
			}
			return line;
		}
	}
}
