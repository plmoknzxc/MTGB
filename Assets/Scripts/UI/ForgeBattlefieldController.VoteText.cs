using System;
using System.Linq;
using System.Text.RegularExpressions;

public sealed partial class ForgeBattlefieldController
{
    private static bool IsNamedVoteRequest(ForgeRequest request)
    {
        var source=request?.sourceCard;
        return request?.kind=="ChooseOptions"&&source?.id>0&&
            request.message==$"{source.name} ({source.id}) Vote:";
    }

    // These terms are pinned to the source's native vote choices. An unrelated
    // effect using the same English word must keep its own description.
    private string VoteTerm(string source,string term)
    {
        switch((source,term)) {
            case ("Tivit, Seller of Secrets","Evidence"):return TapPaymentText("搜证","Evidence","証拠");
            case ("Tivit, Seller of Secrets","Bribery"):return TapPaymentText("行贿","Bribery","賄賂");
            case ("Plea for Power","Time"):return TapPaymentText("时间","Time","時間");
            case ("Plea for Power","Knowledge"):return TapPaymentText("知识","Knowledge","知識");
            default:return null;
        }
    }

    private string VoteOptionLabel(string native)
    {
        var request=view?.request;
        if(!IsNamedVoteRequest(request))return native;
        string name=request.sourceCard.name,prefix=name+" — ";
        if(native?.StartsWith(prefix,StringComparison.Ordinal)!=true)return native;
        string term=VoteTerm(name,native.Substring(prefix.Length));
        return term==null?native:ConfirmationSourceName(name,request.sourceCard.id)+" — "+term;
    }

    private string VoteNoticeText(ForgeNotice notice,string value)
    {
        if(notice.kind!="notice"||notice.sourceCard==null||notice.sourceCard.id<=0)return null;
        var vote=Regex.Match(value,@"^([^\r\n]+)\r?\nCurrent Votes:(\{[^\r\n{}]*\})$");
        if(!vote.Success)return null;
        string source=notice.sourceCard.name,term=VoteTerm(source,vote.Groups[1].Value);
        if(term==null)return null;
        string map=vote.Groups[2].Value.Substring(1,vote.Groups[2].Value.Length-2);
        var entries=Regex.Matches(map,@"(?:^|, )([^=,\[\]\s][^=,\[\]]*)=\[([^\[\]\r\n]*)\]").Cast<Match>().ToArray();
        // Preserve native names, duplicate votes and map order. If any token or
        // formatting is unfamiliar, retain the whole original notice.
        if(entries.Length==0||string.Concat(entries.Select(e=>e.Value))!=map)return null;
        var keys=entries.Select(e=>VoteTerm(source,e.Groups[1].Value)).ToArray();
        if(keys.Any(k=>k==null))return null;
        string translated="{"+string.Join(", ",entries.Select((e,i)=>keys[i]+"=["+e.Groups[2].Value+"]"))+"}";
        return TapPaymentText("投票：","Vote: ","投票：")+term+"\n"+
            TapPaymentText("当前投票：","Current Votes:","現在の投票：")+translated;
    }
}
