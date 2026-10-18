using System;
using System.Collections.Generic;
using UnityEngine;


public static class G
{
    static readonly Dictionary<Type, object> services = new();
    
    public static void Register<T>(T instance) where T : class
    {
        if (instance == null) throw new ArgumentNullException(nameof(instance));
        services[instance.GetType()] = instance;
    }

    public static bool TryGet<T>(out T service) where T : class
    {
        if (services.TryGetValue(typeof(T), out var obj) && obj is T s)
        {
            service = s;
            return true;
        }
        service = null;
        return false;
    }

    public static T Get<T>() where T : class
    {
        if (TryGet<T>(out var s)) return s;
        return null;
    }

    public static T GetRequired<T>() where T : class
    {
        var s = Get<T>();
        if (s == null) throw new InvalidOperationException($"Service of type {typeof(T)} is not registered in G.");
        return s;
    }

    public static void Clear()
    {
        services.Clear();
    }
}