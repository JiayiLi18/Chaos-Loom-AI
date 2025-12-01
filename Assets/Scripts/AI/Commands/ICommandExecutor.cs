using UnityEngine;
using System;

/// <summary>
/// 命令执行器接口 - 所有命令执行器必须实现此接口
/// </summary>
public interface ICommandExecutor
{
    /// <summary>
    /// 执行命令
    /// </summary>
    /// <param name="commandId">命令ID</param>
    /// <param name="paramsData">命令参数</param>
    /// <param name="onComplete">完成回调（success, errorMessage）</param>
    void Execute(string commandId, object paramsData, Action<bool, string> onComplete);
    
    /// <summary>
    /// 获取命令类型
    /// </summary>
    string CommandType { get; }
    
    /// <summary>
    /// 是否可以中断当前执行
    /// </summary>
    bool CanInterrupt { get; }
    
    /// <summary>
    /// 中断当前执行
    /// </summary>
    void Interrupt();
}

