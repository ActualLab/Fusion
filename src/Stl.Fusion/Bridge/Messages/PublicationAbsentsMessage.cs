using System;

namespace Stl.Fusion.Bridge.Messages
{
    [Serializable]
    public class PublicationAbsentsMessage : PublicationMessage
    {
        public bool IsDisposed { get; set; }
    }
}
