using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

public static class LocalPuzzleOpeningValidation
{
    [MenuItem("MTGB/Validation/Local puzzle opening")]
    public static async void Run()
    {
        try
        {
            var puzzle = OpeningFixture();
            puzzle.cards.Add(new PuzzleCard { name = "Jace Beleren", overrideCounter = true, counterValue = 2 });
            puzzle.cards.Add(new PuzzleCard { name = "Invasion of Zendikar" });
            puzzle.cards.Add(new PuzzleCard { name = "The Eldest Reborn" });
            var pair = new PuzzleCard { name = "Wind Drake", key = "drakes", side = 1, count = 2 };
            puzzle.cards.Add(pair);
            var aura = new PuzzleCard { name = "Pacifism", attachedTo = "drakes", targetCopy = 1 };
            puzzle.cards.Add(aura);

            var view = await Opening(puzzle, false);
            var board = view.players[0].battlefield;
            int Counters(string card, string counter) => board.Single(c => c.name == card).counters.Single(c => c.kind == counter).count;
            Require(Counters("Jace Beleren","LOYALTY") == 2, "Configured loyalty");
            Require(Counters("Invasion of Zendikar","DEFENSE") == 3, "Printed defense");
            Require(Counters("The Eldest Reborn","LORE") == 1, "Initial lore");
            Require(view.stackCount == 0 && view.request.kind == "InputPassPriority", "No entry triggers from the setup");
            var drakes = view.players[1].battlefield.Where(c => c.name == "Wind Drake").OrderBy(c => c.id).ToArray();
            Require(drakes.Length == 2 && board.Single(c => c.name == "Pacifism").attachedToId == drakes[1].id, "Exact attachment copy");

            var equipment = new PuzzleCard { name = "Bonesplitter", attachedTo = pair.key };
            puzzle.cards.Add(equipment);
            Require(puzzle.RemoveLastCopy(pair), "Removing the enchanted copy must detach the aura");
            pair.count++;
            Require(string.IsNullOrEmpty(aura.attachedTo) && aura.targetCopy == 0, "A new copy must not inherit the aura");
            Require(equipment.attachedTo == pair.key && equipment.targetCopy == 0, "The first copy must retain its attachment");
            var independent = new PuzzleCard { name = "Wind Drake", side = 1, key = "other", tapped = true, damage = 1, plusCounters = 1 };
            puzzle.cards.Add(independent);
            aura.attachedTo = independent.key;
            var reopened = await Opening(puzzle.Copy(), false);
            drakes = reopened.players[1].battlefield.Where(c => c.name == "Wind Drake").OrderBy(c => c.id).ToArray();
            Require(drakes.Length == 3 && drakes.Count(c => !c.tapped && c.damage == 0 && c.toughness == 2) == 2, "Two ordinary copies");
            var marked = drakes.Single(c => c.tapped && c.damage == 1 && c.toughness == 3);
            board = reopened.players[0].battlefield;
            Require(board.Single(c => c.name == "Pacifism").attachedToId == marked.id, "Reassigned aura follows the selected copy");
            Require(board.Single(c => c.name == "Bonesplitter").attachedToId == drakes[0].id, "First copy still equipped");

            aura.attachedTo = "ai";
            await Opening(puzzle, true);
            var ordered=OpeningFixture();
            ordered.cards.Add(new PuzzleCard {name="Humility"});
            ordered.cards.Add(new PuzzleCard {name="Opalescence",side=1});
            ordered.PrepareTable();
            var first=await Opening(ordered,false);
            Require(first.players[0].battlefield.Single(c=>c.name=="Humility").toughness==4,"Later Opalescence wins");
            var opal=ordered.cards.Single(c=>c.name=="Opalescence");
            Require(ordered.ShiftOrder(opal,-1),"Cross-seat entry reordering");
            var second=await Opening(ordered.Copy(),false);
            Require(second.players[0].battlefield.Single(c=>c.name=="Humility").toughness==1,"Later Humility wins");

            var layers=OpeningFixture();
            layers.cards.Add(new PuzzleCard {name="Wind Drake",count=2,key="pair"});
            layers.cards.Add(new PuzzleCard {name="Lignify",attachedTo="pair",targetCopy=1});
            layers.cards.Add(new PuzzleCard {name="Frogify",side=1,attachedTo="pair",targetCopy=1});
            layers.PrepareTable();
            var frog=layers.cards.Single(c=>c.name=="Frogify");
            var markedCopy=layers.cards.Where(c=>c.name=="Wind Drake").Last();
            Require(frog.attachedTo==markedCopy.key && frog.targetCopy==0,"Migrated aura retains the second physical copy");
            foreach(int toughness in new[]{1,4})
            {
                var layered=await Opening(layers,false);
                var enchanted=layered.players[1].battlefield.Single(c=>c.name=="Frogify").attachedToId;
                Require(layered.players[0].battlefield.Single(c=>c.id==enchanted).toughness==toughness,"Aura order with target on another seat");
                Require(layered.players[0].battlefield.Single(c=>c.name=="Wind Drake" && c.id!=enchanted).toughness==2,"Other copy is untouched");
                if(toughness==1)Require(layers.ShiftOrder(frog,-1),"Reorder starting auras");
            }
            var worlds=OpeningFixture();
            worlds.cards.Add(new PuzzleCard {name="Concordant Crossroads"});
            worlds.cards.Add(new PuzzleCard {name="Gravity Sphere",side=1});
            worlds.PrepareTable();
            var gravity=worlds.cards.Single(c=>c.name=="Gravity Sphere");
            Require(worlds.ToggleSimultaneous(gravity),"Join across seats");
            var tied=await Opening(worlds.Copy(),false);
            Require(tied.players[0].graveyardCards.Any(c=>c.name=="Concordant Crossroads") &&
                tied.players[1].graveyardCards.Any(c=>c.name=="Gravity Sphere"),"Two simultaneous worlds both leave play");
            Require(worlds.ToggleSimultaneous(gravity),"Split the last world");
            var later=await Opening(worlds.Copy(),false);
            Require(later.players[0].graveyardCards.Any(c=>c.name=="Concordant Crossroads") &&
                later.players[1].battlefield.Any(c=>c.name=="Gravity Sphere"),"Only the later world survives");
            UnityEngine.Debug.Log("[LocalPuzzle] PASS opening state, attachments, opposite timestamp orders, simultaneous worlds and splitting.");
        }
        catch (Exception e) { UnityEngine.Debug.LogException(e); }
    }

    private static LocalPuzzle OpeningFixture()
    {
        var puzzle=new LocalPuzzle {title="Opening validation"};
        puzzle.cards.Add(new PuzzleCard {name="Mountain"});
        puzzle.cards.Add(new PuzzleCard {name="Mountain",zone=2,count=4});
        puzzle.cards.Add(new PuzzleCard {name="Island",side=1,zone=2,count=4});
        return puzzle;
    }

    private static async Task<ForgeSeatView> Opening(LocalPuzzle puzzle, bool invalidAttachment)
    {
        using var session = new OnlineMatchSession(Guid.NewGuid().ToString("N"), new DeckDefinition(), new DeckDefinition(),
            puzzle.seed, new OnlineRoomConfig { formatId="none", startingLife=20, firstPlayerSeat=1 },
            roomId:"local-puzzle", aiSeat:2, localPractice:true, localPuzzle:puzzle.EngineSetup());
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed.TotalSeconds < 60)
        {
            if (session.Poll())
            {
                if (!string.IsNullOrEmpty(session.Failure))
                {
                    Require(invalidAttachment && session.Failure.Contains("结附"),session.Failure);
                    return null;
                }
                var view=session.GetView(1).forge;
                if (view.awaitingInput)
                {
                    Require(!invalidAttachment, "An illegal attachment reached playable priority");
                    return view;
                }
            }
            await Task.Delay(40);
        }
        throw new TimeoutException("Timed out waiting for the puzzle opening.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
