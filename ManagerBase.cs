/* *********************************************************************
 * Date: 22 Jan 2012
 * Created by: Zoltan Juhasz
 * E-Mail: forge@jzo.hu
***********************************************************************/

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Forge.Invoker;
using Forge.Legacy;
using Forge.Shared;
using Forge.Threading;
using Forge.Threading.EventRaiser;
using Forge.Threading.Tasking;

namespace Forge.Management
{

#if NET40

    internal delegate ManagerStateEnum ManagerStartDelegate();

    internal delegate ManagerStateEnum ManagerStopDelegate();

#endif

    /// <summary>
    /// Represents the base methods and properties of a manager service
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable"), Serializable]
    public abstract class ManagerBase : MBRBase, IManager
    {

        #region Field(s)

        private static readonly object ASYNC_BEGIN_LOCK = new object();

        [NonSerialized]
        private int mAsyncActiveStartCount = 0;

        [NonSerialized]
        private AutoResetEvent mAsyncActiveStartEvent = null;

        [NonSerialized]
        private int mAsyncActiveStopCount = 0;

        [NonSerialized]
        private AutoResetEvent mAsyncActiveStopEvent = null;

        [NonSerialized]
        private System.Func<ManagerStateEnum> mStartFuncDelegate = null;

        [NonSerialized]
        private System.Func<ManagerStateEnum> mStopFuncDelegate = null;

#if NET40

        [NonSerialized]
        private ManagerStartDelegate mStartDelegate = null;

        [NonSerialized]
        private ManagerStopDelegate mStopDelegate = null;

#endif

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly object mLockObjectForEvents = new object();

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        [NonSerialized]
        private ManagerStateEnum mManagerstate = ManagerStateEnum.Uninitialized;

        [NonSerialized]
        private EventHandler<ManagerEventStateEventArgs> mEventStartDelegate;

        [NonSerialized]
        private EventHandler<ManagerEventStateEventArgs> mEventStopDelegate;

        /// <summary>
        /// Occurs when [event start].
        /// </summary>
        public event EventHandler<ManagerEventStateEventArgs> EventStart
        {
            add
            {
                lock (mLockObjectForEvents)
                {
                    mEventStartDelegate = (EventHandler<ManagerEventStateEventArgs>)Delegate.Combine(mEventStartDelegate, value);
                }
            }
            remove
            {
                lock (mLockObjectForEvents)
                {
                    mEventStartDelegate = (EventHandler<ManagerEventStateEventArgs>)Delegate.Remove(mEventStartDelegate, value);
                }
            }
        }

        /// <summary>
        /// Occurs when [event stop].
        /// </summary>
        public event EventHandler<ManagerEventStateEventArgs> EventStop
        {
            add
            {
                lock (mLockObjectForEvents)
                {
                    mEventStopDelegate = (EventHandler<ManagerEventStateEventArgs>)Delegate.Combine(mEventStopDelegate, value);
                }
            }
            remove
            {
                lock (mLockObjectForEvents)
                {
                    mEventStopDelegate = (EventHandler<ManagerEventStateEventArgs>)Delegate.Remove(mEventStopDelegate, value);
                }
            }
        }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Initializes a new instance of the <see cref="ManagerBase" /> class.
        /// </summary>
        protected ManagerBase()
            : base()
        {
        }

        #endregion

        #region Public properties

        /// <summary>
        /// Gets or sets the state of the manager.
        /// </summary>
        /// <value>
        /// The state.
        /// </value>
        public ManagerStateEnum ManagerState
        {
            get
            {
                return mManagerstate;
            }
            protected set
            {
                mManagerstate = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether [event sync invocation].
        /// </summary>
        /// <value>
        ///   <c>true</c> if [event sync invocation]; otherwise, <c>false</c>.
        /// </value>
        public bool EventSyncInvocation
        {
            get;
            set;
        } = true;

        /// <summary>
        /// Gets or sets a value indicating whether [event UI invocation].
        /// </summary>
        /// <value>
        ///   <c>true</c> if [event UI invocation]; otherwise, <c>false</c>.
        /// </value>
        public bool EventUIInvocation
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether [event parallel invocation].
        /// </summary>
        /// <value>
        /// 	<c>true</c> if [event parallel invocation]; otherwise, <c>false</c>.
        /// </value>
        public bool EventParallelInvocation
        {
            get;
            set;
        }

        #endregion

        #region Public method(s)

#if NETSTANDARD2_0_OR_GREATER || NETCOREAPP3_1_OR_GREATER
        /// <summary>Triggered when the application host is ready to start the service.</summary>
        /// <param name="cancellationToken">Indicates that the start process has been aborted.</param>
        /// <returns>Task</returns>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await Task.Run(() => Start());
        }

        /// <summary>Triggered when the application host is performing a graceful shutdown.</summary>
        /// <param name="cancellationToken">Indicates that the shutdown process should no longer be graceful.</param>
        /// <returns>Task</returns>
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await Task.Run(() => Stop());
        }
#endif

        /// <summary>
        /// Starts this manager instance.
        /// </summary>
        /// <returns>
        /// Manager State
        /// </returns>
        public abstract ManagerStateEnum Start();

        /// <summary>
        /// Stops this manager instance.
        /// </summary>
        /// <returns>
        /// Manager State
        /// </returns>
        public abstract ManagerStateEnum Stop();

#if NET40

        /// <summary>
        /// Starts the manager asyncronously.
        /// </summary>
        /// <param name="callback">The callback.</param>
        /// <param name="state">The state.</param>
        /// <returns>
        /// Async property
        /// </returns>
        [DebuggerHidden]
        public IAsyncResult BeginStart(AsyncCallback callback, object state)
        {
            Interlocked.Increment(ref mAsyncActiveStartCount);
            ManagerStartDelegate d = new ManagerStartDelegate(Start);
            if (mAsyncActiveStartEvent == null)
            {
                lock (ASYNC_BEGIN_LOCK)
                {
                    if (mAsyncActiveStartEvent == null)
                    {
                        mAsyncActiveStartEvent = new AutoResetEvent(true);
                    }
                }
            }
            mAsyncActiveStartEvent.WaitOne();
            mStartDelegate = d;
            return d.BeginInvoke(callback, state);
        }

        /// <summary>
        /// Ends the asynchronous start process.
        /// </summary>
        /// <param name="asyncResult">The async result.</param>
        /// <returns>
        /// Manager State
        /// </returns>
        [DebuggerHidden]
        public ManagerStateEnum EndStart(IAsyncResult asyncResult)
        {
            if (asyncResult == null)
            {
                ThrowHelper.ThrowArgumentNullException("asyncResult");
            }
            if (mStartDelegate == null)
            {
                ThrowHelper.ThrowArgumentException("Wrong async result or EndStart called multiple times.", "asyncResult");
            }
            try
            {
                return mStartDelegate.EndInvoke(asyncResult);
            }
            finally
            {
                mStartDelegate = null;
                mAsyncActiveStartEvent.Set();
                CloseAsyncActiveStartEvent(Interlocked.Decrement(ref mAsyncActiveStartCount));
            }
        }

        /// <summary>
        /// Stops the manager asyncronously.
        /// </summary>
        /// <param name="callback">The callback.</param>
        /// <param name="state">The state.</param>
        /// <returns>
        /// Async property
        /// </returns>
        [DebuggerHidden]
        public IAsyncResult BeginStop(AsyncCallback callback, object state)
        {
            Interlocked.Increment(ref mAsyncActiveStopCount);
            ManagerStopDelegate d = new ManagerStopDelegate(Stop);
            if (mAsyncActiveStopEvent == null)
            {
                lock (ASYNC_BEGIN_LOCK)
                {
                    if (mAsyncActiveStopEvent == null)
                    {
                        mAsyncActiveStopEvent = new AutoResetEvent(true);
                    }
                }
            }
            mAsyncActiveStopEvent.WaitOne();
            mStopDelegate = d;
            return d.BeginInvoke(callback, state);
        }

        /// <summary>
        /// Ends the asynchronous stop process.
        /// </summary>
        /// <param name="asyncResult">The async result.</param>
        /// <returns>
        /// Manager State
        /// </returns>
        [DebuggerHidden]
        public ManagerStateEnum EndStop(IAsyncResult asyncResult)
        {
            if (asyncResult == null)
            {
                ThrowHelper.ThrowArgumentNullException("asyncResult");
            }
            if (mStopDelegate == null)
            {
                ThrowHelper.ThrowArgumentException("Wrong async result or EndStop called multiple times.", "asyncResult");
            }
            try
            {
                return mStopDelegate.EndInvoke(asyncResult);
            }
            finally
            {
                mStopDelegate = null;
                mAsyncActiveStopEvent.Set();
                CloseAsyncActiveStopEvent(Interlocked.Decrement(ref mAsyncActiveStopCount));
            }
        }

#endif

        /// <summary>Starts the manager asyncronously.</summary>
        /// <param name="callback">The callback.</param>
        /// <param name="state">The state.</param>
        /// <returns>Async property</returns>
        public ITaskResult BeginStart(ReturnCallback callback, object state)
        {
            Interlocked.Increment(ref mAsyncActiveStartCount);
            System.Func<ManagerStateEnum> d = new System.Func<ManagerStateEnum>(Start);
            if (mAsyncActiveStartEvent == null)
            {
                lock (ASYNC_BEGIN_LOCK)
                {
                    if (mAsyncActiveStartEvent == null)
                    {
                        mAsyncActiveStartEvent = new AutoResetEvent(true);
                    }
                }
            }
            mAsyncActiveStartEvent.WaitOne();
            mStartFuncDelegate = d;
            return d.BeginInvoke<ManagerStateEnum>(callback, state);
        }

        /// <summary>Ends the asynchronous start process.</summary>
        /// <param name="asyncResult">The async result.</param>
        /// <returns>Manager State</returns>
        public ManagerStateEnum EndStart(ITaskResult asyncResult)
        {
            if (asyncResult == null)
            {
                ThrowHelper.ThrowArgumentNullException("asyncResult");
            }
            if (mStartFuncDelegate == null)
            {
                ThrowHelper.ThrowArgumentException("Wrong async result or EndStart called multiple times.", "asyncResult");
            }
            try
            {
                return mStartFuncDelegate.EndInvoke(asyncResult);
            }
            finally
            {
                mStartFuncDelegate = null;
                mAsyncActiveStartEvent.Set();
                CloseAsyncActiveStartEvent(Interlocked.Decrement(ref mAsyncActiveStartCount));
            }
        }

        /// <summary>Stops the manager asyncronously.</summary>
        /// <param name="callback">The callback.</param>
        /// <param name="state">The state.</param>
        /// <returns>Async property</returns>
        public ITaskResult BeginStop(ReturnCallback callback, object state)
        {
            Interlocked.Increment(ref mAsyncActiveStopCount);
            System.Func<ManagerStateEnum> d = new System.Func<ManagerStateEnum>(Stop);
            if (mAsyncActiveStopEvent == null)
            {
                lock (ASYNC_BEGIN_LOCK)
                {
                    if (mAsyncActiveStopEvent == null)
                    {
                        mAsyncActiveStopEvent = new AutoResetEvent(true);
                    }
                }
            }
            mAsyncActiveStopEvent.WaitOne();
            mStopFuncDelegate = d;
            return d.BeginInvoke(callback, state);
        }

        /// <summary>Ends the asynchronous stop process.</summary>
        /// <param name="asyncResult">The async result.</param>
        /// <returns>Manager State</returns>
        public ManagerStateEnum EndStop(ITaskResult asyncResult)
        {
            if (asyncResult == null)
            {
                ThrowHelper.ThrowArgumentNullException("asyncResult");
            }
            if (mStopFuncDelegate == null)
            {
                ThrowHelper.ThrowArgumentException("Wrong async result or EndStop called multiple times.", "asyncResult");
            }
            try
            {
                return mStopFuncDelegate.EndInvoke(asyncResult);
            }
            finally
            {
                mStopFuncDelegate = null;
                mAsyncActiveStopEvent.Set();
                CloseAsyncActiveStopEvent(Interlocked.Decrement(ref mAsyncActiveStopCount));
            }
        }

        #endregion

        #region Protected method(s)

        /// <summary>
        /// Raises the event.
        /// </summary>
        /// <param name="del">The delegae (event).</param>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void RaiseEvent(Delegate del, object sender, EventArgs e)
        {
            if (EventSyncInvocation)
            {
                if (EventUIInvocation || EventParallelInvocation)
                {
                    Raiser.CallDelegatorBySync(del, new object[] { sender, e }, EventUIInvocation, EventParallelInvocation);
                }
                else
                {
                    Executor.Invoke(del, sender, e);
                }
            }
            else
            {
                Raiser.CallDelegatorByAsync(del, new object[] { sender, e }, EventUIInvocation, EventParallelInvocation);
            }
        }

        /// <summary>
        /// Called when [start].
        /// </summary>
        /// <param name="state">The state.</param>
        protected virtual void OnStart(ManagerEventStateEnum state)
        {
            RaiseEvent(mEventStartDelegate, this, new ManagerEventStateEventArgs(state));
        }

        /// <summary>
        /// Raises the <see cref="E:StartWithCustomEventArgs" /> event.
        /// </summary>
        /// <param name="e">The <see cref="ManagerEventStateEventArgs" /> instance containing the event data.</param>
        protected virtual void OnStartWithCustomEventArgs(ManagerEventStateEventArgs e)
        {
            RaiseEvent(mEventStartDelegate, this, e);
        }

        /// <summary>
        /// Called when [stop].
        /// </summary>
        /// <param name="state">The state.</param>
        protected virtual void OnStop(ManagerEventStateEnum state)
        {
            RaiseEvent(mEventStopDelegate, this, new ManagerEventStateEventArgs(state));
        }

        /// <summary>
        /// Raises the <see cref="E:StopWithCustomEventArgs" /> event.
        /// </summary>
        /// <param name="e">The <see cref="ManagerEventStateEventArgs" /> instance containing the event data.</param>
        protected virtual void OnStopWithCustomEventArgs(ManagerEventStateEventArgs e)
        {
            RaiseEvent(mEventStopDelegate, this, e);
        }

        #endregion

        #region Private method(s)

        private void CloseAsyncActiveStartEvent(int asyncActiveCount)
        {
            if ((mAsyncActiveStartEvent != null) && (asyncActiveCount == 0))
            {
                mAsyncActiveStartEvent.Close();
                mAsyncActiveStartEvent = null;
            }
        }

        private void CloseAsyncActiveStopEvent(int asyncActiveCount)
        {
            if ((mAsyncActiveStopEvent != null) && (asyncActiveCount == 0))
            {
                mAsyncActiveStopEvent.Close();
                mAsyncActiveStopEvent = null;
            }
        }

        #endregion

    }

}
