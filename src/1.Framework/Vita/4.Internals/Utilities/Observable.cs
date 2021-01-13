﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Vita.Entities.Utilities {

  /// <summary>
  ///     Simple implementation of <see cref="IObservable{T}" /> interface.
  /// </summary>
  /// <typeparam name="T">The data type.</typeparam>
  public class Observable<T> : IObservable<T> {
    /// <summary>Constructs a new instance. </summary>
    public Observable() {
    }

    // We use copy+add/remove+replace method when adding/removing subscriptions
    // As a benefit, when we broadcast an itme (call OnNext on each subscriber), we iterate the list without locks
    private IList<Subscription> _subscriptions = new List<Subscription>();
    private readonly object _lock = new object();

    #region IObservable implementation

    /// <summary>Subscribes an observer. </summary>
    /// <param name="observer">An observer instance.</param>
    /// <returns>A disposable subscription object. Disposing the object cancels the subscription.</returns>
    public IDisposable Subscribe(IObserver<T> observer) {
      return AddSubscription(observer);
    }

    #endregion

    /// <summary>Unsubscribes an observer. </summary>
    /// <param name="observer">An observer instance.</param>
    public void Unsubscribe(IObserver<T> observer) {
      RemoveSubscription(observer);
    }

    /// <summary>Broadcasts an data. Calls the OnNext method of all subscribed observers. </summary>
    /// <param name="item">An data to broadcast.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Broadcast(T item) {
      var sList = _subscriptions;
      for (int i = 0; i < sList.Count; i++)
        sList[i].Observer.OnNext(item);
    }

    /// <summary>Broadcasts OnCompleted call. </summary>
    public void BroadcastOnCompleted() {
      var sList = _subscriptions;
      for (int i = 0; i < sList.Count; i++)
        sList[i].Observer.OnCompleted();
    }

    /// <summary>Broadcasts an error. Calls OnError method of all subscribers passing the exception as a parameter. </summary>
    /// <param name="error">The exception to broadcast.</param>
    protected void BroadcastError(Exception error) {
      var sList = _subscriptions;
      for (int i = 0; i < sList.Count; i++)
        sList[i].Observer.OnError(error);
    }

    // Subscription list operations
    // We use copy, add/remove, replace method when adding/removing subscriptions
    private Subscription AddSubscription(IObserver<T> observer) {
      lock(_lock) {
        var newList = new List<Subscription>(_subscriptions);
        var subscr = new Subscription() { Observable = this, Observer = observer };
        newList.Add(subscr);
        _subscriptions = newList.ToArray(); // array is more efficient
        return subscr;
      }
    }

    private void RemoveSubscription(IObserver<T> observer) {
      lock(_lock) {
        var newList = new List<Subscription>(_subscriptions);
        var subscr = newList.FirstOrDefault(s => s.Observer == observer);
        if(subscr != null) {
          newList.Remove(subscr);
          _subscriptions = newList.ToArray(); //array is more efficient in loops
        }
      }
    }

    protected void ForEachSubscription(Action<IObserver<T>> action) {
      var sList = _subscriptions;
      for(int i = 0; i < sList.Count; i++) {
        action(sList[i].Observer);
      }
    }

    /// <summary>Represents a subscription for an observable source. </summary>
    internal class Subscription : IDisposable {
      public Observable<T> Observable;
      public IObserver<T> Observer;

      public void Dispose() {
        Observable.Unsubscribe(Observer);
      }
    }

  } //class

  // Utility class
  public static class Observable {

    public static IDisposable Subscribe<T>(this IObservable<T> observable, Action<T> onNext, Action onCompleted = null, Action<Exception> onError = null) {
      var observer = CreateObserver(onNext, onCompleted, onError);
      return observable.Subscribe(observer); 
    }

    public static IObserver<T> CreateObserver<T>(Action<T> onNext, Action onCompleted = null, Action<Exception> onError = null) {
      return new ActionBasedObserver<T>(onNext, onCompleted, onError);
    }

    class ActionBasedObserver<T> : IObserver<T> {
      Action<T> _onNext;
      Action _onCompleted;
      Action<Exception> _onError;

      public ActionBasedObserver(Action<T> onNext, Action onCompleted = null, Action<Exception> onError = null) {
        _onNext = onNext;
        _onCompleted = onCompleted;
        _onError = onError;
      }
      // IObserver implementation
      public void OnNext(T value) => _onNext?.Invoke(value);
      public void OnCompleted() => _onCompleted?.Invoke();
      public void OnError(Exception error) => _onError?.Invoke(error);
    }
  } // ObserverHelper class
}
