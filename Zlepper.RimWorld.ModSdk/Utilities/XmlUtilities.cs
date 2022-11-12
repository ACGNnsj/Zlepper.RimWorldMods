using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace Zlepper.RimWorld.ModSdk.Utilities;

public static class XmlUtilities
{
    public static string SerializeToXml<T>(T o, string? rootName = null)
        where T : notnull
    {
        var ns = new XmlSerializerNamespaces(new[] {XmlQualifiedName.Empty});
        
        var serializer = GetSerializer<T>(rootName);

        using var sw = new StringWriter();
        using var xmlWriter = XmlWriter.Create(sw, new XmlWriterSettings
        {
            Encoding = Encoding.UTF8,
            Indent = true,
            OmitXmlDeclaration = true,
            NewLineChars = "\n"
        });
        serializer.Serialize(xmlWriter, o, ns);
        return sw.ToString();
    }
    
    public static T DeserializeFromXml<T>(string xml, string? rootName = null)
        where T : notnull
    {
        var serializer = GetSerializer<T>(rootName);

        using var sr = new StringReader(xml);
        using var xmlReader = XmlReader.Create(sr, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore,
        });
        return (T) serializer.Deserialize(xmlReader);
    }
    
    
    public static T DeserializeFromXml<T>(Stream stream, string? rootName = null)
        where T : notnull
    {
        var serializer = GetSerializer<T>(rootName);

        using var xmlReader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore,
        });
        return (T) serializer.Deserialize(xmlReader);
    }

    public static T DeserializeFromFile<T>(string fileName, string? rootName = null)
        where T : notnull
    {
        using var stream = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
        return DeserializeFromXml<T>(stream, rootName);
    }

    private static XmlSerializer GetSerializer<T>(string? rootName) where T : notnull
    {
        var serializer = rootName == null
            ? new XmlSerializer(typeof(T))
            : new XmlSerializer(typeof(T), new XmlRootAttribute(rootName));
        return serializer;
    }

    public static T? ConvertNodeContent<T>(XmlNode node) where T: class
    {
        var stm = new MemoryStream();

        var stw = new StreamWriter(stm);
        stw.Write(node.InnerXml);
        stw.Flush();

        stm.Position = 0;

        var ser = new XmlSerializer(typeof(T));
        return ser.Deserialize(stm) as T;
    }

}