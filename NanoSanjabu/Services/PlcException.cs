using System;

namespace NanoSanjabu.Services
{
    public class PlcException : Exception
    {
        public int ErrorCode { get; }

        public PlcException(string message, int errorCode)
            : base($"{message} (ErrorCode={errorCode})")
        {
            ErrorCode = errorCode;
        }
    }
}