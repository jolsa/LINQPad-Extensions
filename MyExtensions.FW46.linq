<Query Kind="Program" />

void Main()
{
	var xml = XDocument.Parse(@"
<root id=""rootNode"">
	Root Node
	<e1 name=""element1"">
		Echo 1
		<e2>Echo 2</e2>
	</e1>
</root>
");

	xml.XPathSelectAttributes("//@name | //@id").Dump("XPathSelectAttributes");

	xml.Element("root").Attribute("id").GetXPath().Dump("GetXPath attribute");
	xml.Root.Element("e1").GetXPath().Dump("GetXPath element");

	xml.Descendants().Select(x => x.GetXPath()).Dump("GetXPath all elements");
	xml.Descendants().SelectMany(x => x.Attributes().Select(a => a.GetXPath())).Dump("GetXPath all attributes");
	xml.Descendants().Select(x => new { path = x.GetXPath(), level = x.GetLevel() }).Dump("GetLevel");

	string unformatted = @"<root><el1><el2 name=""El 2"" /></el1></root>";
	XDocument doc;

	doc = XDocument.Parse(unformatted).Reformat();
	doc.GetLineInfo().LineNumber.Dump("Document LineNumber");
	doc.XPathSelectElement("//el1").GetLineInfo().LineNumber.Dump("Element LineNumber");
	doc.XPathSelectAttribute("//@name").GetLineInfo().LineNumber.Dump("Attribute LineNumber");

	var xws = new XmlWriterSettings() { Indent = true, IndentChars = "      " };
	XDocument.Parse(unformatted).Reformat(xws).Declaration.Dump("Declaration");
	unformatted.ToFormattedXml(xws).ToFormattedString(xws).Dump("ToFormattedXml");

}

public static class XmlExtensions
{
	public static IXmlLineInfo GetLineInfo(this XObject value)
	{
		return (IXmlLineInfo)value;
	}
	public static string ToFormattedString(this XDocument value, XmlWriterSettings settings = null)
	{
		if (settings == null)
			settings = GetDefaultSettings();

		using (var sw = new StringWriterEncoded(settings.Encoding ?? Encoding.UTF8))
		using (var xw = XmlWriter.Create(sw, settings))
		{
			value.Save(xw);
			xw.Flush();
			return sw.ToString();
		}
	}
	public static int GetLevel(this XElement value)
	{
		int level = 0;
		var e = value;
		while (e.Parent != null)
		{
			level++;
			e = e.Parent;
		}
		return level;
	}
	public static string GetXPath(this XElement value)
	{
		var names = new List<string>() { value.Name.LocalName };
		var e = value;
		while (e.Parent != null)
		{
			names.Add(e.Parent.Name.LocalName);
			e = e.Parent;
		}
		names.Reverse();
		return '/' + string.Join("/", names);
	}
	public static string GetXPath(this XAttribute value)
	{
		return value.Parent.GetXPath() + "[@" + value.Name.LocalName + ']';
	}
	public static string GetThisNodeValue(this XElement element)
	{
		var n = new XElement(element);
		n.Descendants().Remove();
		return n.Value;
	}
	public static XAttribute XPathSelectAttribute(this XContainer value, string expression)
	{
		return value.XPathSelectAttribute(expression, default(IXmlNamespaceResolver));
	}
	public static XAttribute XPathSelectAttribute(this XContainer value, string expression, IXmlNamespaceResolver resolver)
	{
		return value.XPathSelectAttributes(expression, resolver).FirstOrDefault();
	}
	public static IEnumerable<XAttribute> XPathSelectAttributes(this XContainer value, string expression)
	{
		return value.XPathSelectAttributes(expression, default(IXmlNamespaceResolver));
	}
	public static IEnumerable<XAttribute> XPathSelectAttributes(this XContainer value, string expression, IXmlNamespaceResolver resolver)
	{
		return ((IEnumerable)value.XPathEvaluate(expression, resolver)).Cast<XAttribute>();
	}
	public static XDocument Reformat(this XDocument value, XmlWriterSettings settings = null)
	{
		if (settings == null)
			settings = GetDefaultSettings();

		using (var sw = new StringWriterEncoded(settings.Encoding ?? Encoding.UTF8))
		using (var xw = XmlWriter.Create(sw, settings))
		{
			value.WriteTo(xw);
			xw.Flush();
			return XDocument.Parse(sw.ToString(), LoadOptions.SetLineInfo);
		}
	}

	public static XDocument ToFormattedXml(this string xml, XmlWriterSettings settings = null)
	{
		return XDocument.Parse(xml).Reformat(settings);
	}

	private static XmlWriterSettings GetDefaultSettings()
	{
		return new XmlWriterSettings() { Indent = true, IndentChars = "\t" };
	}

	private class StringWriterEncoded : StringWriter
	{
		private readonly Encoding _encoding;
		public StringWriterEncoded(Encoding encoding) : base() { _encoding = encoding; }
		public override Encoding Encoding { get { return _encoding; } }
	}
}

//	XmlNamespaceHelper: Created 11/21/2015 - Johnny Olsa
public class XmlNamespaceHelper
{
	private readonly XmlNamespaceManager _nsm;
	private readonly string _defaultPrefix;
	public XmlNamespaceHelper(XDocument xml, string defaultPrefix)
	{
		_defaultPrefix = defaultPrefix;
		_nsm = new XmlNamespaceManager(new NameTable());
		xml.Root.Attributes()
			.Where(a => a.IsNamespaceDeclaration)
			.GroupBy(a => a.Name.Namespace == XNamespace.None ? String.Empty : a.Name.LocalName, a => XNamespace.Get(a.Value))
			.ToList()
			.ForEach(ns => _nsm.AddNamespace(string.IsNullOrWhiteSpace(ns.Key) ? defaultPrefix : ns.Key, ns.First().NamespaceName));
	}
	public XmlNamespaceManager NamespaceManager { get { return _nsm; } }
	public XName GetXName(string element)
	{
		return GetXName(element, _defaultPrefix);
	}
	public XName GetXName(string element, string prefix)
	{
		return XName.Get(element, _nsm.LookupNamespace(prefix) ?? "");
	}
}