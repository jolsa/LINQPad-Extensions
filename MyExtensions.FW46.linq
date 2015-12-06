<Query Kind="Program" />

void Main()
{
	XDocument xml;
	xml = XDocument.Parse(@"
<root>
	<Level1 value=""value1"" add=""false"" id=""1"">D</Level1>
	<Level1 value=""value1"" add=""false"" id=""2"">C</Level1>
		<Level2>Z</Level2>
		<Level2>
			<Level3 id=""3"" />
			<Level3 id=""2"">ZZ</Level3>
			<Level3 id=""1"" />
		</Level2>
		<Level2>Y</Level2>
	<Level1 value=""value1"" add=""false"" id=""4"">B</Level1>
	<Level1 value=""value1"" add=""false"" id=""3"">A</Level1>
</root>
");

	xml.Sort(true);
	xml.ToFormattedXmlString().Replace('\t', '.').Dump("Sorted");

	xml = XDocument.Parse(@"
<root id=""rootNode"">
	Root Node
	<e1 name=""element1"">
		Echo 1
		<e2>Echo 2</e2>
	</e1>
</root>
");

	xml.XPathSelectAttributes("//@name | //@id").Select(x => x.GetXPath()).Dump("XPathSelectAttributes");

	xml.Element("root").Attribute("id").GetXPath().Dump("GetXPath attribute");
	xml.Root.Element("e1").GetXPath().Dump("GetXPath element");

	xml.Descendants().Select(x => x.GetXPath()).Dump("GetXPath all elements");
	xml.Descendants().SelectMany(x => x.Attributes().Select(a => a.GetXPath())).Dump("GetXPath all attributes");
	xml.Descendants().Select(x => new { path = x.GetXPath(), level = x.GetLevel() }).Dump("GetLevel");

	string unformatted = @"<root><el1><el2 name=""El 2"" /></el1></root>";
	unformatted.ToFormattedXmlString().Replace('\t', '.').Dump("Unformatted to Formatted");
	XDocument doc;

	doc = XDocument.Parse(unformatted).Reformat();
	doc.GetLineInfo().LineNumber.Dump("Document LineNumber");
	doc.XPathSelectElement("//el1").GetLineInfo().LineNumber.Dump("Element LineNumber");
	doc.XPathSelectAttribute("//@name").GetLineInfo().LineNumber.Dump("Attribute LineNumber");

	var xws = new XmlWriterSettings() { Indent = true, IndentChars = "\t" };
	XDocument.Parse(unformatted).Reformat(xws).Declaration.Dump("Declaration");
	unformatted.ToFormattedXmlString(xws).Replace("\t", "....").Dump("ToFormattedXmlString");

}

public static class XmlExtensions
{
	public static IXmlLineInfo GetLineInfo(this XObject value)
	{
		return (IXmlLineInfo)value;
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

	//	Returns the value of THIS node excluding any child elements' values
	public static string GetThisNodeValue(this XElement element)
	{
		var n = new XElement(element);
		n.Descendants().Remove();
		return n.Value;
	}

	//	Sorting
	public static XDocument Sort(this XDocument value, bool sortAttributes = false, IEnumerable<string> sortFirstAttributes = null)
	{

		var sortAttrDict = (sortFirstAttributes ?? new[] { "id", "key", "name", "path" }).ToDictionary(k => k, StringComparer.OrdinalIgnoreCase);

		//	Order elements by depth (descending)
		var elements = value.Descendants().OrderByDescending(x => x.GetXPath().Cast<char>().Count(c => c == '/')).ToList();
		elements.ForEach(e =>
		{
			if (sortAttributes && e.Attributes().Any())
			{
				var newElement = new XElement(e.Name);
				newElement.Add(e.Attributes()
					.OrderBy(a => sortAttrDict.ContainsKey(a.Name.LocalName) ? 0 : 1)
					.ThenBy(a => a.Name.LocalName)
					.ThenBy(a => a.Value));
				e.Attributes().Remove();
				e.Add(newElement.Attributes());
			}

			if (e.Elements().Any())
			{
				var newElement = new XElement(e.Name);
				newElement.Add(
					e.Elements()
						.OrderBy(c => c.GetXPath())
						//	Convert attributes to tab-delimited string for secondary sort
						//	e.g.: name="Joe" value="45" becomes name{tab}Joe{tab}value{tab}45
						.ThenBy(c => string.Join("\t", c.Attributes().Select(a => a.Name.LocalName + '\t' + a.Value)))
						.ThenBy(c => c.GetThisNodeValue().Trim())
						.ThenBy(c => ((IXmlLineInfo)c).LineNumber)
					);
				e.Elements().Remove();
				e.Add(newElement.Elements());
			}
		});
		return value;
	}

	//	XPath enhancements
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

	//	Formatting:

	//	Return XDocument after formatting current XDocuments content
	public static XDocument Reformat(this XDocument value, XmlWriterSettings settings = null)
	{
		return XDocument.Parse(value.ToFormattedXmlString(settings), LoadOptions.SetLineInfo);
	}
	//	Return XDocument after formatting current string
	public static XDocument ToFormattedXml(this string xml, XmlWriterSettings settings = null)
	{
		return XDocument.Parse(xml).Reformat(settings);
	}
	//	Return formatted XML string from current string
	public static string ToFormattedXmlString(this string value, XmlWriterSettings settings = null)
	{
		return XDocument.Parse(value).ToFormattedXmlString(settings);
	}
	//	Return formatted XML string from current XDocument
	public static string ToFormattedXmlString(this XDocument value, XmlWriterSettings settings = null)
	{
		if (settings == null)
			settings = GetDefaultSettings();

		using (var sw = new StringWriterEncoded(settings.Encoding ?? Encoding.UTF8))
		using (var xw = XmlWriter.Create(sw, settings))
		{
			value.WriteTo(xw);
			xw.Flush();
			return sw.ToString();
		}
	}

	//	Private support classes and methods:

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

public static class JoinExtensions
{
	public static IEnumerable<TResult> CrossJoin<TOuter, TInner, TResult>(this IEnumerable<TOuter> outer,
		IEnumerable<TInner> inner, Func<TOuter, TInner, TResult> resultSelector)
	{
		return outer.SelectMany(o => inner.Select(i => resultSelector(o, i)));
	}

	public static IEnumerable<TResult> LeftJoin<TOuter, TInner, TKey, TResult>(this IEnumerable<TOuter> outer,
		IEnumerable<TInner> inner, Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector,
		Func<TOuter, TInner, TResult> resultSelector, IEqualityComparer<TKey> comparer)
	{
		return outer.GroupJoin(
			inner,
			outerKeySelector,
			innerKeySelector,
			(o, ei) => ei
				.Select(i => resultSelector(o, i))
				.DefaultIfEmpty(resultSelector(o, default(TInner))), comparer)
				.SelectMany(oi => oi);
	}

	public static IEnumerable<TResult> LeftJoin<TOuter, TInner, TKey, TResult>(this IEnumerable<TOuter> outer,
		IEnumerable<TInner> inner, Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector,
		Func<TOuter, TInner, TResult> resultSelector)
	{
		return outer.LeftJoin(inner, outerKeySelector, innerKeySelector, resultSelector, default(IEqualityComparer<TKey>));
	}

	public static IEnumerable<TResult> RightJoin<TOuter, TInner, TKey, TResult>(this IEnumerable<TOuter> outer,
		IEnumerable<TInner> inner, Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector,
		Func<TOuter, TInner, TResult> resultSelector, IEqualityComparer<TKey> comparer)
	{
		return inner.LeftJoin(outer, innerKeySelector, outerKeySelector, (o, i) => resultSelector(i, o), comparer);
	}

	public static IEnumerable<TResult> RightJoin<TOuter, TInner, TKey, TResult>(this IEnumerable<TOuter> outer,
		IEnumerable<TInner> inner, Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector,
		Func<TOuter, TInner, TResult> resultSelector)
	{
		return outer.RightJoin(inner, outerKeySelector, innerKeySelector, resultSelector, default(IEqualityComparer<TKey>));
	}

	public static IEnumerable<TResult> FullJoin<TOuter, TInner, TKey, TResult>(this IEnumerable<TOuter> outer,
		IEnumerable<TInner> inner, Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector,
		Func<TOuter, TInner, TResult> resultSelector, IEqualityComparer<TKey> comparer)
	{
		var leftInner = outer.LeftJoin(inner, outerKeySelector, innerKeySelector, (o, i) => new { o, i }, comparer);
		var defOuter = default(TOuter);
		var right = outer.RightJoin(inner, outerKeySelector, innerKeySelector, (o, i) => new { o, i }, comparer)
			.Where(oi => oi.o == null || oi.o.Equals(defOuter));
		return leftInner.Concat(right).Select(oi => resultSelector(oi.o, oi.i));
	}

	public static IEnumerable<TResult> FullJoin<TOuter, TInner, TKey, TResult>(this IEnumerable<TOuter> outer,
		IEnumerable<TInner> inner, Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector,
		Func<TOuter, TInner, TResult> resultSelector)
	{
		return outer.FullJoin(inner, outerKeySelector, innerKeySelector, resultSelector, default(IEqualityComparer<TKey>));
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
			.GroupBy(a => a.Name.Namespace == XNamespace.None ? defaultPrefix : a.Name.LocalName, a => XNamespace.Get(a.Value))
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

public static class Utilties
{
	/// <summary>
	/// Convert a mis-cased path to a properly cased one
	/// </summary>
	public static string CleanPath(string fullPath)
	{
		//	Get the root
		fullPath = Path.GetFullPath(fullPath);
		string root = Directory.GetDirectoryRoot(fullPath);
		//	If it has a ':', assume it's a drive and capitalize; otherwise, leave it alone
		if (root.IndexOf(':') >= 0)
			root = root.ToUpper();
		//	Check each part and aggregate
		if (fullPath.Length > root.Length)
			root = fullPath.Substring(root.Length).Split('\\')
				.Aggregate(root, (r, p) => new DirectoryInfo(r).GetFileSystemInfos(p)[0].FullName);
		return root;
	}

}