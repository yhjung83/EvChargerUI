using System;

namespace EvChargerUI.Services.DspControl
{
    public class DspTimeoutException : Exception
    {
        public DspTimeoutException()
        {
        }

        public DspTimeoutException(string message)
            : base(message)
        {
        }

        public DspTimeoutException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
