using Community.VisualStudio.Toolkit;
using EnvDTE;
using Microsoft.VisualStudio.RpcContracts.Utilities;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.VCCodeModel;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Tab;

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
            HandyToolsPackage package = await HandyToolsPackage.GetPackageAsync();
            if (null == package)
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
            ProjectItem projectItem = package.DTE.Solution.FindProjectItem(documentView.FilePath);
            if (null == projectItem)
            {
                return (null, null, 0);
            }
            FileCodeModel fileCodeModel = projectItem.FileCodeModel;
            if (null == fileCodeModel)
            {
                return (null, null, 0);
            }
            //TextDocument textDocument = projectItem.Document.Object("TextDocument") as TextDocument;
            CodeElements codeElements = fileCodeModel.CodeElements;
            if (null == codeElements)
            {
                return (null, null, 0);
            }
            int selectionStart = GetCharOffsetWithLF(textSnapshot, selection);
            VCCodeFunction codeFunction = FindCodeElement(codeElements, selectionStart, documentView.FilePath);
            if (null == codeFunction)
            {
                return (null, null, 0);
            }
            if (string.IsNullOrEmpty(codeFunction.BodyText))
            {
                return (null, null, 0);
            }

            TextPoint declStartPoint = codeFunction.get_StartPointOf(vsCMPart.vsCMPartWholeWithAttributes, vsCMWhere.vsCMWhereDeclaration);
            TextPoint defineStartPoint = codeFunction.get_StartPointOf(vsCMPart.vsCMPartWholeWithAttributes, vsCMWhere.vsCMWhereDefinition);
            TextPoint defineEndPoint = codeFunction.get_EndPointOf(vsCMPart.vsCMPartWholeWithAttributes, vsCMWhere.vsCMWhereDefinition);
            EditPoint defineEditPoint = defineStartPoint.CreateEditPoint();
            TextDocument textDocument = declStartPoint.Parent;
            if (null == textDocument)
            {
                return (null, null, 0);
            }

            if (textDocument.Parent.FullName != documentView.FilePath && defineStartPoint.Parent.Parent.FullName == documentView.FilePath)
            {
                declStartPoint = defineStartPoint;
            }

            string textCode = defineEditPoint.GetText(defineEndPoint);
            string indent = string.Empty;
            int lineNumber = 0;
            if (null != declStartPoint)
            {
                lineNumber = Math.Max(declStartPoint.Line - 1, 0);
                if (0 < declStartPoint.LineCharOffset)
                {
                    string lineString = textBuffer.CurrentSnapshot.GetLineFromPosition(selection.Start.Position).GetText();
                    StringBuilder stringBuilder = new StringBuilder(lineString.Length);
                    foreach (char c in lineString)
                    {
                        if (!char.IsWhiteSpace(c))
                        {
                            break;
                        }
                        stringBuilder.Append(c);
                    }
                    indent = stringBuilder.ToString();
                }
            }
            return (textCode, indent, lineNumber);
        }

        public static VCCodeFunction FindCodeElement(CodeElements elements, int selectionStart, string filePath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            //EnvDTE.TextPoint has one base offset, so offset selection points.
            selectionStart = selectionStart + 1;
            foreach (CodeElement codeElement in elements.OfType<CodeElement>())
            {
                if (codeElement.Kind != vsCMElement.vsCMElementFunction || !(codeElement is VCCodeFunction))
                {
                    VCCodeFunction recurse = FindCodeElementRecursive(codeElement.Children, selectionStart, filePath);
                    if (null != recurse)
                    {
                        return recurse;
                    }
                    continue;
                }
                VCCodeFunction codeFunction = codeElement as VCCodeFunction;
                TextPoint startPoint;
                TextPoint endPoint;
                TextPoint declStart = codeFunction.get_StartPointOf(vsCMPart.vsCMPartWholeWithAttributes, vsCMWhere.vsCMWhereDeclaration);
                if (declStart.Parent.Parent.FullName == filePath)
                {

                    startPoint = declStart;
                    endPoint = codeFunction.get_EndPointOf(vsCMPart.vsCMPartWholeWithAttributes, vsCMWhere.vsCMWhereDeclaration);
                }
                else
                {
                    startPoint = codeFunction.get_StartPointOf(vsCMPart.vsCMPartWholeWithAttributes, vsCMWhere.vsCMWhereDefinition);
                    endPoint = codeFunction.get_EndPointOf(vsCMPart.vsCMPartWholeWithAttributes, vsCMWhere.vsCMWhereDefinition);
                    //Log.Output(string.Format("{0} {1} - {2}\n{3}\n\n{4}\n", codeFunction.Name, startPoint.AbsoluteCharOffset, endPoint.AbsoluteCharOffset, codeFunction.DeclarationText, codeFunction.BodyText));
                }

                if (startPoint.AbsoluteCharOffset <= selectionStart && selectionStart <= endPoint.AbsoluteCharOffset)
                {
                    return codeFunction;
                }
            }
            return null;
        }

        public static VCCodeFunction FindCodeElementRecursive(CodeElements elements, int selectionStart, string filePath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (null == elements)
            {
                return null;
            }
            foreach (CodeElement codeElement in elements.OfType<CodeElement>())
            {
                if (codeElement.Kind != vsCMElement.vsCMElementFunction || !(codeElement is VCCodeFunction))
                {
                    VCCodeFunction recurse = FindCodeElementRecursive(codeElement.Children, selectionStart, filePath);
                    if (null != recurse)
                    {
                        return recurse;
                    }
                    continue;
                }
                VCCodeFunction codeFunction = codeElement as VCCodeFunction;
                TextPoint startPoint;
                TextPoint endPoint;
                TextPoint declStart = codeFunction.get_StartPointOf(vsCMPart.vsCMPartWholeWithAttributes, vsCMWhere.vsCMWhereDeclaration);
                if(declStart.Parent.Parent.FullName == filePath)
                {

                    startPoint = declStart;
                    endPoint = codeFunction.get_EndPointOf(vsCMPart.vsCMPartWholeWithAttributes, vsCMWhere.vsCMWhereDeclaration);
                }
                else
                {
                    startPoint = codeFunction.get_StartPointOf(vsCMPart.vsCMPartWholeWithAttributes, vsCMWhere.vsCMWhereDefinition);
                    endPoint = codeFunction.get_EndPointOf(vsCMPart.vsCMPartWholeWithAttributes, vsCMWhere.vsCMWhereDefinition);
                    //Log.Output(string.Format("{0} {1} - {2}\n{3}\n\n{4}\n", codeFunction.Name, startPoint.AbsoluteCharOffset, endPoint.AbsoluteCharOffset, codeFunction.DeclarationText, codeFunction.BodyText));
                }
                if (startPoint.AbsoluteCharOffset <= selectionStart && selectionStart <= endPoint.AbsoluteCharOffset)
                {
                    return codeFunction;
                }
            }
            return null;
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

        public static (string, string) GetCodeAround(DocumentView documentView, SnapshotPoint startPoint, int maxChars)
        {
            System.Diagnostics.Debug.Assert(0<=maxChars);
            StringBuilder stringBuilder = new StringBuilder(128);
            int halfMaxChars = maxChars / 2;
            int currentLine = startPoint.GetContainingLineNumber();
            string lastLine = string.Empty;
            int charCount = 0;
            {
                ITextSnapshotLine line = startPoint.GetContainingLine();
				for (SnapshotPoint p = line.Start; p< startPoint; p = p.Add(1))
                {
                    if(halfMaxChars <= charCount)
                    {
                        break;
                    }
                    stringBuilder.Append(p.GetChar());
                    ++charCount;
                }
                lastLine = stringBuilder.ToString();
                stringBuilder.Clear();
            }
            ITextSnapshot textSnapshot = documentView.TextBuffer.CurrentSnapshot;
            int startLine = currentLine - 1;
            while (0 <= startLine)
            {
                ITextSnapshotLine line = textSnapshot.GetLineFromLineNumber(startLine);
                if(halfMaxChars < (line.Length + 1 + charCount))
                {
                    break;
                }
                charCount += line.Length + 1;
                --startLine;
            }
            if(startLine<0)
            {
                startLine = 0;
            }
            int prefixStartLine = startLine;
            while (startLine < currentLine)
            {
                ITextSnapshotLine line = textSnapshot.GetLineFromLineNumber(startLine);
                stringBuilder.Append(line.GetText());
                stringBuilder.Append('\n');
                ++startLine;
            }
            stringBuilder.Append(lastLine);

            string prefix = stringBuilder.ToString();
            stringBuilder.Clear();
            {
                ITextSnapshotLine line = startPoint.GetContainingLine();
                for (SnapshotPoint p = startPoint; p < line.End; p = p.Add(1))
                {
                    if (maxChars <= charCount)
                    {
                        break;
                    }
                    stringBuilder.Append(p.GetChar());
                    ++charCount;
                }
            }
            startLine = currentLine + 1;
            if(startLine < textSnapshot.LineCount && charCount<maxChars)
            {
                stringBuilder.Append('\n');
                ++charCount;
            }
            while (startLine < textSnapshot.LineCount)
            {
                ITextSnapshotLine line = textSnapshot.GetLineFromLineNumber(startLine);
                if (maxChars < (line.Length + 1 + charCount))
                {
                    break;
                }
                charCount += line.Length + 1;
                ++startLine;
                stringBuilder.Append(line.GetText());
                stringBuilder.Append('\n');
            }
            string suffix = stringBuilder.ToString();
            stringBuilder.Clear();

            startLine = prefixStartLine - 1;
            while (0 <= startLine)
            {
                ITextSnapshotLine line = textSnapshot.GetLineFromLineNumber(startLine);
                if (maxChars < (line.Length + 1 + charCount))
                {
                    break;
                }
                charCount += line.Length + 1;
                --startLine;
            }
            {
                if (startLine < 0)
                {
                    startLine = 0;
                }
                while (startLine < prefixStartLine)
                {
                    ITextSnapshotLine line = textSnapshot.GetLineFromLineNumber(startLine);
                    stringBuilder.Append(line.GetText());
                    stringBuilder.Append('\n');
                    ++startLine;
                }
            }
            prefix = stringBuilder.ToString() + prefix;
            return (prefix, suffix);
        }

        public static string FormatFillInTheMiddle(string prompt, string prefix_code, string suffix_code)
        {
            string result = prompt.Replace("{prefix}", prefix_code);
            return result.Replace("{suffix}", suffix_code);
        }

        public static string GetLine(string text)
        {
            int i;
            for(i=0; i<text.Length; ++i)
            {
                if ('\n' == text[i] || '\r' == text[i])
                {
                    break;
                }
            }
            return text.Substring(0, i);
        }

        public static (string, string) GetNextCompletion(string text)
        {
            System.Diagnostics.Debug.Assert(null != text);
            int wordStart = 0;
            for(int i=0; i<text.Length; ++i)
            {
                if (!char.IsWhiteSpace(text[i]))
                {
                    break;
                }
                ++wordStart;
			}
			int wordEnd = wordStart;
			for (int i = wordEnd; i < text.Length; ++i)
			{
				if (char.IsWhiteSpace(text[i]))
				{
					break;
				}
				++wordEnd;
			}
            string prefix = text.Substring(0, wordEnd);
			string suffix = text.Substring(wordEnd);
            return (prefix, suffix);
		}
    }
}

