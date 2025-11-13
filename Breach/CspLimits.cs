using UnityEngine;

[System.Serializable]
public sealed class CspLimits
{
    [Min(1)]
    public int LogicIterCap = 20000; // Sıralı yapıcı geri-adım tavanı (8x8 için öneri)

    public static CspLimits Default()
    {
        return new CspLimits
        {
            LogicIterCap = 20000
        };
    }
}
