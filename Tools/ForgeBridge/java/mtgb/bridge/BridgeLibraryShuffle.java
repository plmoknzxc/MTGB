// SPDX-License-Identifier: GPL-3.0-or-later
package mtgb.bridge;

import java.util.*;

/** Round-robin pile deal, finite packet mashes, then a cut; card-agnostic. */
final class BridgeLibraryShuffle {
    static final String MODEL="physical-pile10-mash3-cut-v2";
    static final int PILES=10, PASSES=3, MAX_PACKET=3;

    static <T> void shuffle(List<T> cards,Random random) {
        if(cards.size()<2)return;
        dealPiles(cards);
        for(int pass=0;pass<PASSES;pass++)mash(cards,random);
        cut(cards,random);
    }

    static <T> void dealPiles(List<T> cards) {
        int size=cards.size();
        List<T> collected=new ArrayList<>(size);
        // Top-first input: each newly dealt face-down card covers the previous one.
        // Collect the upper row left-to-right, then the lower row, as whole packets.
        for(int pile=0;pile<Math.min(PILES,size);pile++){
            int last=pile+((size-1-pile)/PILES)*PILES;
            for(int index=last;index>=pile;index-=PILES)collected.add(cards.get(index));
        }
        for(int i=0;i<size;i++)cards.set(i,collected.get(i));
    }

    static <T> void mash(List<T> cards,Random random) {
        int size=cards.size();
        if(size<2)return;
        // A near-half split (+/-10%) and alternating 1..3-card packets approximate
        // hand insertion. No uniform permutation or card-type correction follows.
        int spread=size/10;
        int split=Math.max(1,Math.min(size-1,size/2+random.nextInt(spread*2+1)-spread));
        int left=0,right=split;
        boolean takeLeft=random.nextBoolean();
        List<T> mixed=new ArrayList<>(size);
        while(left<split&&right<size){
            int count=1+random.nextInt(MAX_PACKET);
            if(takeLeft)for(int n=0;n<count&&left<split;n++)mixed.add(cards.get(left++));
            else for(int n=0;n<count&&right<size;n++)mixed.add(cards.get(right++));
            takeLeft=!takeLeft;
        }
        while(left<split)mixed.add(cards.get(left++));
        while(right<size)mixed.add(cards.get(right++));
        for(int i=0;i<size;i++)cards.set(i,mixed.get(i));
    }

    static <T> void cut(List<T> cards,Random random) {
        if(cards.size()>1)Collections.rotate(cards,-(1+random.nextInt(cards.size()-1)));
    }
}
