using System;
using Newtonsoft.Json;

namespace Stl.Fusion.Bridge.Messages
{
    [Serializable]
    public abstract class Message
    {
        public override string ToString()
        {
            var json = JsonConvert.SerializeObject(this, Formatting.Indented);
            return $"{GetType().Name} {json}";
        }
    }
}
