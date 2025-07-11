using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using GrandCompany = Lumina.Excel.Sheets.GrandCompany;

namespace Henchman.Helpers;

internal static class HuntLogHelper
{
    internal static unsafe int GetGrandCompanyRankInfo()
    {
        int gcMonsterNoteId = Svc.Data.GetExcelSheet<GrandCompany>()
                                 .GetRow(PlayerState.Instance()->GrandCompany)
                                 .Unknown8;

        var gcMonsterNoteRankInfo = MonsterNoteManager.Instance()->RankData[gcMonsterNoteId];

        return gcMonsterNoteRankInfo.Rank;
    }

    internal static unsafe int GetClassJobRankInfo()
    {
        var classMonsterNoteId = Svc.Data.GetExcelSheet<ClassJob>()
                                    .GetRow(PlayerState.Instance()->CurrentClassJobId)
                                    .MonsterNote.RowId.ToInt();

        var classMonsterNoteRankInfo = MonsterNoteManager.Instance()->RankData[classMonsterNoteId];

        return classMonsterNoteRankInfo.Rank;
    }
}
