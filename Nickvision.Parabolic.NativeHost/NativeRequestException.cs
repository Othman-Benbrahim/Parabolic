using System;

namespace Nickvision.Parabolic.NativeHost;

internal sealed class NativeRequestException : Exception
{
    public string Code { get; }

    public NativeRequestException(string code, string message) : base(message)
    {
        Code = code;
    }

    public NativeRequestException(string code, string message, Exception innerException) : base(message, innerException)
    {
        Code = code;
    }
}
