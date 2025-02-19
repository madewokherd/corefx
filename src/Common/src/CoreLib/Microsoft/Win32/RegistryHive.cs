// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Win32
{
    /**
     * Registry hive values.  Useful only for GetRemoteBaseKey
     */
#if REGISTRY_ASSEMBLY
    public
#else
    internal
#endif
    enum RegistryHive
    {
        ClassesRoot = unchecked((int)0x80000000),
        CurrentUser = unchecked((int)0x80000001),
        LocalMachine = unchecked((int)0x80000002),
        Users = unchecked((int)0x80000003),
        PerformanceData = unchecked((int)0x80000004),
        CurrentConfig = unchecked((int)0x80000005),
#if MONO
        DynData = unchecked((int)0x80000006)
#endif
    }
}