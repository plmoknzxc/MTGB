// SPDX-License-Identifier: GPL-3.0-or-later
package mtgb.bridge;

import java.util.*;
import java.nio.file.*;

public final class PhysicalShuffleFixture {
    private static int checks;
    private static void check(boolean value,String message){checks++;if(!value)throw new AssertionError(message);}
    private static List<Integer> ordered(int size){var result=new ArrayList<Integer>();for(int i=0;i<size;i++)result.add(i);return result;}
    private static void noPiles(List<?> cards,Random random){for(int pass=0;pass<7;pass++)BridgeLibraryShuffle.mash(cards,random);BridgeLibraryShuffle.cut(cards,random);}
    private static final class Scripted extends Random {
        final int[] values;final boolean firstLeft;int position;
        Scripted(boolean firstLeft,int... values){this.firstLeft=firstLeft;this.values=values;}
        @Override public boolean nextBoolean(){return firstLeft;}
        @Override public int nextInt(int bound){int value=values[position++];if(value<0||value>=bound)throw new AssertionError("invalid scripted bound "+bound);return value;}
    }
    private static final class Tracked extends Random {
        int splits,lastBound;
        Tracked(long seed){super(seed);}
        @Override public boolean nextBoolean(){splits++;return super.nextBoolean();}
        @Override public int nextInt(int bound){lastBound=bound;return super.nextInt(bound);}
    }
    public static void main(String[] args)throws Exception {
        for(int size:new int[]{0,1,2,7,10,11,40,60,61,500})for(int seed=0;seed<80;seed++)for(boolean piles:new boolean[]{true,false}){
            var original=ordered(size);var first=new ArrayList<>(original);var second=new ArrayList<>(original);
            if(piles){BridgeLibraryShuffle.shuffle(first,new Random(seed));BridgeLibraryShuffle.shuffle(second,new Random(seed));}
            else{noPiles(first,new Random(seed));noPiles(second,new Random(seed));}
            check(first.equals(second),"same input and seed reproduce exact order in either schedule");
            check(first.size()==size&&new HashSet<>(first).equals(new HashSet<>(original)),"every physical identity retained, including short/odd libraries");
        }
        var piles=ordered(23);BridgeLibraryShuffle.dealPiles(piles);
        check(piles.equals(List.of(20,10,0,21,11,1,22,12,2,13,3,14,4,15,5,16,6,17,7,18,8,19,9)),"ten round-robin piles, last card on top, fixed collection order including uneven piles");
        var shortPiles=ordered(7);BridgeLibraryShuffle.dealPiles(shortPiles);
        check(shortPiles.equals(ordered(7)),"fewer than ten cards form single-card piles");
        var packets=ordered(8);BridgeLibraryShuffle.mash(packets,new Scripted(true,0,2,0,1));
        check(packets.equals(List.of(0,1,2,4,3,5,6,7)),"uneven 3/1/2 insertion preserves both packet orders and drains remaining tail");
        var rightFirst=ordered(4);BridgeLibraryShuffle.mash(rightFirst,new Scripted(false,0,0,0,0));
        check(rightFirst.equals(List.of(2,0,3,1)),"right packet can lead an insertion");
        var cut=ordered(4);BridgeLibraryShuffle.cut(cut,new Scripted(false,1));
        check(cut.equals(List.of(2,3,0,1)),"cut moves two intact packets without changing their internal order");
        var defaultOrder=ordered(60);var track=new Tracked(8197);BridgeLibraryShuffle.shuffle(defaultOrder,track);
        check(track.splits==3&&track.lastBound==59,"default schedule performs exactly three mashes and one nonempty cut");
        var alternate=ordered(60);var alternateTrack=new Tracked(8197);noPiles(alternate,alternateTrack);
        check(alternateTrack.splits==7&&alternateTrack.lastBound==59,"no-pile comparison performs exactly seven mashes and one cut");
        for(boolean usePiles:new boolean[]{true,false}){
            var renamed=new ArrayList<String>();for(int i=0;i<60;i++)renamed.add("opaque-"+i);
            var identities=ordered(60);
            if(usePiles){BridgeLibraryShuffle.shuffle(identities,new Random(8197));BridgeLibraryShuffle.shuffle(renamed,new Random(8197));}
            else{noPiles(identities,new Random(8197));noPiles(renamed,new Random(8197));}
            for(int i=0;i<60;i++)check(renamed.get(i).equals("opaque-"+identities.get(i)),"permutation independent of card labels and attributes");
        }
        String result="{\"status\":\"PASS\",\"checks\":"+checks+",\"model\":\""+BridgeLibraryShuffle.MODEL+"\",\"passes\":"+BridgeLibraryShuffle.PASSES+",\"piles\":"+BridgeLibraryShuffle.PILES+",\"maxPacket\":"+BridgeLibraryShuffle.MAX_PACKET+",\"comparisonPasses\":7}";
        Files.writeString(Path.of(args[0]),result);System.out.println(result);
    }
}
