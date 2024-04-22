using Community.VisualStudio.Toolkit;
using EnvDTE;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.VCCodeModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;

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

		private static readonly vsCMElement[] IgnoredElements = {
			vsCMElement.vsCMElementVCBase,
		};
#endif

		public static Types.TypeLanguage GetLanguageFromDocument(EnvDTE.Document document)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			switch (document.Language)
			{
				case "C/C++":
					return Types.TypeLanguage.C_Cpp;
				case "CSharp":
					return Types.TypeLanguage.CSharp;
				default:
					return Types.TypeLanguage.Others;
			}
		}

		public static int GetCharOffsetWithLF(ITextSnapshot textSnapshot, SnapshotSpan selection)
		{
			int targetLine = selection.Start.GetContainingLineNumber();
			int charOffset = 0;
			foreach (ITextSnapshotLine line in textSnapshot.Lines)
			{
				if(line.LineNumber == targetLine)
				{
					charOffset += selection.Start - selection.Start.GetContainingLine().Start;
					break;
				}
				charOffset += line.Length + 1;
			}
			return charOffset;
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
			ITextSnapshot textSnapshot = textBuffer.CurrentSnapshot;
			ITextSnapshotLine line = textSnapshot.GetLineFromPosition(selection.Start.Position);
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
			int selectionStart = GetCharOffsetWithLF(textSnapshot, selection);
			(VCCodeFunction, bool) result = FindCodeElement(fileCodeModel.CodeElements, selectionStart, documentView.Document.FilePath);
			VCCodeFunction codeFunction = result.Item1;
			bool declaration = result.Item2;
			if (null == codeFunction)
			{
				return (null, null, 0);
			}
			if (string.IsNullOrEmpty(codeFunction.BodyText))
			{
				return (null, null, 0);
			}

			TextPoint declStartPoint = declaration
				? codeFunction.get_StartPointOf(vsCMPart.vsCMPartWholeWithAttributes, vsCMWhere.vsCMWhereDeclaration)
				: codeFunction.get_StartPointOf(vsCMPart.vsCMPartWholeWithAttributes, vsCMWhere.vsCMWhereDefinition);
			EditPoint defineStartPoint = codeFunction.get_StartPointOf(vsCMPart.vsCMPartWholeWithAttributes, vsCMWhere.vsCMWhereDefinition).CreateEditPoint();
			TextPoint defineEndPoint = codeFunction.get_EndPointOf(vsCMPart.vsCMPartWholeWithAttributes, vsCMWhere.vsCMWhereDefinition);
#if false
			if (null == codeFunction.ProjectItem.Document)
			{
				needClose = true;
				codeFunction.ProjectItem.Open(EnvDTE.Constants.vsViewKindCode);
				if(null == codeFunction.ProjectItem.Document)
				{
					return (null, null, 0);
				}
			}
			Document document = codeFunction.ProjectItem.Document;
			TextDocument textDocument = document.Object("TextDocument") as EnvDTE.TextDocument;
#endif
			TextDocument textDocument = codeFunction.get_StartPointOf(vsCMPart.vsCMPartWholeWithAttributes, vsCMWhere.vsCMWhereDeclaration).Parent;
			if (null == textDocument)
			{
				return (null, null, 0);
			}

			string textCode = defineStartPoint.GetText(defineEndPoint);
			string indent = string.Empty;
			int lineNumber = 0;
			if (null != declStartPoint && 0 < declStartPoint.LineCharOffset)
			{
				lineNumber = Math.Max(declStartPoint.Line - 1, 0);
				string lineString = textBuffer.CurrentSnapshot.GetLineFromPosition(selection.Start.Position).GetText();
				StringBuilder stringBuilder = new StringBuilder(lineString.Length);
				foreach(char c in lineString)
				{
					if (!char.IsWhiteSpace(c))
					{
						break;
					}
					stringBuilder.Append(c);
				}
				indent = stringBuilder.ToString();
			}
			return (textCode, indent, lineNumber);
		}

		public static (VCCodeFunction, bool) FindCodeElement(CodeElements elements, int selectionStart, string filePath)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			//EnvDTE.TextPoint has one base offset, so offset selection points.
			selectionStart = selectionStart + 1;
			foreach (CodeElement codeElement in elements)
			{
				if (codeElement.Kind != vsCMElement.vsCMElementFunction || !(codeElement is VCCodeFunction))
				{
					(VCCodeFunction, bool) recurse = FindCodeElementRecursive(codeElement.Children, selectionStart, filePath);
					if (null != recurse.Item1)
					{
						return recurse;
					}
					continue;
				}
				VCCodeFunction codeFunction = codeElement as VCCodeFunction;
				bool declaration;
				TextPoint startPoint;
				TextPoint endPoint;
				if (codeFunction.DeclarationText.Contains(codeFunction.BodyText))
				{
					declaration = true;
					startPoint = codeFunction.get_StartPointOf(vsCMPart.vsCMPartWholeWithAttributes, vsCMWhere.vsCMWhereDeclaration);
					endPoint = codeFunction.get_EndPointOf(vsCMPart.vsCMPartWholeWithAttributes, vsCMWhere.vsCMWhereDeclaration);
				}
				else
				{
					declaration = false;
					startPoint = codeFunction.get_StartPointOf(vsCMPart.vsCMPartWholeWithAttributes, vsCMWhere.vsCMWhereDefinition);
					endPoint = codeFunction.get_EndPointOf(vsCMPart.vsCMPartWholeWithAttributes, vsCMWhere.vsCMWhereDefinition);
				}
				Log.Output(string.Format("{0} {1} - {2}\n{3}\n\n{4}\n", codeFunction.Name, startPoint.AbsoluteCharOffset, endPoint.AbsoluteCharOffset, codeFunction.DeclarationText, codeFunction.BodyText));
				if (startPoint.AbsoluteCharOffset <= selectionStart && selectionStart <= endPoint.AbsoluteCharOffset)
				{
					return (codeFunction, declaration);
				}
			}
			return (null, false);
		}

		public static (VCCodeFunction, bool) FindCodeElementRecursive(CodeElements elements, int selectionStart, string filePath)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			if (null == elements)
			{
				return (null, false);
			}
			foreach (CodeElement codeElement in elements)
			{
				if (codeElement.Kind != vsCMElement.vsCMElementFunction || !(codeElement is VCCodeFunction))
				{
					(VCCodeFunction, bool) recurse = FindCodeElementRecursive(codeElement.Children, selectionStart, filePath);
					if (null != recurse.Item1)
					{
						return recurse;
					}
					continue;
				}
				VCCodeFunction codeFunction = codeElement as VCCodeFunction;
				bool declaration;
				TextPoint startPoint;
				TextPoint endPoint;
				if (codeFunction.DeclarationText.Contains(codeFunction.BodyText))
				{
					declaration = true;
					startPoint = codeFunction.get_StartPointOf(vsCMPart.vsCMPartWholeWithAttributes, vsCMWhere.vsCMWhereDeclaration);
					endPoint = codeFunction.get_EndPointOf(vsCMPart.vsCMPartWholeWithAttributes, vsCMWhere.vsCMWhereDeclaration);
				}
				else
				{
					declaration = false;
					startPoint = codeFunction.get_StartPointOf(vsCMPart.vsCMPartWholeWithAttributes, vsCMWhere.vsCMWhereDefinition);
					endPoint = codeFunction.get_EndPointOf(vsCMPart.vsCMPartWholeWithAttributes, vsCMWhere.vsCMWhereDefinition);
				}
				Log.Output(string.Format("{0} {1} - {2}\n{3}\n\n{4}\n", codeFunction.Name, startPoint.AbsoluteCharOffset, endPoint.AbsoluteCharOffset, codeFunction.DeclarationText, codeFunction.BodyText));
				if (startPoint.AbsoluteCharOffset <= selectionStart && selectionStart <= endPoint.AbsoluteCharOffset)
				{
					return (codeFunction, declaration);
				}
			}
			return (null, false);
		}

		public static string AddIndent(string text, string indent, Types.TypeLineFeed typeLineFeed)
		{
			StringBuilder stringBuilder = new StringBuilder();
			using (TextReader reader = new StringReader(text))
			{
				int count = 0;
				while (true)
				{
					string line = reader.ReadLine();
					if (null == line)
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
			return AddIndent(response.Substring(start, end - start + "*/".Length), indent, typeLineFeed);
		}

		private static bool IsEmptyLine(string line)
		{
			foreach(char c in line)
			{
                if (!char.IsWhiteSpace(c))
                {
					return false; 
                }
            }
			return true;
		}

		public static ITextSnapshotLine GetCommentInsertionLineFromPosition(DocumentView documentView, int declStartLine)
		{
			ITextBuffer textBuffer = documentView.TextView.TextBuffer;
			ITextSnapshotLine line = textBuffer.CurrentSnapshot.GetLineFromLineNumber(declStartLine);
			if (line.LineNumber <= 0)
			{
				return line;
			}
			return textBuffer.CurrentSnapshot.GetLineFromLineNumber(line.LineNumber);
		}
	}
}
