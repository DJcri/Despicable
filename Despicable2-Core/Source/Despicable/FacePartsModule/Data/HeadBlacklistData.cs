using System.Collections.Generic;
using System.Xml.Serialization;

namespace Despicable;
// A serializable class to represent the XML data structure.
public class HeadBlacklistData
{
    [XmlArray("blacklistedHeads")]
    [XmlArrayItem("li")]
    public List<string> blacklistedHeadNames = new();

    [XmlArray("allowedHeads")]
    [XmlArrayItem("li")]
    public List<string> allowedHeadNames = new();
}
