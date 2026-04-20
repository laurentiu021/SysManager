namespace SysManager.Models;

/// <summary>
/// Role of a target inside the health diagnostic chain.
/// The analyzer walks these roles to find where the problem begins:
/// Gateway → DNS → Game/Server.
/// </summary>
public enum TargetRole
{
    Generic,
    Gateway,     // local router — loss here means local network problem
    PublicDns,   // 8.8.8.8 / 1.1.1.1 — loss here means ISP or upstream problem
    GameServer,  // only loss here means the game/service is the culprit
    Streaming
}
