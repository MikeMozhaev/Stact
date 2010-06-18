// Copyright 2007-2008 The Apache Software Foundation.
//  
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use 
// this file except in compliance with the License. You may obtain a copy of the 
// License at 
// 
//     http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software distributed 
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR 
// CONDITIONS OF ANY KIND, either express or implied. See the License for the 
// specific language governing permissions and limitations under the License.
namespace Magnum.Fibers
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Threading;
	using Concurrency;
	using Logging;


	public enum FiberState
	{
		Created = 0,
		Starting,
		Running,
		ShuttingDown,
		Stopping,
		Stopped,
	}


	/// <summary>
	/// An Fiber that uses the .NET ThreadPool and QueueUserWorkItem to execute
	/// actions.
	/// </summary>
	[DebuggerDisplay("{GetType().Name} ( Count: {Count} )")]
	public class ThreadPoolFiber :
		Fiber
	{
		static readonly ILogger _log = Logger.GetLogger<ThreadPoolFiber>();

		readonly object _lock = new object();

		ImmutableReference<ImmutableList<Action>> _actions = new ImmutableReference<ImmutableList<Action>>(ImmutableList<Action>.EmptyList);

		bool _executorQueued;
		bool _shuttingDown;
		bool _stopping;

		protected int Count
		{
			get { return _actions.Value.Count; }
		}

		public void Add(Action action)
		{
			if (_shuttingDown)
				throw new FiberException("The fiber is no longer accepting actions");

			ImmutableList<Action> previous = null;
			_actions.Set(x =>
				{
					previous = x;
					return x.Add(action);
				});

			if (previous.Count == 0)
				QueueExecutor();
		}

		public void AddMany(params Action[] actions)
		{
			if (_shuttingDown)
				throw new FiberException("The fiber is no longer accepting actions");

			ImmutableList<Action> previous = null;
			_actions.Set(x =>
				{
					previous = x;
					return x.AddMany(actions);
				});

			if (previous.Count == 0)
				QueueExecutor();
		}

		public virtual void Shutdown(TimeSpan timeout)
		{
			DateTime waitUntil = SystemUtil.Now + timeout;

			lock (_lock)
			{
				_shuttingDown = true;
				Monitor.PulseAll(_lock);

				while (_actions.Value.Count > 0 || _executorQueued)
				{
					timeout = waitUntil - SystemUtil.Now;
					if (timeout < TimeSpan.Zero)
						throw new FiberException("Timeout expired waiting for all pending actions to complete during shutdown");

					Monitor.Wait(_lock, timeout);
				}
			}
		}

		public void Stop()
		{
			_shuttingDown = true;
			_stopping = true;
		}

		void QueueExecutor()
		{
			lock (_lock)
			{
				if (_executorQueued)
					return;

				QueueWorkItem();
			}
		}

		void QueueWorkItem()
		{
			if (!ThreadPool.QueueUserWorkItem(x => Execute()))
				throw new FiberException("QueueUserWorkItem did not accept our execute method");

			_executorQueued = true;
		}

		bool Execute()
		{
			ImmutableList<Action> actions = RemoveAll();

			ExecuteActions(actions);

			lock (_lock)
			{
				if (_actions.Value.Count > 0)
					QueueWorkItem();
				else
				{
					_executorQueued = false;

					Monitor.PulseAll(_lock);
				}
			}

			return true;
		}

		void ExecuteActions(IEnumerable<Action> actions)
		{
			foreach (Action action in actions)
			{
				if (_stopping)
					break;

				action();
			}
		}

		ImmutableList<Action> RemoveAll()
		{
			ImmutableList<Action> runActions = null;

			_actions.Set(x =>
				{
					runActions = x;

					return ImmutableList<Action>.EmptyList;
				});

			return runActions;
		}
	}
}