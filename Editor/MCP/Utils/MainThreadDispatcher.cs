#nullable enable

using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEditor;

namespace GameDeck.MCP.Utils
{
    /// <summary>
    /// Dispatches work to the Unity Editor main thread from any background thread.
    /// Replaces <c>MainThread.Instance.Run()</c> from <c>com.IvanMurzak.ReflectorNet.Utils</c>
    /// with zero external dependencies.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unity's engine APIs (scene queries, asset operations, Editor GUI) must be called from the
    /// main thread. MCP tool methods are often invoked from a WebSocket receive thread, so every
    /// tool that touches the engine must route its work through this dispatcher.
    /// </para>
    /// <para>
    /// Call <see cref="Initialize"/> once at Editor startup (e.g. from an
    /// <c>[InitializeOnLoad]</c> static constructor) to hook into <see cref="EditorApplication.update"/>.
    /// </para>
    /// <para>
    /// Main-thread detection uses <c>Thread.CurrentThread.ManagedThreadId == 1</c>, which is the
    /// managed thread ID Unity assigns to its main thread. This is consistent and stable for the
    /// lifetime of the Editor process.
    /// </para>
    /// </remarks>
    public static class MainThreadDispatcher
    {
        #region FIELDS

        private static int _mainThreadId;
        private static readonly ConcurrentQueue<WorkItem> _queue = new();
        private static bool _initialized;

        #endregion

        #region INITIALIZATION METHODS

        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            EditorApplication.update += HandleProcessQueue;
            _initialized = true;
        }

        #endregion

        #region PRIVATE METHODS

        /// <summary>
        /// Returns <c>true</c> when the calling thread is the Unity main thread.
        /// </summary>
        /// <returns><c>true</c> if the current thread's managed ID matches the cached main thread ID; otherwise <c>false</c>.</returns>
        private static bool IsMainThread()
        {
            return Thread.CurrentThread.ManagedThreadId == _mainThreadId;
        }

        /// <summary>
        /// Throws <see cref="InvalidOperationException"/> when <see cref="Initialize"/> has not
        /// been called yet, which would mean the queue is never drained.
        /// </summary>
        private static void EnsureInitialized()
        {
            if (!_initialized)
            {
                throw new InvalidOperationException(
                    "MainThreadDispatcher.Initialize() must be called from the main thread " +
                    "before dispatching work from background threads. " +
                    "Add it to an [InitializeOnLoad] static constructor.");
            }
        }

        #endregion

        #region PUBLIC METHODS

        /// <summary>
        /// Executes <paramref name="func"/> on the main thread and returns its result synchronously.
        /// </summary>
        /// <typeparam name="T">The return type of the work to execute.</typeparam>
        /// <param name="func">
        /// A delegate that calls Unity APIs and returns a value of type <typeparamref name="T"/>.
        /// Must not be <c>null</c>.
        /// </param>
        /// <returns>The value returned by <paramref name="func"/>.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="func"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when <see cref="Initialize"/> has not been called before the first off-thread use.
        /// </exception>
        /// <exception cref="Exception">
        /// Any exception thrown by <paramref name="func"/> is re-thrown on the calling thread.
        /// </exception>
        /// <remarks>
        /// When called from the main thread the delegate is invoked directly, with no queuing
        /// overhead. When called from a background thread the delegate is enqueued and the calling
        /// thread blocks until the main thread processes and completes it.
        /// </remarks>
        public static T Execute<T>(Func<T> func)
        {
            if (func is null)
            {
                throw new ArgumentNullException(nameof(func));
            }

            if (IsMainThread())
            {
                return func();
            }

            EnsureInitialized();
            using var item = new WorkItem<T>(func);
            _queue.Enqueue(item);

            item.Wait();

            return item.GetResultOrThrow();
        }

        #endregion

        #region INTERNAL QUEUE DRAIN

        /// <summary>
        /// Processes all pending work items on the main thread.
        /// Hooked into <see cref="EditorApplication.update"/> by <see cref="Initialize"/>.
        /// </summary>
        private static void HandleProcessQueue()
        {
            while (_queue.TryDequeue(out WorkItem? item))
            {
                item.Execute();
            }
        }

        #endregion

        #region WORK ITEM ABSTRACTIONS

        /// <summary>
        /// Base abstraction for an item stored in the dispatch queue.
        /// </summary>
        private abstract class WorkItem
        {
            #region PUBLIC METHODS

            /// <summary>Executes the stored work on the main thread.</summary>
            public abstract void Execute();

            #endregion
        }

        /// <summary>
        /// A synchronous work item that uses a <see cref="ManualResetEventSlim"/> to block the
        /// producing thread until the main thread completes execution.
        /// </summary>
        /// <typeparam name="T">The result type.</typeparam>
        private sealed class WorkItem<T> : WorkItem, IDisposable
        {
            #region CONSTRUCTOR

            internal WorkItem(Func<T> func)
            {
                OnFunc = func;
            }

            #endregion

            #region FIELDS

            private readonly ManualResetEventSlim _done = new(false);
            private T? _result;
            private Exception? _exception;

            #endregion

            #region EVENTS

            /// <summary>The delegate to execute on the main thread.</summary>
            private readonly Func<T> OnFunc;

            #endregion

            #region PUBLIC METHODS

            /// <inheritdoc/>
            public override void Execute()
            {
                try
                {
                    _result = OnFunc();
                }
                catch (Exception ex)
                {
                    _exception = ex;
                }
                finally
                {
                    _done.Set();
                }
            }

            /// <summary>
            /// Blocks the calling (background) thread until <see cref="Execute"/> has completed.
            /// </summary>
            public void Wait() => _done.Wait();

            /// <summary>
            /// Returns the result produced by the work, or re-throws the captured exception.
            /// </summary>
            /// <returns>The <typeparamref name="T"/> result if the work completed successfully.</returns>
            /// <exception cref="Exception">Re-throws whatever the delegate threw.</exception>
            public T GetResultOrThrow()
            {
                if (_exception is not null)
                {
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(_exception).Throw();
                }

                return _result!;
            }

            /// <inheritdoc/>
            public void Dispose() => _done.Dispose();

            #endregion
        }

        #endregion
    }
}