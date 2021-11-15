#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

#pragma warning disable 8603

#endregion

namespace Glimmr.Models {
	public class AsyncEvent<TEventArgs> where TEventArgs : DynamicEventArgs {
		private readonly List<Func<object, TEventArgs, Task>> _invocationList;
		private readonly object _locker;

		private AsyncEvent() {
			_invocationList = new List<Func<object, TEventArgs, Task>>();
			_locker = new object();
		}

		public static AsyncEvent<TEventArgs> operator +(
			AsyncEvent<TEventArgs> e, Func<object, TEventArgs, Task> callback) {
			if (callback == null) {
				throw new NullReferenceException("callback is null");
			}

			//Note: Thread safety issue- if two threads register to the same event (on the first time, i.e when it is null)
			//they could get a different instance, so whoever was first will be overridden.
			//A solution for that would be to switch to a public constructor and use it, but then we'll 'lose' the similar syntax to c# events             

			e ??= new AsyncEvent<TEventArgs>();

			lock (e._locker) {
				e._invocationList.Add(callback);
			}

			return e;
		}

		public static AsyncEvent<TEventArgs> operator -(
			AsyncEvent<TEventArgs> e, Func<object, TEventArgs, Task> callback) {
			if (callback == null) {
				throw new NullReferenceException("callback is null");
			}

			if (e == null) {
				return null;
			}

			lock (e._locker) {
				e._invocationList.Remove(callback);
			}

			return e;
		}

		public async Task InvokeAsync(object sender, TEventArgs eventArgs) {
			List<Func<object, TEventArgs, Task>> tmpInvocationList;
			lock (_locker) {
				tmpInvocationList = new List<Func<object, TEventArgs, Task>>(_invocationList);
			}

			var tasks = (from callback in tmpInvocationList
				where sender != null && eventArgs != null
				select callback.Invoke(sender, eventArgs)).ToList();

			await Task.WhenAll(tasks);
		}
	}

	public class DynamicEventArgs : EventArgs {
		public dynamic Arg0 { get; }
		public dynamic? Arg1 { get; }

		public DynamicEventArgs(dynamic input0, dynamic? input1 = null) {
			Arg0 = input0;
			Arg1 = input1;
		}
	}
}