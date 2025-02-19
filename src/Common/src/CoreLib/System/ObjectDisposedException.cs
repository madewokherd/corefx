// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Runtime.Serialization;

namespace System
{
    /// <devdoc>
    ///    <para> The exception that is thrown when accessing an object that was
    ///       disposed.</para>
    /// </devdoc>
    [Serializable]
#if !MONO
    [System.Runtime.CompilerServices.TypeForwardedFrom("mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")]
#endif
    public class ObjectDisposedException : InvalidOperationException
    {
        private string _objectName;

        // This constructor should only be called by the EE (COMPlusThrow)
        private ObjectDisposedException() :
            this(null, SR.ObjectDisposed_Generic)
        {
        }

        public ObjectDisposedException(string objectName) :
            this(objectName, SR.ObjectDisposed_Generic)
        {
        }

        public ObjectDisposedException(string objectName, string message) : base(message)
        {
            HResult = HResults.COR_E_OBJECTDISPOSED;
            _objectName = objectName;
        }

        public ObjectDisposedException(string message, Exception innerException)
            : base(message, innerException)
        {
            HResult = HResults.COR_E_OBJECTDISPOSED;
        }

        protected ObjectDisposedException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            _objectName = info.GetString("ObjectName");
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("ObjectName", ObjectName, typeof(string));
        }

        /// <devdoc>
        ///    <para>Gets the text for the message for this exception.</para>
        /// </devdoc>
        public override string Message
        {
            get
            {
                string name = ObjectName;
                if (name == null || name.Length == 0)
                    return base.Message;

                string objectDisposed = SR.Format(SR.ObjectDisposed_ObjectName_Name, name);
                return base.Message + Environment.NewLine + objectDisposed;
            }
        }

        public string ObjectName
        {
            get
            {
                if (_objectName == null)
                {
                    return string.Empty;
                }
                return _objectName;
            }
        }
    }
}
