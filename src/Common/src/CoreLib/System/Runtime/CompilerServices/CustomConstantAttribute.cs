// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter, Inherited = false)]
#if MONO
    [System.SerializableAttribute]
#endif
    public abstract class CustomConstantAttribute : Attribute
    {
        public abstract object Value { get; }
    }
}
