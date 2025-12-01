using System;
using System.Collections.Generic;

/// <summary>
/// 一个超轻量的全局事件公告栏：
/// - 任何脚本都可以 Publish(某种类型的事件对象)
/// - 想听的脚本用 Subscribe(同一种类型) 注册回调
/// </summary>
public static class EventBus
{
    // 把"事件类型"映射到"订阅这个类型的所有回调"
    private static Dictionary<Type, Delegate> _handlers = new();
    
    /// <summary>
    /// 清空所有订阅（用于域重新加载时避免旧数据残留）
    /// </summary>
    public static void Clear()
    {
        if (_handlers != null)
        {
            _handlers.Clear();
        }
    }

    /// <summary>
    /// 订阅某种类型的事件。例：
    /// EventBus.Subscribe<PlayerBuildMsg>(OnPlayerBuild);
    /// </summary>
    public static void Subscribe<T>(Action<T> handler)
    {
        var key = typeof(T);
        if (_handlers.TryGetValue(key, out var existing))
            _handlers[key] = Delegate.Combine(existing, handler);
        else
            _handlers[key] = handler;
    }

    /// <summary>
    /// 取消订阅
    /// </summary>
    public static void Unsubscribe<T>(Action<T> handler)
    {
        var key = typeof(T);
        if (_handlers.TryGetValue(key, out var existing))
        {
            var cur = Delegate.Remove(existing, handler);
            if (cur == null) _handlers.Remove(key);
            else _handlers[key] = cur;
        }
    }

    /// <summary>
    /// 发布一个事件对象（类型就是频道）。
    /// 所有订阅了该类型的回调都会被调用。
    /// </summary>
    public static void Publish<T>(T evt)
    {
        if (_handlers.TryGetValue(typeof(T), out var d))
            (d as Action<T>)?.Invoke(evt);
    }
}
