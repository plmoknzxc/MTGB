#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ForgeFetchValidation
{
    static OnlineMatchSession session;
    static double deadline;
    static bool activated, selected;
    static int steps;
    static int fetchSeat, life, library, fieldCount;
    const string Report="Tools/Validation/Reports/CardSelection/fetch-session.txt";
    public static void Run()
    {
        if(session!=null)throw new InvalidOperationException("Validation already running");
        var deck=DeckRepository.Load(Path.Combine(DeckRepository.DeckRoot,"living end.mdk"));
        var fetch=CardDatabaseRepository.LoadAll().First(c=>c.cardName=="Wooded Foothills" && !c.forgeAmbiguous);
        DeckRepository.AddCard(deck,fetch,4);
        // In-memory regression fixture: guarantee fetches are drawn promptly,
        // retaining the real deck's varied eligible and ineligible printings.
        var entry=deck.cards.First(e=>e.setCode+"/"+e.collectorNumber==fetch.PrintingKey);
        entry.count=40;
        session=new OnlineMatchSession(Guid.NewGuid().ToString("N"),deck,deck,4);
        activated=selected=false;steps=0;deadline=EditorApplication.timeSinceStartup+90;
        File.WriteAllText(Report,"RUNNING");EditorApplication.update+=Tick;
    }
    static void Tick()
    {
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Timed out");
            session.Poll();if(session.Failure!=null)throw new Exception(session.Failure);
            for(int seat=1;seat<=2;seat++)
            {
                var state=session.GetView(seat).forge;
                if(!state.awaitingInput)continue;
                if(state.inputPending)throw new Exception("New engine request still marked pending");
                var r=state.request;var me=state.players[seat-1];var options=r.options;
                string pick="ok";
                if(r.kind=="InputPassPriority")
                {
                    if(activated && selected && state.stackCount==0)
                    {
                        var player=state.players[fetchSeat-1];
                        if(player.life!=life-1 || player.libraryCount!=library-1 || player.battlefield.Length!=fieldCount)
                            throw new Exception("Fetch did not pay life, sacrifice itself and put a library card onto battlefield");
                        Finish("PASS: real deck printings, fetch activation, UTF-8 ineligible previews, picker confirmation and final zones through OnlineMatchSession; "+steps+" responses");return;
                    }
                    if(!activated && state.phase=="MAIN1")
                    {
                        var land=me.battlefield.FirstOrDefault(c=>c.name=="Wooded Foothills");
                        if(land!=null){pick="card:"+land.id;activated=true;fetchSeat=seat;life=me.life;library=me.libraryCount;fieldCount=me.battlefield.Length;}
                        else {land=me.hand.FirstOrDefault(c=>c.name=="Wooded Foothills");if(land!=null)pick="card:"+land.id;}
                    }
                }
                else if(r.kind.StartsWith("InputSelect") || r.kind=="SelectCards")
                {
                    if(activated)File.WriteAllText("Tools/Validation/Reports/Interactions/library-"+(r.selectedCount==0?"0":"1")+".json",JsonUtility.ToJson(state));
                    if(activated && !selected)
                    {
                        if(!r.previewCards.Any(c=>c.selection=="不可选"))throw new Exception("Missing intact UTF-8 ineligible previews");
                        if(options.Any(o=>o.card!=null && !string.IsNullOrEmpty(o.card.name) && !o.card.land))throw new Exception("Nonland fetch candidate");
                        File.WriteAllText("Tools/Validation/Reports/CardSelection/fetch-live-choice.json",JsonUtility.ToJson(state));
                    }
                    if(r.selectedCount==0)
                    {
                        var option=options.FirstOrDefault(o=>o.card!=null && o.card.land);
                        if(option!=null){pick=option.id;if(activated)selected=true;}
                    }
                    else pick=options.Any(o=>o.id=="confirm-cards")?"confirm-cards":"ok";
                }
                int index=Array.FindIndex(options,o=>o.id==pick);if(index<0)index=0;
                File.AppendAllText(Report,"\n"+r.kind+" "+state.phase+" "+options[index].id+(steps<12?" hand="+string.Join(",",me.hand.Select(c=>c.name)):""));
                var result=session.ApplyAction(new GameAction{type=GameActionType.ConfirmChoice,playerId=seat,sourceObjectId=r.requestId,optionId=index});
                if(!result.accepted)throw new Exception("Action rejected");
                var ack=session.GetView(seat).forge;
                if(!ack.inputPending || ack.awaitingInput || ack.request?.requestId>0)throw new Exception("Invalid pending acknowledgement");
                if(activated && selected)File.WriteAllText("Tools/Validation/Reports/Interactions/library-ack.json",JsonUtility.ToJson(ack));
                if(++steps>400)throw new Exception("Fetch picker not reached");return;
            }
        }
        catch(Exception e){Finish("FAIL: "+e);}
    }
    static void Finish(string result)
    {
        File.AppendAllText(Report,"\n"+result);EditorApplication.update-=Tick;session?.Dispose();session=null;
    }
}
#endif
