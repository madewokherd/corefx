// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*=============================================================================
**
** Class: DllNotFoundException
**
**
** Purpose: The exception class for some failed P/Invoke calls.
**
**
=============================================================================*/

using System.Runtime.Serialization;

namespace System
{
    [Serializable]
#if !MONO
    [System.Runtime.CompilerServices.TypeForwardedFrom("mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")]
#endif
    public class DllNotFoundException : TypeLoadException
    {
        public DllNotFoundException()
            : base(SR.Arg_DllNotFoundException)
        {
            HResult = HResults.COR_E_DLLNOTFOUND;
        }

        public DllNotFoundException(string message)
            : base(message)
        {
            HResult = HResults.COR_E_DLLNOTFOUND;
        }

        public DllNotFoundException(string message, Exception inner)
            : base(message, inner)
        {
            HResult = HResults.COR_E_DLLNOTFOUND;
        }

        protected DllNotFoundException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
