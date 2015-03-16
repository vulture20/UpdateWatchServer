using System;
using System.Runtime.Serialization;
using System.Security.Permissions;

public class UWConfig : ISerializable
{
    public string bindIP { get; set; }
    public Int16 bindPort { get; set; }
    public string dbFile { get; set; }

    public UWConfig()
    {
        this.bindIP = "0.0.0.0";
        this.bindPort = 4584;
        this.dbFile = "UpdateWatch.sqlite";
    }
    protected UWConfig(SerializationInfo info, StreamingContext context)
    {
        if (info == null)
            throw new System.ArgumentNullException("info");
        bindIP = (string)info.GetValue("bindIP ", typeof(string));
        bindPort = (Int16)info.GetValue("bindPort ", typeof(string));
        dbFile = (string)info.GetValue("dbFile ", typeof(string));
    }

    [SecurityPermissionAttribute(SecurityAction.LinkDemand,
        Flags = SecurityPermissionFlag.SerializationFormatter)]
    void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
    {
        if (info == null)
            throw new System.ArgumentNullException("info");
        info.AddValue("bindIP ", bindIP);
        info.AddValue("bindPort ", bindPort);
        info.AddValue("dbFile ", dbFile);
    }
}
