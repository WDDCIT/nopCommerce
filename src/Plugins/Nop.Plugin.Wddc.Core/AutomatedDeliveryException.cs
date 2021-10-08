using System;
using System.Runtime.Serialization;

namespace Nop.Plugin.Wddc.Core
{
    [Serializable]
    internal class AutomatedDeliveryException : Exception
    {
        public AutomatedDeliveryException()
        {
        }

        public AutomatedDeliveryException(string message) : base(message)
        {
        }

        public AutomatedDeliveryException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected AutomatedDeliveryException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}