// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.Serialization;

namespace System.Configuration
{
    [Serializable]
#if !MONO
    [System.Runtime.CompilerServices.TypeForwardedFrom("System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")]
#endif
    public class SettingsPropertyNotFoundException : Exception
    {
        public SettingsPropertyNotFoundException(string message)
            : base(message)
        {
        }

        public SettingsPropertyNotFoundException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected SettingsPropertyNotFoundException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        public SettingsPropertyNotFoundException()
        {            
        }
    }

}
