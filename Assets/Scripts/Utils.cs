using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;


public static class Utils {
    /// <summary>
    /// Generate a random number from the OS generator with extra randomness.
    /// </summary>
    public static int SuperRandom() {

        using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider()) {
            
            byte[] randomNumber = new byte[4]; //4 for int32
            rng.GetBytes(randomNumber);
            int value = BitConverter.ToInt32(randomNumber, 0);
            return value;

        }
    }
}


///
/// <summary>Manage a list of things that need to be worked on, if ordering is not important.</summary>
///
public class Jobs<T> : IEnumerable<T> {

    HashSet<T> things = new HashSet<T>();

    // note that number may be out of sync with Any()
    public int Count {
        get { return things.Count; }
    }

    public void Clear() {
        lock(things) {
            things.Clear();
        }
    }

    ///
    /// <summary>add a job wherever</summary>
    ///
    /// <param name="job">data object to save as a job</param>
    ///
    public void Add(T job) {
        lock(things) {
            things.Add(job);
        }
    }

    public void AddAll(IEnumerable<T> collection) {

        lock(things) {

            things.UnionWith(collection);

        }
    }

    ///
    /// <summary>remove a specific job</summary>
    ///
    public bool Remove(T job) {
        lock(things) {
            return things.Remove(job);
        }
    }

    public void RemoveAll(IEnumerable<T> collection) {

        lock(things) {

            things.ExceptWith(collection);

        }
    }

    public bool Contains(T c)
    {
        return things.Contains(c);
    }

    ///
    /// <summary>remove and return one of the jobs stored here</summary>
    ///
    public T Any() {

        //lock(things) {
        if(Monitor.TryEnter(things)) { try {

            HashSet<T>.Enumerator e = things.GetEnumerator();
            if(e.MoveNext()) {

                T job = e.Current;
                e.Dispose();
                things.Remove(job);
                return job;

            }
            else
                return default(T);

        //}
        } finally { Monitor.Exit(things); } } else { return default(T); }

    }

    // use a lambda, like for Sort:
    // list.Sort( (obj1,obj2) => obj1.FirstName.CompareTo(obj2.FirstName) );
    // returns default(T) if list is empty
    public T First(Comparer<T> comparer) {

        T first = default(T);

        lock(things) {

            //iterate things, keep lowest
            bool haveFirst = false;
            foreach(T t in things) {

                if(!haveFirst)
                    first = t;
                else if(comparer.Compare(first,t) < 0)
                    first = t;

            }
        }

        return first;
    }

    public IEnumerator<T> GetEnumerator()
    {
        // clone and enumerate so changes don't break it
        lock(things) {
            return new List<T>(things).GetEnumerator();
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        // clone and enumerate so changes don't break it
        lock(things) {
            return new List<T>(things).GetEnumerator();
        }
    }
}
