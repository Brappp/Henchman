using System.Linq;
using ECommons.Configuration;
using Henchman.Data;
using Henchman.Features.RetainerVocate;

namespace Henchman;

[Serializable]
public class Configuration : IEzConfig
{
    public string AutoRotationPlugin = IPCNames.Wrath;
    public bool   DetourForOtherAB   = false;
    public bool   DiscardOldBills    = false;

    /*
     * On Your Mark
     */

    public Dictionary<string, bool> EnableHuntBills  = HuntBoardOptions.ToDictionary(kvp => kvp, _ => false);
    public int                      MinMountDistance = 50;
    public int                      MinRunDistance   = 20;

    public uint MountId = 1;
    /*
     * General Player Config
     */

    public uint QstClassJob = 1;

    /*
     * Bump On A Log
     */

    public int RetainerAmount = 1;

    public NpcDatabase.StarterCity RetainerCity = NpcDatabase.StarterCity.LimsaLominsa;

    public uint                           RetainerClass = 18;
    public RetainerDetails.RetainerGender RetainerGender;

    public RetainerDetails.RetainerPersonality RetainerPersonality = RetainerDetails.RetainerPersonality.Polite;

    public RetainerDetails.RetainerRace RetainerRace;

    public bool                             ReturnOnceDone = false;
    public Lifestream.LifestreamDestination ReturnTo       = Lifestream.LifestreamDestination.Home;

    /*
     * Retainer Creator
     */

    public bool UseMaxRetainerAmount = true;

    public bool UseMeleeRange = false;

    public bool UseMount         = true;
    public bool UseMountRoulette = true;

    public bool UseOnlineMobData = false;
}
