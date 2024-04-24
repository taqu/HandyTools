using MSXML;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace HandyTools.Completion
{
    internal static class SnippetUtil
    {
        internal static DOMDocument GenerateSnippetXml(string inputCode, string language)
        {
            DOMDocument domDoc = new MSXML.DOMDocument();
            IXMLDOMElement codeShnippets = domDoc.createElement("CodeSnippets");
            codeShnippets.setAttribute("xmlns", "http://schemas.microsoft.com/VisualStudio/2005/CodeSnippet");
            domDoc.appendChild(codeShnippets);

            IXMLDOMElement codeSnippet = domDoc.createElement("CodeSnippet");
            codeSnippet.setAttribute("Format", "1.0.0");

            IXMLDOMElement header = domDoc.createElement("Header");
            IXMLDOMElement title = domDoc.createElement("Title");
            title.text = "Completion";
            IXMLDOMElement shortcut = domDoc.createElement("Shortcut");
            IXMLDOMElement description = domDoc.createElement("Description");
            IXMLDOMElement author = domDoc.createElement("Author");

            IXMLDOMElement snipTypes = domDoc.createElement("SnippetTypes");
            IXMLDOMElement expType = domDoc.createElement("SnippetType");
            expType.text = "Expansion";
            snipTypes.appendChild(expType);

            header.appendChild(title);
            header.appendChild(shortcut);
            header.appendChild(description);
            header.appendChild(author);
            header.appendChild(snipTypes);
            codeSnippet.appendChild(header);

            IXMLDOMElement snippet = domDoc.createElement("Snippet");
            IXMLDOMElement declarations = domDoc.createElement("Declarations");
            //foreach (var replacement in dynSnippet.Replacements)
            //{

            //    var literal = xmlDoc.CreateElement("Literal");
            //    var id = xmlDoc.CreateElement("ID");
            //    id.InnerText = replacement.ID;
            //    var tooltip = xmlDoc.CreateElement("ToolTip");
            //    tooltip.InnerText = replacement.ToolTip;
            //    var def = xmlDoc.CreateElement("Default");
            //    def.InnerText = replacement.Default;
            //    literal.AppendChild(id);
            //    literal.AppendChild(tooltip);
            //    literal.AppendChild(def);
            //    declars.AppendChild(literal);
            //}
            snippet.appendChild(declarations);

            IXMLDOMElement code = domDoc.createElement("Code");
            code.setAttribute("Language", language);
			IXMLDOMCDATASection cdata = domDoc.createCDATASection(inputCode);
            code.appendChild(cdata);
            snippet.appendChild(code);
            codeSnippet.appendChild(snippet);
			codeShnippets.appendChild(codeSnippet);
			Log.Output(domDoc.xml);
            return domDoc;
        }
    }
}

