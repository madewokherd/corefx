// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Enumeration;
using System.Threading;
using System.Threading.Tasks;

#if MONO && !MOBILE

using System;
using System.IO;
namespace System.IO.CoreFX
#else

namespace System.IO
#endif
{
    /// <devdoc>
    ///    Listens to the system directory change notifications and
    ///    raises events when a directory or file within a directory changes.
    /// </devdoc>
    public partial class FileSystemWatcher : Component, ISupportInitialize
    {
        /// <devdoc>
        ///     Private instance variables
        /// </devdoc>
        // Directory being monitored
        private string _directory;

        // The watch filter for the API call.
        private const NotifyFilters c_defaultNotifyFilters = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
        private NotifyFilters _notifyFilters = c_defaultNotifyFilters;

        // Flag to watch subtree of this directory
        private bool _includeSubdirectories = false;

        // Flag to note whether we are attached to the thread pool and responding to changes
        private bool _enabled = false;

        // Are we in init?
        private bool _initializing = false;

        // Buffer size
        private uint _internalBufferSize = 8192;

        // Used for synchronization
        private bool _disposed;

        // Event handlers
        private FileSystemEventHandler _onChangedHandler = null;
        private FileSystemEventHandler _onCreatedHandler = null;
        private FileSystemEventHandler _onDeletedHandler = null;
        private RenamedEventHandler _onRenamedHandler = null;
        private ErrorEventHandler _onErrorHandler = null;

        // To validate the input for "path"
        private static readonly char[] s_wildcards = new char[] { '?', '*' };

        private const int c_notifyFiltersValidMask = (int)(NotifyFilters.Attributes |
                                                           NotifyFilters.CreationTime |
                                                           NotifyFilters.DirectoryName |
                                                           NotifyFilters.FileName |
                                                           NotifyFilters.LastAccess |
                                                           NotifyFilters.LastWrite |
                                                           NotifyFilters.Security |
                                                           NotifyFilters.Size);

#if DEBUG
        static FileSystemWatcher()
        {
            int s_notifyFiltersValidMask = 0;
            foreach (int enumValue in Enum.GetValues(typeof(NotifyFilters)))
                s_notifyFiltersValidMask |= enumValue;
            Debug.Assert(c_notifyFiltersValidMask == s_notifyFiltersValidMask, "The NotifyFilters enum has changed. The c_notifyFiltersValidMask must be updated to reflect the values of the NotifyFilters enum.");
        }
#endif

        /// <devdoc>
        ///    Initializes a new instance of the <see cref='System.IO.FileSystemWatcher'/> class.
        /// </devdoc>
        public FileSystemWatcher()
        {
            _directory = string.Empty;
        }

        /// <devdoc>
        ///    Initializes a new instance of the <see cref='System.IO.FileSystemWatcher'/> class,
        ///    given the specified directory to monitor.
        /// </devdoc>
        public FileSystemWatcher(string path)
        {
            CheckPathValidity(path);
            _directory = path;
        }

        /// <devdoc>
        ///    Initializes a new instance of the <see cref='System.IO.FileSystemWatcher'/> class,
        ///    given the specified directory and type of files to monitor.
        /// </devdoc>
        public FileSystemWatcher(string path, string filter)
        {
            CheckPathValidity(path);
            _directory = path;
            Filter = filter ?? throw new ArgumentNullException(nameof(filter));
        }

        /// <devdoc>
        ///    Gets or sets the type of changes to watch for.
        /// </devdoc>
        public NotifyFilters NotifyFilter
        {
            get
            {
                return _notifyFilters;
            }
            set
            {
                if (((int)value & ~c_notifyFiltersValidMask) != 0)
                    throw new ArgumentException(SR.Format(SR.InvalidEnumArgument, nameof(value), (int)value, nameof(NotifyFilters)));

                if (_notifyFilters != value)
                {
                    _notifyFilters = value;

                    Restart();
                }
            }
        }

        public Collection<string> Filters { get; } = new NormalizedFilterCollection();

        /// <devdoc>
        ///    Gets or sets a value indicating whether the component is enabled.
        /// </devdoc>
        public bool EnableRaisingEvents
        {
            get
            {
                return _enabled;
            }
            set
            {
                if (_enabled == value)
                {
                    return;
                }

                if (IsSuspended())
                {
                    _enabled = value; // Alert the Component to start watching for events when EndInit is called.
                }
                else
                {
                    if (value)
                    {
                        StartRaisingEventsIfNotDisposed(); // will set _enabled to true once successfully started
                    }
                    else
                    {
                        StopRaisingEvents(); // will set _enabled to false
                    }
                }
            }
        }

        /// <devdoc>
        ///    Gets or sets the filter string, used to determine what files are monitored in a directory.
        /// </devdoc>
        public string Filter
        {
            get
            {
                return Filters.Count == 0 ? "*" : Filters[0];
            }
            set
            {
                Filters.Clear();
                Filters.Add(value);
            }
        }

        /// <devdoc>
        ///    Gets or sets a value indicating whether subdirectories within the specified path should be monitored.
        /// </devdoc>
        public bool IncludeSubdirectories
        {
            get
            {
                return _includeSubdirectories;
            }
            set
            {
                if (_includeSubdirectories != value)
                {
                    _includeSubdirectories = value;

                    Restart();
                }
            }
        }

        /// <devdoc>
        ///    Gets or sets the size of the internal buffer.
        /// </devdoc>
        public int InternalBufferSize
        {
            get
            {
                return (int)_internalBufferSize;
            }
            set
            {
                if (_internalBufferSize != value)
                {
                    if (value < 4096)
                    {
                        _internalBufferSize = 4096;
                    }
                    else
                    {
                        _internalBufferSize = (uint)value;
                    }

                    Restart();
                }
            }
        }

        /// <summary>Allocates a buffer of the requested internal buffer size.</summary>
        /// <returns>The allocated buffer.</returns>
        private byte[] AllocateBuffer()
        {
            try
            {
                return new byte[_internalBufferSize];
            }
            catch (OutOfMemoryException)
            {
                throw new OutOfMemoryException(SR.Format(SR.BufferSizeTooLarge, _internalBufferSize));
            }
        }

        /// <devdoc>
        ///    Gets or sets the path of the directory to watch.
        /// </devdoc>
        public string Path
        {
            get
            {
                return _directory;
            }
            set
            {
                value = (value == null) ? string.Empty : value;
                if (!string.Equals(_directory, value, PathInternal.StringComparison))
                {
                    if (value.Length == 0)
                        throw new ArgumentException(SR.Format(SR.InvalidDirName, value), nameof(Path));

                    if (!Directory.Exists(value))
                        throw new ArgumentException(SR.Format(SR.InvalidDirName_NotExists, value), nameof(Path));

                    _directory = value;
                    Restart();
                }
            }
        }

        /// <devdoc>
        ///    Occurs when a file or directory in the specified <see cref='System.IO.FileSystemWatcher.Path'/> is changed.
        /// </devdoc>
        public event FileSystemEventHandler Changed
        {
            add
            {
                _onChangedHandler += value;
            }
            remove
            {
                _onChangedHandler -= value;
            }
        }

        /// <devdoc>
        ///    Occurs when a file or directory in the specified <see cref='System.IO.FileSystemWatcher.Path'/> is created.
        /// </devdoc>
        public event FileSystemEventHandler Created
        {
            add
            {
                _onCreatedHandler += value;
            }
            remove
            {
                _onCreatedHandler -= value;
            }
        }

        /// <devdoc>
        ///    Occurs when a file or directory in the specified <see cref='System.IO.FileSystemWatcher.Path'/> is deleted.
        /// </devdoc>
        public event FileSystemEventHandler Deleted
        {
            add
            {
                _onDeletedHandler += value;
            }
            remove
            {
                _onDeletedHandler -= value;
            }
        }

        /// <devdoc>
        ///    Occurs when the internal buffer overflows.
        /// </devdoc>
        public event ErrorEventHandler Error
        {
            add
            {
                _onErrorHandler += value;
            }
            remove
            {
                _onErrorHandler -= value;
            }
        }

        /// <devdoc>
        ///    Occurs when a file or directory in the specified <see cref='System.IO.FileSystemWatcher.Path'/>
        ///    is renamed.
        /// </devdoc>
        public event RenamedEventHandler Renamed
        {
            add
            {
                _onRenamedHandler += value;
            }
            remove
            {
                _onRenamedHandler -= value;
            }
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                {
                    //Stop raising events cleans up managed and
                    //unmanaged resources.
                    StopRaisingEvents();

                    // Clean up managed resources
                    _onChangedHandler = null;
                    _onCreatedHandler = null;
                    _onDeletedHandler = null;
                    _onRenamedHandler = null;
                    _onErrorHandler = null;
                }
                else
                {
                    FinalizeDispose();
                }
            }
            finally
            {
                _disposed = true;
                base.Dispose(disposing);
            }
        }

        private static void CheckPathValidity(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            // Early check for directory parameter so that an exception can be thrown as early as possible.
            if (path.Length == 0)
                throw new ArgumentException(SR.Format(SR.InvalidDirName, path), nameof(path));

            if (!Directory.Exists(path))
                throw new ArgumentException(SR.Format(SR.InvalidDirName_NotExists, path), nameof(path));
        }

        /// <summary>
        /// Sees if the name given matches the name filter we have.
        /// </summary>
        private bool MatchPattern(ReadOnlySpan<char> relativePath)
        {
            ReadOnlySpan<char> name = IO.Path.GetFileName(relativePath);
            if (name.Length == 0)
                return false;

            if (Filters.Count == 0)
                return true;

            foreach (string filter in Filters)
            {
                if (FileSystemName.MatchesSimpleExpression(filter, name, ignoreCase: !PathInternal.IsCaseSensitive))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Raises the event to each handler in the list.
        /// </summary>
        private void NotifyInternalBufferOverflowEvent()
        {
            _onErrorHandler?.Invoke(this, new ErrorEventArgs(
                    new InternalBufferOverflowException(SR.Format(SR.FSW_BufferOverflow, _directory))));
        }

        /// <summary>
        /// Raises the event to each handler in the list.
        /// </summary>
        private void NotifyRenameEventArgs(WatcherChangeTypes action, ReadOnlySpan<char> name, ReadOnlySpan<char> oldName)
        {
            // filter if there's no handler or neither new name or old name match a specified pattern
            RenamedEventHandler handler = _onRenamedHandler;
            if (handler != null &&
                (MatchPattern(name) || MatchPattern(oldName)))
            {
                handler(this, new RenamedEventArgs(action, _directory, name.IsEmpty ? null : name.ToString(), oldName.IsEmpty ? null : oldName.ToString()));
            }
        }

        private FileSystemEventHandler GetHandler(WatcherChangeTypes changeType)
        {
            switch (changeType)
            {
                case WatcherChangeTypes.Created:
                    return _onCreatedHandler;
                case WatcherChangeTypes.Deleted:
                    return _onDeletedHandler;
                case WatcherChangeTypes.Changed:
                    return _onChangedHandler;
            }

            Debug.Fail("Unknown FileSystemEvent change type!  Value: " + changeType);
            return null;
        }

        /// <summary>
        /// Raises the event to each handler in the list.
        /// </summary>
        private void NotifyFileSystemEventArgs(WatcherChangeTypes changeType, ReadOnlySpan<char> name)
        {
            FileSystemEventHandler handler = GetHandler(changeType);

            if (handler != null && MatchPattern(name.IsEmpty ? _directory : name))
            {
                handler(this, new FileSystemEventArgs(changeType, _directory, name.IsEmpty ? null : name.ToString()));
            }
        }

        /// <summary>
        /// Raises the event to each handler in the list.
        /// </summary>
        private void NotifyFileSystemEventArgs(WatcherChangeTypes changeType, string name)
        {
            FileSystemEventHandler handler = GetHandler(changeType);

            if (handler != null && MatchPattern(string.IsNullOrEmpty(name) ? _directory : name))
            {
                handler(this, new FileSystemEventArgs(changeType, _directory, name));
            }
        }

        /// <devdoc>
        ///    Raises the <see cref='System.IO.FileSystemWatcher.Changed'/> event.
        /// </devdoc>
        [SuppressMessage("Microsoft.Security", "CA2109:ReviewVisibleEventHandlers", MessageId = "0#", Justification = "Changing from protected to private would be a breaking change")]
        protected void OnChanged(FileSystemEventArgs e)
        {
            InvokeOn(e, _onChangedHandler);
        }

        /// <devdoc>
        ///    Raises the <see cref='System.IO.FileSystemWatcher.Created'/> event.
        /// </devdoc>
        [SuppressMessage("Microsoft.Security", "CA2109:ReviewVisibleEventHandlers", MessageId = "0#", Justification = "Changing from protected to private would be a breaking change")]
        protected void OnCreated(FileSystemEventArgs e)
        {
            InvokeOn(e, _onCreatedHandler);
        }

        /// <devdoc>
        ///    Raises the <see cref='System.IO.FileSystemWatcher.Deleted'/> event.
        /// </devdoc>
        [SuppressMessage("Microsoft.Security", "CA2109:ReviewVisibleEventHandlers", MessageId = "0#", Justification = "Changing from protected to private would be a breaking change")]
        protected void OnDeleted(FileSystemEventArgs e)
        {
            InvokeOn(e, _onDeletedHandler);
        }

        private void InvokeOn(FileSystemEventArgs e, FileSystemEventHandler handler)
        {
            if (handler != null)
            {
                ISynchronizeInvoke syncObj = SynchronizingObject;
                if (syncObj != null && syncObj.InvokeRequired)
                    syncObj.BeginInvoke(handler, new object[] { this, e });
                else
                    handler(this, e);
            }
        }

        /// <devdoc>
        ///    Raises the <see cref='System.IO.FileSystemWatcher.Error'/> event.
        /// </devdoc>
        [SuppressMessage("Microsoft.Security", "CA2109:ReviewVisibleEventHandlers", MessageId = "0#", Justification = "Changing from protected to private would be a breaking change")]
        protected void OnError(ErrorEventArgs e)
        {
            ErrorEventHandler handler = _onErrorHandler;
            if (handler != null)
            {
                ISynchronizeInvoke syncObj = SynchronizingObject;
                if (syncObj != null && syncObj.InvokeRequired)
                    syncObj.BeginInvoke(handler, new object[] { this, e });
                else
                    handler(this, e);
            }
        }

        /// <devdoc>
        ///    Raises the <see cref='System.IO.FileSystemWatcher.Renamed'/> event.
        /// </devdoc>
        [SuppressMessage("Microsoft.Security", "CA2109:ReviewVisibleEventHandlers", MessageId = "0#", Justification = "Changing from protected to private would be a breaking change")]
        protected void OnRenamed(RenamedEventArgs e)
        {
            RenamedEventHandler handler = _onRenamedHandler;
            if (handler != null)
            {
                ISynchronizeInvoke syncObj = SynchronizingObject;
                if (syncObj != null && syncObj.InvokeRequired)
                    syncObj.BeginInvoke(handler, new object[] { this, e });
                else
                    handler(this, e);
            }
        }

        public WaitForChangedResult WaitForChanged(WatcherChangeTypes changeType) =>
            WaitForChanged(changeType, Timeout.Infinite);

        public WaitForChangedResult WaitForChanged(WatcherChangeTypes changeType, int timeout)
        {
            // The full framework implementation doesn't do any argument validation, so
            // none is done here, either.

            var tcs = new TaskCompletionSource<WaitForChangedResult>();
            FileSystemEventHandler fseh = null;
            RenamedEventHandler reh = null;

            // Register the event handlers based on what events are desired.  The full framework
            // doesn't register for the Error event, so this doesn't either.
            if ((changeType & (WatcherChangeTypes.Created | WatcherChangeTypes.Deleted | WatcherChangeTypes.Changed)) != 0)
            {
                fseh = (s, e) =>
                {
                    if ((e.ChangeType & changeType) != 0)
                    {
                        tcs.TrySetResult(new WaitForChangedResult(e.ChangeType, e.Name, oldName: null, timedOut: false));
                    }
                };
                if ((changeType & WatcherChangeTypes.Created) != 0)
                    Created += fseh;
                if ((changeType & WatcherChangeTypes.Deleted) != 0)
                    Deleted += fseh;
                if ((changeType & WatcherChangeTypes.Changed) != 0)
                    Changed += fseh;
            }
            if ((changeType & WatcherChangeTypes.Renamed) != 0)
            {
                reh = (s, e) =>
                {
                    if ((e.ChangeType & changeType) != 0)
                    {
                        tcs.TrySetResult(new WaitForChangedResult(e.ChangeType, e.Name, e.OldName, timedOut: false));
                    }
                };
                Renamed += reh;
            }
            try
            {
                // Enable the FSW if it wasn't already.
                bool wasEnabled = EnableRaisingEvents;
                if (!wasEnabled)
                {
                    EnableRaisingEvents = true;
                }

                // Block until an appropriate event arrives or until we timeout.
                Debug.Assert(EnableRaisingEvents, "Expected EnableRaisingEvents to be true");
                tcs.Task.Wait(timeout);

                // Reset the enabled state to what it was.
                EnableRaisingEvents = wasEnabled;
            }
            finally
            {
                // Unregister the event handlers.
                if (reh != null)
                {
                    Renamed -= reh;
                }
                if (fseh != null)
                {
                    if ((changeType & WatcherChangeTypes.Changed) != 0)
                        Changed -= fseh;
                    if ((changeType & WatcherChangeTypes.Deleted) != 0)
                        Deleted -= fseh;
                    if ((changeType & WatcherChangeTypes.Created) != 0)
                        Created -= fseh;
                }
            }

            // Return the results.
#if MONO
            return tcs.Task.Status == TaskStatus.RanToCompletion ?
                tcs.Task.Result :
                WaitForChangedResult.TimedOutResult;
#else
            return tcs.Task.IsCompletedSuccessfully ?
                tcs.Task.Result :
                WaitForChangedResult.TimedOutResult;
#endif
        }

        /// <devdoc>
        ///     Stops and starts this object.
        /// </devdoc>
        /// <internalonly/>
        private void Restart()
        {
            if ((!IsSuspended()) && _enabled)
            {
                StopRaisingEvents();
                StartRaisingEventsIfNotDisposed();
            }
        }

        private void StartRaisingEventsIfNotDisposed()
        {
            //Cannot allocate the directoryHandle and the readBuffer if the object has been disposed; finalization has been suppressed.
            if (_disposed)
                throw new ObjectDisposedException(GetType().Name);
            StartRaisingEvents();
        }

        public override ISite Site
        {
            get
            {
                return base.Site;
            }
            set
            {
                base.Site = value;

                // set EnableRaisingEvents to true at design time so the user
                // doesn't have to manually. 
                if (Site != null && Site.DesignMode)
                    EnableRaisingEvents = true;
            }
        }

        public ISynchronizeInvoke SynchronizingObject { get; set; }

        public void BeginInit()
        {
            bool oldEnabled = _enabled;
            StopRaisingEvents();
            _enabled = oldEnabled;
            _initializing = true;
        }

        public void EndInit()
        {
            _initializing = false;
            // Start listening to events if _enabled was set to true at some point.
            if (_directory.Length != 0 && _enabled)
                StartRaisingEvents();
        }

        private bool IsSuspended()
        {
            return _initializing || DesignMode;
        }

        private class NormalizedFilterCollection : Collection<string>
        {
            protected override void InsertItem(int index, string item)
            {
                base.InsertItem(index, string.IsNullOrEmpty(item) || item == "*.*" ? "*" : item);
            }

            protected override void SetItem(int index, string item)
            {
                base.SetItem(index, string.IsNullOrEmpty(item) || item == "*.*" ? "*" : item);
            }
        }
    }
}
