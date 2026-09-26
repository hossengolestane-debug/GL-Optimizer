namespace GLOptimizer.Core.Detection;

/// <summary>
/// Maps MSFT_PhysicalDisk MediaType codes. 3 is HDD and 4 is SSD. Other codes are ignored.
/// </summary>
public static class StorageMedia
{
    public static string? Describe(IEnumerable<int>? mediaTypes)
    {
        if (mediaTypes is null)
        {
            return null;
        }

        var ssd = false;
        var hdd = false;
        foreach (var mediaType in mediaTypes)
        {
            if (mediaType == 4)
            {
                ssd = true;
            }
            else if (mediaType == 3)
            {
                hdd = true;
            }
        }

        if (ssd && hdd)
        {
            return "SSD + HDD";
        }

        if (ssd)
        {
            return "SSD";
        }

        return hdd ? "HDD" : null;
    }
}
