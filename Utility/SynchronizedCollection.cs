using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Lunacy.Tcp.Utility {
    public class SynchronizedCollection<T> : ICollection<T>, INotifyCollectionChanged {
        public event NotifyCollectionChangedEventHandler? CollectionChanged;

        public int Count {
            get {
                lock(_lock) {
                    return _Items.Count;
                }
            }
        }

        public bool IsReadOnly {
            get {
                return false;
            }
        }

        protected readonly object _lock = new();
        protected readonly Collection<T> _Items;

        public SynchronizedCollection() : this([]) { }
        public SynchronizedCollection(Collection<T> baseCollection) {
            _Items = baseCollection;
        }

        public virtual void Add(T item) {
            lock(_lock) {
                _Items.Add(item);
            }

            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item));
        }

        public virtual bool Remove(T item) {
            bool success = false;
            lock(_lock) {
                success = _Items.Remove(item);
            }

            if(success) {
                CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item));
            }

            return success;
        }

        public virtual void Clear() {
            lock(_lock) {
                _Items.Clear();
            }

            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public virtual bool Contains(T item) {
            lock(_lock) {
                return _Items.Contains(item);
            }
        }

        public virtual void CopyTo(T[] array, int arrayIndex) {
            lock(_lock) {
                _Items.CopyTo(array, arrayIndex);
            }
        }

        public virtual IEnumerator<T> GetEnumerator() {
            lock(_lock) {
                return _Items.ToList().GetEnumerator();
            }
        }

        IEnumerator IEnumerable.GetEnumerator() {
            lock(_lock) {
                return _Items.ToList().GetEnumerator();
            }
        }
    }
}