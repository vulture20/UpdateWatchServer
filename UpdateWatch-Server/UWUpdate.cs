using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Security.Permissions;

public class UWUpdate
{
    [Serializable]
    public class clSendData : ISerializable
    {
        public string dnsName { get; set; }
        public string machineName { get; set; }
        public Int64 tickCount { get; set; }
        public string osVersion { get; set; }
        public Int32 updateCount { get; set; }
        public List<WUpdate> wUpdate { get; set; }

        public clSendData() { }
        protected clSendData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
                throw new System.ArgumentNullException("info");
            dnsName = (string)info.GetValue("dnsName ", typeof(string));
            machineName = (string)info.GetValue("machineName ", typeof(string));
            tickCount = (Int64)info.GetValue("tickCount ", typeof(Int64));
            osVersion = (string)info.GetValue("osVersion ", typeof(string));
            updateCount = (Int32)info.GetValue("updateCount ", typeof(Int32));
            wUpdate = (List<WUpdate>)info.GetValue("wUpdate ", typeof(List<WUpdate>));
        }

        [SecurityPermissionAttribute(SecurityAction.LinkDemand,
            Flags = SecurityPermissionFlag.SerializationFormatter)]
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
                throw new System.ArgumentNullException("info");
            info.AddValue("dnsName ", dnsName);
            info.AddValue("machineName ", machineName);
            info.AddValue("tickCount ", tickCount);
            info.AddValue("osVersion ", osVersion);
            info.AddValue("updateCount ", updateCount);
            info.AddValue("wUpdate ", wUpdate);
        }
    }

    [Serializable]
    public class WUpdate
    {
        public string Description { get; set; }
        public string ReleaseNotes { get; set; }
        public string SupportUrl { get; set; }
        public string Title { get; set; }
        public string UpdateID { get; set; }
        public Int32  RevisionNumber { get; set; }
        public bool   isMandatory { get; set; }
        public bool   isUninstallable { get; set; }
        public string KBArticleIDs { get; set; }
        public string MsrcSeverity { get; set; }
        public Int32  Type { get; set; } // 1 = Software, 2 = Driver
    }
}
