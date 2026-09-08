using System;

// The IL2CPP interop reference set replaces parts of the framework surface.
// These tiny attributes keep the net6 compiler happy without affecting the
// runtime behaviour of the plugin.
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Event | AttributeTargets.Parameter | AttributeTargets.ReturnValue, AllowMultiple = false)]
    internal sealed class NullableAttribute : Attribute
    {
        public NullableAttribute(byte _) { }
        public NullableAttribute(byte[] _) { }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Event | AttributeTargets.Parameter | AttributeTargets.ReturnValue, AllowMultiple = false)]
    internal sealed class NullableContextAttribute : Attribute
    {
        public NullableContextAttribute(byte _) { }
    }
}
