namespace GLOptimizer.Core.Detection;

public static class MobilePackages
{
    public static IReadOnlyList<string> Pubg { get; } =
    [
        "com.tencent.ig",
        "com.pubg.krmobile",
        "com.rekoo.pubgm",
        "com.vng.pubgmobile",
        "com.tencent.tmgp.pubgmhd"
    ];

    public static IReadOnlyList<string> Cod { get; } =
    [
        "com.activision.callofduty.shooter",
        "com.garena.game.codm",
        "com.tencent.tmgp.kr.codm",
        "com.vng.codmvn"
    ];
}
