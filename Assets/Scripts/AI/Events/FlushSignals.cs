using System;

/// <summary>
/// 请求所有生产者同步刷新“玩家建造”相关的待发送事件。
/// 当前由 RuntimeVoxelBuilding 响应并立刻发布 PlayerBuildPayload。
/// </summary>
public struct FlushPlayerBuildRequest { }


