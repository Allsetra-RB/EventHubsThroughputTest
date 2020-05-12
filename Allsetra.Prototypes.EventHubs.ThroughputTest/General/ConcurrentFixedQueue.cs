using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Allsetra.Prototypes.EventHubs.ThroughputTest.General
{
	public class ConcurrentFixedQueue<T> : IEnumerable<T>
	{
		private static readonly object Lock = new object();

		private readonly ConcurrentQueue<T> _queue = new ConcurrentQueue<T>();

		public int Capacity { get; set; }

		public IEnumerable<T> Collection
		{
			get { return _queue; }
		}

		public ConcurrentFixedQueue( int capacity )
		{
			Capacity = capacity;
		}

		public void Enqueue( T item )
		{
			// The queue itself is threadsafe already, at least for atomic calls.
			_queue.Enqueue( item );

			lock ( Lock )
			{
				while ( _queue.Count > Capacity && _queue.TryDequeue( out T _ ) )
				{
					// Do nothing...
				}
			}
		}

		public IEnumerator<T> GetEnumerator()
		{
			return Collection.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}
}