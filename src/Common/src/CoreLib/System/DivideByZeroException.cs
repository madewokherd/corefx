// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*=============================================================================
**
**
**
** Purpose: Exception class for bad arithmetic conditions!
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
    public class DivideByZeroException : ArithmeticException
    {
        public DivideByZeroException()
            : base(SR.Arg_DivideByZero)
        {
            HResult = HResults.COR_E_DIVIDEBYZERO;
        }

        public DivideByZeroException(string message)
            : base(message)
        {
            HResult = HResults.COR_E_DIVIDEBYZERO;
        }

        public DivideByZeroException(string message, Exception innerException)
            : base(message, innerException)
        {
            HResult = HResults.COR_E_DIVIDEBYZERO;
        }

        protected DivideByZeroException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
