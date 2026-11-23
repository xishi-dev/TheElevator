using System;

[Serializable]
public struct FloorMission
{
    public string name;
    public int spawnMin, spawnMax, capacityTarget, unloadMin, unloadMax;
    public float loadSeconds, unloadSeconds;

    public FloorMission(string name, int min, int max, int target, float time,
        int unloadMin = 0, int unloadMax = 0, float unloadTime = 0)
    {
        this.name = name;
        spawnMin = min;
        spawnMax = max;
        capacityTarget = target;
        loadSeconds = time;
        this.unloadMin = unloadMin;
        this.unloadMax = unloadMax;
        unloadSeconds = unloadTime;
    }

    public static FloorMission[] DefaultCampaign() => new[]
    {
        new FloorMission("Morning Rush", 20, 25, 17, 25),
        new FloorMission("Shift Change", 18, 22, 25, 25, 5, 7, 15),
        new FloorMission("Coffee Break", 12, 16, 26, 22, 8, 10, 14),
        new FloorMission("Meeting Wave", 14, 18, 27, 23, 9, 11, 14),
        new FloorMission("Lunch Traffic", 16, 20, 28, 24, 10, 12, 15),
        new FloorMission("Overtime", 17, 21, 29, 24, 11, 13, 15),
        new FloorMission("Final Dispatch", 18, 22, 30, 25, 12, 14, 15)
    };

    public bool IsFeasibleFrom(int minimumArrival)
    {
        return capacityTarget > 0 && loadSeconds > 0 && spawnMin > 0 && spawnMax >= spawnMin
            && unloadMin >= 0 && unloadMax >= unloadMin && unloadMax <= minimumArrival
            && (unloadMax == 0 || unloadSeconds > 0)
            && minimumArrival - unloadMax + spawnMin >= capacityTarget;
    }
}
